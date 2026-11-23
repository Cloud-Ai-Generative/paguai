using Npgsql;
using Testcontainers.PostgreSql;

namespace PagUai.Testes;

/// <summary>
/// PostgreSQL para os testes. Por padrão sobe um contêiner (Testcontainers, exige Docker), um só
/// para todo o projeto de testes. Se a variável PAGUAI_TEST_DB estiver definida, usa esse servidor.
/// Cada chamada pode pedir um database diferente; ele é criado se ainda não existir.
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

        await Trava.WaitAsync(); // testes em paralelo pedindo o mesmo database
        try
        {
            await CriarSeNaoExistirAsync(baseDaConexao, database);
        }
        finally
        {
            Trava.Release();
        }

        return new NpgsqlConnectionStringBuilder(baseDaConexao) { Database = database }.ConnectionString;
    }

    private static async Task CriarSeNaoExistirAsync(string baseDaConexao, string database)
    {
        await using var conexao = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(baseDaConexao) { Database = "postgres" }.ConnectionString);
        await conexao.OpenAsync();
        await using var existe = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @nome", conexao);
        existe.Parameters.AddWithValue("nome", database);
        if (await existe.ExecuteScalarAsync() is null)
        {
            try
            {
                await using var criar = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", conexao);
                await criar.ExecuteNonQueryAsync();
            }
            catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.DuplicateDatabase or PostgresErrorCodes.UniqueViolation)
            {
                // outro processo de testes criou ao mesmo tempo
            }
        }
    }
}
