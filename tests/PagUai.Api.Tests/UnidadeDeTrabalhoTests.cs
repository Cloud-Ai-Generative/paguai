using Microsoft.Extensions.Configuration;
using PagUai.Api.Compartilhado;

namespace PagUai.Api.Tests;

/// <summary>Sprint 3: o evento para outros serviços só pode sair depois do commit, e só se houver commit.</summary>
[Collection(ColecaoDaApi.Nome)]
public class UnidadeDeTrabalhoTests(PagUaiApiFactory factory)
{
    private UnidadeDeTrabalho Nova() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:PagUai"] = factory.ConexaoDoBanco })
            .Build());

    [Fact]
    public async Task Acao_posterior_roda_depois_do_commit_da_transacao_mais_externa()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var unidade = Nova();
        var ordem = new List<string>();

        await unidade.ExecutarAsync(async () =>
        {
            // Transação interna (como Pagamentos dentro do pagamento de uma cobrança): participa da externa.
            await unidade.ExecutarAsync(() =>
            {
                unidade.AposConfirmar(() => { ordem.Add("evento publicado"); return Task.CompletedTask; });
                return Task.CompletedTask;
            }, ct);

            ordem.Add("fim da transação interna");
        }, ct);

        Assert.Equal(["fim da transação interna", "evento publicado"], ordem);
    }

    [Fact]
    public async Task Acao_posterior_nao_roda_se_a_transacao_for_desfeita()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var unidade = Nova();
        var publicou = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => unidade.ExecutarAsync(() =>
        {
            unidade.AposConfirmar(() => { publicou = true; return Task.CompletedTask; });
            throw new InvalidOperationException("falhou depois da transferência");
        }, ct));

        Assert.False(publicou);
    }
}
