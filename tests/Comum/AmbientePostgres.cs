using Npgsql;
using Testcontainers.PostgreSql;

namespace PagUai.Testes;

/// <summary>
/// PostgreSQL para os testes. Por padrão sobe um contêiner (Testcontainers, exige Docker), um só
/// para todo o projeto de testes. Se a variável PAGUAI_TEST_DB estiver definida, usa esse servidor.
/// Cada chamada pode pedir um database diferente; o EF Core cria o database na migration.
/// </summary>
public static class AmbientePostgres
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static PostgreSqlContainer? _conteiner;

    public static async Task<string> ConexaoAsync(string database)
    {
        var baseDaConexao = Environment.GetEnvironmentVariable("PAGUAI_TEST_DB");
        if (string.IsNullOrWhiteSpace(baseDaConexao))
        {
            await Trava.WaitAsync();
            try
            {
                if (_conteiner is null)
                {
                    var conteiner = new PostgreSqlBuilder("postgres:17-alpine").Build();
                    await conteiner.StartAsync();
                    _conteiner = conteiner;
                }
            }
            finally
            {
                Trava.Release();
            }

            baseDaConexao = _conteiner.GetConnectionString();
        }

        return new NpgsqlConnectionStringBuilder(baseDaConexao) { Database = database }.ConnectionString;
    }
}
