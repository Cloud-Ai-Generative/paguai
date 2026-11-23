using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PagUai.Mensageria;

/// <summary>Quem trata os eventos de uma fila. Uma instância por mensagem (escopo próprio, com o seu DbContext).</summary>
public interface IManipuladorDeEventos
{
    Task TratarAsync(EventoRecebido evento, CancellationToken ct);
}

/// <summary>
/// Consome uma fila e chama o manipulador para cada mensagem.
///
///  - CONFIRMAÇÃO MANUAL (ack): a mensagem só sai da fila depois que o manipulador terminou sem erro.
///    Se o processo cair no meio, o RabbitMQ entrega de novo. Por isso a entrega é "pelo menos uma vez"
///    e o manipulador PRECISA ser idempotente (receber o mesmo evento duas vezes sem efeito duplicado).
///  - RETRY: se o manipulador falhar, a mensagem vai para a fila de retry, espera e volta.
///  - DEAD LETTER: na última tentativa, ou se a mensagem for inválida, ela é rejeitada e vai para a fila .dlq.
/// </summary>
public sealed class ConsumidorDeEventos<TManipulador>(
    ConfiguracaoDaFila fila,
    ConexaoRabbitMq conexao,
    IServiceScopeFactory escopos,
    ILogger<ConsumidorDeEventos<TManipulador>> logger) : BackgroundService
    where TManipulador : IManipuladorDeEventos
{
    public const string CabecalhoTentativa = "x-paguai-tentativa";

    private readonly TaskCompletionSource _pronto = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IChannel? _canal;

    public ConfiguracaoDaFila Fila => fila;

    /// <summary>Completa quando o consumidor já está recebendo mensagens (útil nos testes).</summary>
    public Task Pronto => _pronto.Task;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var conexaoAberta = await ConectarAsync(ct);
        if (conexaoAberta is null)
        {
            return;
        }

        _canal = await conexaoAberta.CreateChannelAsync(cancellationToken: ct);
        await Topologia.DeclararFilaAsync(_canal, fila, ct);
        await _canal.BasicQosAsync(prefetchSize: 0, prefetchCount: fila.Prefetch, global: false, ct);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.ReceivedAsync += (_, entrega) => ProcessarAsync(_canal, entrega, ct);
        await _canal.BasicConsumeAsync(fila.Nome, autoAck: false, consumidor, ct);

        logger.LogInformation("Consumindo a fila {Fila} (chaves: {Chaves}; {Tentativas} tentativas, {Espera} s entre elas).",
            fila.Nome, string.Join(", ", fila.Chaves), fila.MaximoDeTentativas, fila.Espera.TotalSeconds);
        _pronto.TrySetResult();

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<IConnection?> ConectarAsync(CancellationToken ct)
    {
        for (var tentativa = 1; !ct.IsCancellationRequested; tentativa++)
        {
            try
            {
                return await conexao.ObterAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // No Docker Compose o broker pode demorar alguns segundos para ficar pronto.
                logger.LogWarning("RabbitMQ ainda indisponível (tentativa {Tentativa}): {Erro}", tentativa, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        return null;
    }

    private async Task ProcessarAsync(IChannel canal, BasicDeliverEventArgs entrega, CancellationToken ct)
    {
        var tentativa = LerTentativa(entrega.BasicProperties);
        EventoRecebido? evento = null;
        try
        {
            evento = Ler(entrega.Body, tentativa);

            await using var escopo = escopos.CreateAsyncScope();
            var manipulador = escopo.ServiceProvider.GetRequiredService<TManipulador>();
            await manipulador.TratarAsync(evento, ct);

            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Desligando: sem ack, o RabbitMQ devolve a mensagem à fila quando o canal fechar.
        }
        catch (MensagemInvalida ex)
        {
            logger.LogError("Mensagem inválida na fila {Fila}: {Erro} Enviada para {Dlq} sem novas tentativas.", fila.Nome, ex.Message, fila.FilaDeDeadLetter);
            await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, ct);
        }
        catch (Exception ex) when (tentativa < fila.MaximoDeTentativas)
        {
            logger.LogWarning("Tentativa {Tentativa} de {Maximo} falhou para {Tipo} {Id} na fila {Fila}: {Erro}. Nova tentativa em {Espera} s.",
                tentativa, fila.MaximoDeTentativas, evento?.Tipo, evento?.Id, fila.Nome, ex.Message.TrimEnd('.'), fila.Espera.TotalSeconds);
            await AgendarNovaTentativaAsync(canal, entrega, tentativa + 1, ct);
            await canal.BasicAckAsync(entrega.DeliveryTag, multiple: false, ct);
        }
        catch (Exception ex)
        {
            logger.LogError("Tentativa {Tentativa} de {Maximo} falhou para {Tipo} {Id} na fila {Fila}: {Erro}. Enviada para {Dlq}.",
                tentativa, fila.MaximoDeTentativas, evento?.Tipo, evento?.Id, fila.Nome, ex.Message.TrimEnd('.'), fila.FilaDeDeadLetter);
            await canal.BasicRejectAsync(entrega.DeliveryTag, requeue: false, ct);
        }
    }

    /// <summary>Publica uma cópia na fila de retry (pela exchange padrão), com o número da próxima tentativa.</summary>
    private async Task AgendarNovaTentativaAsync(IChannel canal, BasicDeliverEventArgs entrega, int proxima, CancellationToken ct)
    {
        var propriedades = new BasicProperties(entrega.BasicProperties)
        {
            Headers = new Dictionary<string, object?>(entrega.BasicProperties.Headers ?? new Dictionary<string, object?>())
            {
                [CabecalhoTentativa] = proxima
            }
        };
        await canal.BasicPublishAsync("", fila.FilaDeRetry, mandatory: false, propriedades, entrega.Body, ct);
    }

    private static int LerTentativa(IReadOnlyBasicProperties propriedades)
    {
        if (propriedades.Headers?.TryGetValue(CabecalhoTentativa, out var valor) != true)
        {
            return 1;
        }

        return valor switch
        {
            int i => i,
            long l => (int)l,
            byte[] texto when int.TryParse(System.Text.Encoding.UTF8.GetString(texto), out var i) => i,
            _ => 1
        };
    }

    private static EventoRecebido Ler(ReadOnlyMemory<byte> corpo, int tentativa)
    {
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            var raiz = documento.RootElement;
            return new EventoRecebido
            {
                Id = raiz.GetProperty("id").GetGuid(),
                Tipo = raiz.GetProperty("tipo").GetString() ?? throw new MensagemInvalida("Evento sem tipo."),
                Versao = raiz.GetProperty("versao").GetInt32(),
                OcorridoEm = raiz.GetProperty("ocorridoEm").GetDateTimeOffset(),
                Dados = raiz.GetProperty("dados").Clone(),
                Tentativa = tentativa
            };
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            throw new MensagemInvalida($"O corpo não segue o envelope do contrato de eventos ({ex.Message}).");
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        if (_canal is not null)
        {
            await _canal.DisposeAsync();
        }
    }
}
