using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using PagUai.Api.Modulos.Contas;
using PagUai.Api.Modulos.Extrato;
using PagUai.Api.Modulos.Transferencias;

namespace PagUai.Api.Tests;

/// <summary>Testes de integração: API + PostgreSQL de verdade.</summary>
public class TransferenciasTests(PagUaiApiFactory factory) : IClassFixture<PagUaiApiFactory>
{
    private readonly HttpClient _http = factory.CreateClient();

    private static string Unico() => Guid.NewGuid().ToString("N")[..10];

    private async Task<ContaResponse> AbrirContaAsync(HttpClient http, decimal saldo)
    {
        var id = Unico();
        var resp = await http.PostAsJsonAsync("/contas", new AbrirContaRequest($"Cliente {id}", id, $"{id}@teste.local", saldo), TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ContaResponse>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task Transferencia_movimenta_saldos_e_gera_extrato()
    {
        var ct = TestContext.Current.CancellationToken;
        var origem = await AbrirContaAsync(_http, 500m);
        var destino = await AbrirContaAsync(_http, 0m);

        var resp = await _http.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 120m), ct);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
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
        var conta = await _http.GetFromJsonAsync<ContaResponse>($"/contas/{origem.Id}", ct);
        Assert.Equal(10000m, conta!.Saldo);
    }

    /// <summary>
    /// Documenta a DOR da Sprint 3: com a notificação dentro da transação,
    /// uma falha no e-mail desfaz a transferência inteira.
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
        var conta = await _http.GetFromJsonAsync<ContaResponse>($"/contas/{origem.Id}", ct);
        Assert.Equal(300m, conta!.Saldo);
    }
}
