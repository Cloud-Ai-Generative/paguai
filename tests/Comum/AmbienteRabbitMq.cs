using Testcontainers.RabbitMq;

namespace PagUai.Testes;

/// <summary>
/// RabbitMQ para os testes. Por padrão sobe um contêiner (Testcontainers, exige Docker), um só para
/// todo o projeto de testes. Se a variável PAGUAI_TEST_RABBITMQ estiver definida (por exemplo,
/// amqp://guest:guest@localhost:5672), usa esse broker.
/// </summary>
public static class AmbienteRabbitMq
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static RabbitMqContainer? _conteiner;

    public static async Task<string> UrlAsync()
    {
        var externo = Environment.GetEnvironmentVariable("PAGUAI_TEST_RABBITMQ");
        if (!string.IsNullOrWhiteSpace(externo))
        {
            return externo;
        }

        await Trava.WaitAsync();
        try
        {
            if (_conteiner is null)
            {
                var conteiner = new RabbitMqBuilder("rabbitmq:4-management").Build();
                await conteiner.StartAsync();
                _conteiner = conteiner;
            }
        }
        finally
        {
            Trava.Release();
        }

        return _conteiner.GetConnectionString();
    }
}
