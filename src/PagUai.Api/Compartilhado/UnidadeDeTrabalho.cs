using System.Data.Common;
using Npgsql;

namespace PagUai.Api.Compartilhado;

/// <summary>
/// Uma conexão e, quando preciso, uma transação por requisição, compartilhadas por todos os módulos.
///
/// Sprint 1: cada módulo tem o seu DbContext e o seu schema, mas todos ainda estão no MESMO banco.
/// Por isso uma operação que passa por vários módulos (o PIX passa por Pagamentos, Contas e
/// Antifraude) ainda pode acontecer numa transação só. Isso deixa de ser possível quando cada
/// serviço tiver o seu próprio banco (Sprint 2 em diante) e é um dos custos da distribuição.
///
/// Sprint 3: ações "depois de confirmar". Um evento para OUTROS SERVIÇOS só pode sair depois do
/// commit: publicado antes, ele anunciaria um PIX que ainda pode ser desfeito (por exemplo, se o
/// pagamento de uma cobrança falhar depois de a transferência ter sido feita).
/// </summary>
public sealed class UnidadeDeTrabalho(IConfiguration configuracao) : IAsyncDisposable
{
    private readonly List<Func<Task>> _aposConfirmar = [];

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
        T resultado;
        try
        {
            resultado = await acao();
            await Transacao.CommitAsync(ct);
        }
        catch
        {
            await Transacao.RollbackAsync(CancellationToken.None);
            _aposConfirmar.Clear(); // desfeito no banco: nada a anunciar
            throw;
        }
        finally
        {
            await Transacao.DisposeAsync();
            Transacao = null;
        }

        // Só chega aqui depois do commit. Entre o commit e estas ações existe uma janela: se o processo
        // cair agora, o banco mudou e ninguém fica sabendo (o problema do "dual write").
        var acoes = _aposConfirmar.ToList();
        _aposConfirmar.Clear();
        foreach (var acaoPosterior in acoes)
        {
            await acaoPosterior();
        }

        return resultado;
    }

    /// <summary>Agenda uma ação para depois do commit da transação em andamento (a mais externa, se houver várias).</summary>
    public void AposConfirmar(Func<Task> acao)
    {
        if (Transacao is null)
        {
            throw new InvalidOperationException("Não há transação em andamento. Chame dentro de ExecutarAsync.");
        }

        _aposConfirmar.Add(acao);
    }

    public async Task ExecutarAsync(Func<Task> acao, CancellationToken ct) =>
        await ExecutarAsync(async () => { await acao(); return true; }, ct);

    public ValueTask DisposeAsync() => Conexao.DisposeAsync();
}
