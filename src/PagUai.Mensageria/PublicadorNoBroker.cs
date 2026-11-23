using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace PagUai.Mensageria;

/// <summary>
/// Publica eventos na exchange <see cref="Topologia.Exchange"/>.
///
/// Duas garantias pedidas ao RabbitMQ em cada publicação:
///  - mensagem PERSISTENTE: sobrevive a uma reinicialização do broker;
///  - CONFIRMAÇÃO do publicador (publisher confirms): o método só termina depois que o broker
///    confirma que guardou a mensagem. Se nenhuma fila assinar a chave (mandatory), dá erro.
/// O que ele NÃO garante: que a publicação aconteça se o processo cair logo depois do commit no
/// banco. Esse é o problema do "dual write", tratado com o padrão Outbox (veja o ADR 0005).
/// </summary>
public sealed class PublicadorNoBroker(ConexaoRabbitMq conexao, ILogger<PublicadorNoBroker> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _trava = new(1, 1); // um canal não deve ser usado por duas threads ao mesmo tempo
    private IChannel? _canal;

    public async Task<Guid> PublicarAsync<TDados>(string tipo, int versao, string chave, TDados dados, CancellationToken ct)
    {
        var envelope = new Envelope<TDados>(Guid.NewGuid(), tipo, versao, DateTimeOffset.UtcNow, dados);
        var corpo = JsonSerializer.SerializeToUtf8Bytes(envelope, Json.Opcoes);
        var propriedades = new BasicProperties
        {
            MessageId = envelope.Id.ToString(),
            Type = tipo,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(envelope.OcorridoEm.ToUnixTimeSeconds())
        };

        await _trava.WaitAsync(ct);
        try
        {
            var canal = await CanalAsync(ct);
            await canal.BasicPublishAsync(Topologia.Exchange, chave, mandatory: true, propriedades, corpo, ct);
        }
        finally
        {
            _trava.Release();
        }

        logger.LogInformation("Evento {Tipo} {Id} publicado em {Exchange} com a chave {Chave}.", tipo, envelope.Id, Topologia.Exchange, chave);
        return envelope.Id;
    }

    private async Task<IChannel> CanalAsync(CancellationToken ct)
    {
        if (_canal is { IsOpen: true })
        {
            return _canal;
        }

        if (_canal is not null)
        {
            await _canal.DisposeAsync();
        }

        var conexaoAberta = await conexao.ObterAsync(ct);
        _canal = await conexaoAberta.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await Topologia.DeclararExchangeAsync(_canal, ct);
        return _canal;
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null)
        {
            await _canal.DisposeAsync();
        }

        _trava.Dispose();
    }
}
