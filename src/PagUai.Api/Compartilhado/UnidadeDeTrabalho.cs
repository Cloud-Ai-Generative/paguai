using System.Data.Common;
using Npgsql;

namespace PagUai.Api.Compartilhado;

/// <summary>
/// Uma conexão e, quando preciso, uma transação por requisição, compartilhadas por todos os módulos.
///
/// Sprint 1: cada módulo tem o seu DbContext e o seu schema, mas todos ainda estão no MESMO banco.
/// Por isso uma operação que passa por vários módulos (o PIX passa por Pagamentos, Antifraude e
/// Notificações) ainda pode acontecer numa transação só. Isso deixa de ser possível quando cada
/// serviço tiver o seu próprio banco (Sprint 2 em diante) e é um dos custos da distribuição.
/// </summary>
public sealed class UnidadeDeTrabalho(IConfiguration configuracao) : IAsyncDisposable
{
    public NpgsqlConnection Conexao { get; } = new(configuracao.GetConnectionString("PagUai"));

    /// <summary>Transação em andamento nesta requisição, ou null.</summary>
    public DbTransaction? Transacao { get; private set; }

    /// <summary>
    /// Executa a ação numa transação. Se já existe uma transação aberta (por exemplo, o pagamento de
    /// uma cobrança, que chama Pagamentos), a ação participa dela.
    /// </summary>
    public async Task<T> ExecutarAsync<T>(Func<Task<T>> acao, CancellationToken ct)
    {
        if (Transacao is not null)
        {
            return await acao();
        }

        if (Conexao.State != System.Data.ConnectionState.Open)
        {
            await Conexao.OpenAsync(ct);
        }

        Transacao = await Conexao.BeginTransactionAsync(ct);
        try
        {
            var resultado = await acao();
            await Transacao.CommitAsync(ct);
            return resultado;
        }
        catch
        {
            await Transacao.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await Transacao.DisposeAsync();
            Transacao = null;
        }
    }

    public async Task ExecutarAsync(Func<Task> acao, CancellationToken ct) =>
        await ExecutarAsync(async () => { await acao(); return true; }, ct);

    public ValueTask DisposeAsync() => Conexao.DisposeAsync();
}
