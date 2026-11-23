using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PagUai.Testes;
using RabbitMQ.Client;

namespace PagUai.Mensageria.Tests;

/// <summary>O que acontece com uma mensagem quando o consumidor acerta, falha uma vez, falha sempre ou recebe lixo.</summary>
public class ConsumidorDeEventosTests : IAsyncLifetime
{
    private readonly string _chave = $"teste.{Guid.NewGuid():N}";
    private readonly Roteiro _roteiro = new();
    private ConfiguracaoDaFila _fila = null!;
    private IHost _host = null!;
    private IConnection _conexao = null!;
    private IChannel _canal = null!;

    public async ValueTask InitializeAsync()
    {
        var url = await AmbienteRabbitMq.UrlAsync();
        _fila = new ConfiguracaoDaFila($"testes-{Guid.NewGuid():N}", [_chave], MaximoDeTentativas: 3, EsperaEntreTentativas: TimeSpan.FromMilliseconds(300));

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:RabbitMq"] = url;
        builder.Services.AddSingleton(_roteiro);
        builder.Services.AddMensageria("testes");
        builder.Services.AddConsumidorDeEventos<ManipuladorDeTeste>(_fila);
        _host = builder.Build();
        await _host.StartAsync();
        await _host.Services.GetRequiredService<ConsumidorDeEventos<ManipuladorDeTeste>>().Pronto.WaitAsync(TimeSpan.FromSeconds(30));

        _conexao = await new ConnectionFactory { Uri = new Uri(url) }.CreateConnectionAsync();
        _canal = await _conexao.CreateChannelAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        foreach (var fila in new[] { _fila.Nome, _fila.FilaDeRetry, _fila.FilaDeDeadLetter })
        {
            await _canal.QueueDeleteAsync(fila);
        }

        await _canal.DisposeAsync();
        await _conexao.DisposeAsync();
    }

    private Task PublicarAsync() =>
        _host.Services.GetRequiredService<PublicadorNoBroker>().PublicarAsync("AlgoAconteceu", 1, _chave, new { valor = 42 }, CancellationToken.None);

    /// <summary>Espera uma mensagem aparecer na dead letter. Null se não aparecer a tempo.</summary>
    private async Task<BasicGetResult?> DeadLetterAsync(TimeSpan limite)
    {
        var fim = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fim)
        {
            var mensagem = await _canal.BasicGetAsync(_fila.FilaDeDeadLetter, autoAck: true);
            if (mensagem is not null)
            {
                return mensagem;
            }

            await Task.Delay(100);
        }

        return null;
    }

    [Fact]
    public async Task Sucesso_confirma_e_a_mensagem_sai_da_fila()
    {
        await PublicarAsync();

        await _roteiro.EsperarAsync(1);
        Assert.Equal([1], _roteiro.Tentativas);
        Assert.Null(await DeadLetterAsync(TimeSpan.FromSeconds(1)));
        Assert.Equal(0u, await _canal.MessageCountAsync(_fila.Nome, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Falha_temporaria_tenta_de_novo_e_conclui()
    {
        _roteiro.FalharAteATentativa = 1;

        await PublicarAsync();

        await _roteiro.EsperarAsync(2);
        Assert.Equal([1, 2], _roteiro.Tentativas);
        Assert.Null(await DeadLetterAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Falha_permanente_vai_para_a_dead_letter_depois_das_tentativas()
    {
        _roteiro.FalharAteATentativa = int.MaxValue;

        await PublicarAsync();

        var morta = await DeadLetterAsync(TimeSpan.FromSeconds(15));
        Assert.NotNull(morta);
        Assert.Equal([1, 2, 3], _roteiro.Tentativas);
        Assert.Equal(3, Convert.ToInt32(morta.BasicProperties.Headers![ConsumidorDeEventos<ManipuladorDeTeste>.CabecalhoTentativa]));
    }

    [Fact]
    public async Task Mensagem_invalida_vai_direto_para_a_dead_letter()
    {
        await _canal.BasicPublishAsync(Topologia.Exchange, _chave, Encoding.UTF8.GetBytes("isto não é um envelope"), TestContext.Current.CancellationToken);

        Assert.NotNull(await DeadLetterAsync(TimeSpan.FromSeconds(10)));
        Assert.Empty(_roteiro.Tentativas);
    }

    [Fact]
    public async Task Publicar_numa_chave_que_nenhuma_fila_assina_da_erro()
    {
        var publicador = _host.Services.GetRequiredService<PublicadorNoBroker>();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            publicador.PublicarAsync("NinguemOuve", 1, $"ninguem.{Guid.NewGuid():N}", new { }, CancellationToken.None));
    }
}

/// <summary>Diz ao manipulador de teste quando falhar e guarda as tentativas vistas.</summary>
public class Roteiro
{
    private readonly ConcurrentQueue<int> _tentativas = new();

    public int FalharAteATentativa { get; set; }
    public int[] Tentativas => _tentativas.ToArray();

    public void Registrar(int tentativa) => _tentativas.Enqueue(tentativa);

    public async Task EsperarAsync(int quantidade)
    {
        var fim = DateTime.UtcNow.AddSeconds(15);
        while (_tentativas.Count < quantidade && DateTime.UtcNow < fim)
        {
            await Task.Delay(50);
        }

        await Task.Delay(300); // tempo para o ack chegar ao broker
    }
}

public class ManipuladorDeTeste(Roteiro roteiro) : IManipuladorDeEventos
{
    public Task TratarAsync(EventoRecebido evento, CancellationToken ct)
    {
        roteiro.Registrar(evento.Tentativa);
        if (evento.Tentativa <= roteiro.FalharAteATentativa)
        {
            throw new InvalidOperationException($"Falha simulada na tentativa {evento.Tentativa}.");
        }

        Assert.Equal(42, evento.Dados.GetProperty("valor").GetInt32());
        return Task.CompletedTask;
    }
}
