using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace PagUai.Extrato.Tests;

/// <summary>Faz o papel do monólito nas respostas HTTP, sem subir banco nem monólito.</summary>
public class MonolitoFalso : HttpMessageHandler
{
    public static readonly Guid ContaDaAna = Guid.Parse("2a981875-052f-4076-abee-bf4e0b1a7abd");

    public bool ForaDoAr { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (ForaDoAr)
        {
            throw new HttpRequestException("Connection refused (monólito fora do ar)");
        }

        var caminho = request.RequestUri!.AbsolutePath;
        var consulta = request.RequestUri.Query;
        if (!caminho.Contains(ContaDaAna.ToString()))
        {
            return Responder(HttpStatusCode.NotFound, """{"title":"Recurso não encontrado","status":404}""");
        }

        if (caminho.StartsWith("/contas/"))
        {
            return Responder(HttpStatusCode.OK, $$"""{"id":"{{ContaDaAna}}","titular":"Ana Clara Mendes","chavePix":"ana.mendes@paguai.local","criadaEm":"2026-11-01T10:00:00Z"}""");
        }

        if (caminho.StartsWith("/saldos/"))
        {
            return Responder(HttpStatusCode.OK, $$"""{"contaId":"{{ContaDaAna}}","saldo":9950.00,"consultadoEm":"2026-11-21T03:00:00Z"}""");
        }

        if (consulta.Contains("cursor=lixo"))
        {
            return Responder(HttpStatusCode.UnprocessableEntity, """{"title":"Regra de negócio violada","status":422,"detail":"Cursor inválido."}""");
        }

        return Responder(HttpStatusCode.OK, $$"""
            {"contaId":"{{ContaDaAna}}","itens":[
              {"id":"6d76008f-bafc-43d4-8139-1f47643cf890","data":"2026-11-21T02:58:03Z","tipo":"Debito","valor":50.00,"descricao":"PIX para Loja do Seu Geraldo"},
              {"id":"7e86008f-bafc-43d4-8139-1f47643cf891","data":"2026-11-20T02:58:01Z","tipo":"Credito","valor":10000.00,"descricao":"Depósito inicial"}],
             "proximoCursor":"NjM5MzA4"}
            """);
    }

    private static Task<HttpResponseMessage> Responder(HttpStatusCode status, string json) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
}

public class ExtratoFactory : WebApplicationFactory<Program>
{
    public MonolitoFalso Monolito { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(s => s.AddHttpClient<ClienteDoMonolito>().ConfigurePrimaryHttpMessageHandler(() => Monolito));
}
