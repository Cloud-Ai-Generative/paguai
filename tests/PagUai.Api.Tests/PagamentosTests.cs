using System.Net;
using System.Net.Http.Json;
using PagUai.Api.Modulos.Cobrancas.Interno;
using PagUai.Api.Modulos.Contas.Interno;
using PagUai.Api.Modulos.Pagamentos.Interno;

namespace PagUai.Api.Tests;

/// <summary>Testes de integração: API + PostgreSQL de verdade, passando por vários módulos.</summary>
[Collection(ColecaoDaApi.Nome)]
public class PagamentosTests(PagUaiApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();

    private static string Unico() => Guid.NewGuid().ToString("N")[..10];

    private async Task<ContaResponse> AbrirContaAsync(HttpClient http, decimal depositoInicial)
    {
        var id = Unico();
        var resp = await http.PostAsJsonAsync("/contas", new AbrirContaRequest($"Cliente {id}", id, $"{id}@teste.local", depositoInicial), TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ContaResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<decimal> SaldoAsync(Guid contaId) =>
        (await _http.GetFromJsonAsync<SaldoResponse>($"/saldos/{contaId}", TestContext.Current.CancellationToken))!.Saldo;

    [Fact]
    public async Task Abrir_conta_cria_o_saldo_em_Pagamentos()
    {
        var conta = await AbrirContaAsync(_http, 250m);

        Assert.Equal(250m, await SaldoAsync(conta.Id));
    }

    [Fact]
    public async Task Transferencia_movimenta_saldos()
    {
        var ct = TestContext.Current.CancellationToken;
        var origem = await AbrirContaAsync(_http, 500m);
        var destino = await AbrirContaAsync(_http, 0m);

        var resp = await _http.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 120m), ct);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        Assert.Equal(380m, await SaldoAsync(origem.Id));
        Assert.Equal(120m, await SaldoAsync(destino.Id));
    }

    [Fact]
    public async Task Saldo_insuficiente_retorna_422()
    {
        var ct = TestContext.Current.CancellationToken;
        var origem = await AbrirContaAsync(_http, 10m);
        var destino = await AbrirContaAsync(_http, 0m);

        var resp = await _http.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 50m), ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task Antifraude_reprova_valor_alto_sem_mover_saldo()
    {
        var ct = TestContext.Current.CancellationToken;
        var origem = await AbrirContaAsync(_http, 10000m);
        var destino = await AbrirContaAsync(_http, 0m);

        var resp = await _http.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 6000m), ct);
        var t = await resp.Content.ReadFromJsonAsync<TransferenciaResponse>(ct);

        Assert.Equal("ReprovadaAntifraude", t!.Status);
        Assert.Equal(10000m, await SaldoAsync(origem.Id));
    }

    [Fact]
    public async Task Pagar_cobranca_transfere_e_baixa_a_cobranca()
    {
        var ct = TestContext.Current.CancellationToken;
        var lojista = await AbrirContaAsync(_http, 0m);
        var cliente = await AbrirContaAsync(_http, 100m);
        var criada = await _http.PostAsJsonAsync("/cobrancas", new CriarCobrancaRequest(lojista.Id, 40m, "Queijo canastra"), ct);
        var cobranca = await criada.Content.ReadFromJsonAsync<CobrancaResponse>(ct);

        var resp = await _http.PostAsJsonAsync($"/cobrancas/{cobranca!.Id}/pagar", new PagarCobrancaRequest(cliente.Id), ct);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(40m, await SaldoAsync(lojista.Id));
        Assert.Equal(60m, await SaldoAsync(cliente.Id));
    }

    /// <summary>Movimentos paginados por cursor (criado na Sprint 2 para o Extrato; continua no monólito).</summary>
    [Fact]
    public async Task Movimentos_sao_paginados_por_cursor_sem_repetir_itens()
    {
        var ct = TestContext.Current.CancellationToken;
        var origem = await AbrirContaAsync(_http, 1000m);
        var destino = await AbrirContaAsync(_http, 0m);
        foreach (var valor in new[] { 10m, 20m, 30m })
        {
            (await _http.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, valor), ct)).EnsureSuccessStatusCode();
        }

        var de = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"));
        var ate = Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("O"));
        var url = $"/pagamentos/contas/{origem.Id}/movimentos?de={de}&ate={ate}&limite=2";
        var pagina1 = await _http.GetFromJsonAsync<PaginaDeMovimentosResponse>(url, ct);
        var pagina2 = await _http.GetFromJsonAsync<PaginaDeMovimentosResponse>($"{url}&cursor={pagina1!.ProximoCursor}", ct);

        Assert.Equal(2, pagina1.Itens.Count);
        Assert.NotNull(pagina1.ProximoCursor);
        Assert.Equal(2, pagina2!.Itens.Count);
        Assert.Null(pagina2.ProximoCursor);
        Assert.Empty(pagina1.Itens.Select(i => i.Id).Intersect(pagina2.Itens.Select(i => i.Id)));
        Assert.Equal(new[] { 30m, 20m, 10m, 1000m }, pagina1.Itens.Concat(pagina2.Itens).Select(i => i.Valor));
    }

    [Fact]
    public async Task Cursor_invalido_responde_422()
    {
        var conta = await AbrirContaAsync(_http, 10m);
        var resp = await _http.GetAsync($"/pagamentos/contas/{conta.Id}/movimentos?de=2026-01-01T00:00:00Z&ate=2030-01-01T00:00:00Z&cursor=lixo", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }
}
