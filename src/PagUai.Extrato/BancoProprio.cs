using Npgsql;

namespace PagUai.Extrato;

/// <summary>
/// Cada serviço tem o SEU database. No laboratório, todos ficam no mesmo servidor PostgreSQL,
/// e o serviço cria o próprio database na primeira vez que sobe.
/// </summary>
public static class BancoProprio
{
    public static async Task CriarSeNaoExistirAsync(string? conexao, CancellationToken ct = default)
    {
        var dados = new NpgsqlConnectionStringBuilder(conexao);
        var nome = dados.Database ?? throw new InvalidOperationException("A connection string não informa o database.");
        dados.Database = "postgres"; // database administrativo, que sempre existe

        await using var conexaoAdmin = new NpgsqlConnection(dados.ConnectionString);
        await conexaoAdmin.OpenAsync(ct);
        await using var existe = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @nome", conexaoAdmin);
        existe.Parameters.AddWithValue("nome", nome);
        if (await existe.ExecuteScalarAsync(ct) is null)
        {
            await using var criar = new NpgsqlCommand($"CREATE DATABASE \"{nome}\"", conexaoAdmin);
            await criar.ExecuteNonQueryAsync(ct);
        }
    }
}
