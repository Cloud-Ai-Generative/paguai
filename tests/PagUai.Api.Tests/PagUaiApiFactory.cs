using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace PagUai.Api.Tests;

/// <summary>
/// Sobe a API inteira em memória, apontando para um PostgreSQL de verdade.
/// Por padrão o banco vem de um contêiner (Testcontainers, exige Docker).
/// Se a variável PAGUAI_TEST_DB estiver definida, usa essa connection string no lugar do contêiner.
/// </summary>
public class PagUaiApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string _connectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var externa = Environment.GetEnvironmentVariable("PAGUAI_TEST_DB");
        if (!string.IsNullOrWhiteSpace(externa))
        {
            _connectionString = externa;
            return;
        }

        _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:PagUai", _connectionString);
        builder.UseSetting("Notificacoes:SmtpHost", "");
        builder.UseSetting("Antifraude:LatenciaMs", "0");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
