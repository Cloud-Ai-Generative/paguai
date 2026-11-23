using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace PagUai.Mensageria;

/// <summary>
/// Uma conexão com o RabbitMQ por processo (conexões são caras; canais são baratos).
/// A conexão é aberta na primeira vez que alguém precisa dela e se recupera sozinha se cair
/// (recuperação automática do cliente oficial, que também refaz filas e consumidores).
/// </summary>
public sealed class ConexaoRabbitMq(IConfiguration configuracao, ILogger<ConexaoRabbitMq> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private IConnection? _conexao;

    /// <summary>Nome que aparece na aba Connections da interface do RabbitMQ.</summary>
    public string NomeDoCliente { get; set; } = AppDomain.CurrentDomain.FriendlyName;

    public async Task<IConnection> ObterAsync(CancellationToken ct)
    {
        if (_conexao is { IsOpen: true })
        {
            return _conexao;
        }

        await _trava.WaitAsync(ct);
        try
        {
            if (_conexao is { IsOpen: true })
            {
                return _conexao;
            }

            var url = configuracao.GetConnectionString("RabbitMq")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:RabbitMq (por exemplo, amqp://paguai:senha@localhost:5672).");
            var fabrica = new ConnectionFactory
            {
                Uri = new Uri(url),
                ClientProvidedName = NomeDoCliente,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };

            if (_conexao is not null)
            {
                await _conexao.DisposeAsync();
            }

            _conexao = await fabrica.CreateConnectionAsync(ct);
            logger.LogInformation("Conectado ao RabbitMQ em {Host}:{Porta} como {Cliente}.", fabrica.HostName, fabrica.Port, NomeDoCliente);
            return _conexao;
        }
        finally
        {
            _trava.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexao is not null)
        {
            await _conexao.DisposeAsync();
        }

        _trava.Dispose();
    }
}
