using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using PagUai.Testes;

namespace PagUai.Api.Tests;

/// <summary>
/// Sobe a API inteira em memória, apontando para um PostgreSQL e um RabbitMQ de verdade.
/// Por padrão os dois vêm de contêineres (Testcontainers, exige Docker). Para usar servidores já
/// em execução, defina PAGUAI_TEST_DB e PAGUAI_TEST_RABBITMQ (veja tests/Comum).
/// </summary>
public class PagUaiApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string ConexaoDoBanco { get; private set; } = string.Empty;
    public string UrlDoRabbitMq { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        ConexaoDoBanco = await AmbientePostgres.ConexaoAsync("paguai");
        UrlDoRabbitMq = await AmbienteRabbitMq.UrlAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:PagUai", ConexaoDoBanco);
        builder.UseSetting("ConnectionStrings:RabbitMq", UrlDoRabbitMq);
        builder.UseSetting("Antifraude:LatenciaMs", "0");
    }
}
