using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using PagUai.Api.Modulos.Cobrancas.Interno;
using PagUai.Api.Modulos.Contas.Interno;
using PagUai.Api.Modulos.Extrato.Interno;
using PagUai.Api.Modulos.Pagamentos.Interno;

namespace PagUai.Api.Tests;

/// <summary>Testes de integração: API + PostgreSQL de verdade, passando por vários módulos.</summary>
public class PagamentosTests(PagUaiApiFactory factory) : IClassFixture<PagUaiApiFactory>
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
    public async Task Transferencia_movimenta_saldos_e_gera_extrato()
    {
        var ct = TestContext.Current.CancellationToken;
        var origem = await AbrirContaAsync(_http, 500m);
        var destino = await AbrirContaAsync(_http, 0m);

        var resp = await _http.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 120m), ct);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        Assert.Equal(380m, await SaldoAsync(origem.Id));
        var extrato = await _http.GetFromJsonAsync<ExtratoResponse>($"/contas/{destino.Id}/extrato", ct);
        Assert.Equal(120m, extrato!.SaldoAtual);
        Assert.Single(extrato.Lancamentos);
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

    /// <summary>
    /// Documenta a DOR da Sprint 3, que a Sprint 1 NÃO resolve: com a notificação dentro da
    /// transação, uma falha no e-mail desfaz a transferência inteira, mesmo com módulos separados.
    /// </summary>
    [Fact]
    public async Task Falha_na_notificacao_desfaz_a_transferencia()
    {
        var ct = TestContext.Current.CancellationToken;
        var origem = await AbrirContaAsync(_http, 300m);
        var destino = await AbrirContaAsync(_http, 0m);

        using var comFalha = factory
            .WithWebHostBuilder(b => b.UseSetting("Notificacoes:SimularFalha", "true"))
            .CreateClient();
        var resp = await comFalha.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 100m), ct);

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        Assert.Equal(300m, await SaldoAsync(origem.Id));
        Assert.Equal(0m, await SaldoAsync(destino.Id));
    }
}
