using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using PagUai.Api.Modulos.Contas.Interno;
using PagUai.Api.Modulos.Pagamentos.Interno;

namespace PagUai.Api.Tests;

/// <summary>Sprint 3: o monólito anuncia o que aconteceu, pelo RabbitMQ, depois do commit.</summary>
[Collection(ColecaoDaApi.Nome)]
public class EventosTests(PagUaiApiFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient();

    private static string Unico() => Guid.NewGuid().ToString("N")[..10];

    private static async Task<ContaResponse> AbrirContaAsync(HttpClient http, decimal depositoInicial)
    {
        var id = Unico();
        var resp = await http.PostAsJsonAsync("/contas", new AbrirContaRequest($"Cliente {id}", id, $"{id}@teste.local", depositoInicial), TestContext.Current.CancellationToken);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ContaResponse>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task Abrir_conta_publica_ContaAberta()
    {
        await using var escuta = await EscutaDeEventos.AbrirAsync(factory.UrlDoRabbitMq);

        var conta = await AbrirContaAsync(_http, 75m);

        var evento = await escuta.EsperarAsync("ContaAberta", d => EscutaDeEventos.Texto(d, "contaId", conta.Id));
        Assert.NotNull(evento);
        Assert.Equal(1, evento["versao"]!.GetValue<int>());
        Assert.Equal(75m, evento["dados"]!["depositoInicial"]!.GetValue<decimal>());
        Assert.Equal(conta.Titular, evento["dados"]!["titular"]!.GetValue<string>());
    }

    [Fact]
    public async Task Transferencia_publica_PagamentoAprovado_com_o_que_os_consumidores_precisam()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var escuta = await EscutaDeEventos.AbrirAsync(factory.UrlDoRabbitMq);
        var origem = await AbrirContaAsync(_http, 500m);
        var destino = await AbrirContaAsync(_http, 0m);

        var resp = await _http.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 120m), ct);
        var transferencia = await resp.Content.ReadFromJsonAsync<TransferenciaResponse>(ct);

        var evento = await escuta.EsperarAsync("PagamentoAprovado", d => EscutaDeEventos.Texto(d, "transferenciaId", transferencia!.Id));
        Assert.NotNull(evento);
        var dados = evento["dados"]!;
        Assert.Equal(origem.Titular, dados["titularOrigem"]!.GetValue<string>());
        Assert.Equal(destino.Titular, dados["titularDestino"]!.GetValue<string>());
        Assert.Equal(120m, dados["valor"]!.GetValue<decimal>());
        Assert.Equal("PIX", dados["descricao"]!.GetValue<string>());
    }

    [Fact]
    public async Task Transferencia_reprovada_pelo_antifraude_nao_publica_PagamentoAprovado()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var escuta = await EscutaDeEventos.AbrirAsync(factory.UrlDoRabbitMq);
        var origem = await AbrirContaAsync(_http, 10000m);
        var destino = await AbrirContaAsync(_http, 0m);

        await _http.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 6000m), ct);

        Assert.Null(await escuta.EsperarAsync("PagamentoAprovado", d => EscutaDeEventos.Texto(d, "contaOrigemId", origem.Id), TimeSpan.FromSeconds(2)));
    }

    /// <summary>
    /// Até a Sprint 2, uma falha no e-mail desfazia o PIX. Agora o PIX não depende de ninguém depois do
    /// commit. Mas fica a DOR do "dual write": com o broker fora do ar, o PIX vale e o evento se perde
    /// (só aparece no log como EVENTO PERDIDO). O padrão Outbox resolve; veja o ADR 0005.
    /// </summary>
    [Fact]
    public async Task Broker_fora_do_ar_nao_desfaz_o_PIX_mas_o_evento_se_perde()
    {
        var ct = TestContext.Current.CancellationToken;
        var origem = await AbrirContaAsync(_http, 300m);
        var destino = await AbrirContaAsync(_http, 0m);

        using var semBroker = factory
            .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:RabbitMq", "amqp://guest:guest@127.0.0.1:1"))
            .CreateClient();
        var resp = await semBroker.PostAsJsonAsync("/transferencias", new TransferirRequest(origem.Id, destino.ChavePix, 100m), ct);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var saldo = await _http.GetFromJsonAsync<SaldoResponse>($"/saldos/{destino.Id}", ct);
        Assert.Equal(100m, saldo!.Saldo);
    }
}
