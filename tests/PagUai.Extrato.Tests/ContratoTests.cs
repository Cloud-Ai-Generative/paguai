using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace PagUai.Extrato.Tests;

/// <summary>
/// API FIRST: o contrato (contratos/extrato.openapi.yaml) é a fonte da verdade.
/// Estes testes falham se o serviço implementado divergir dele: rota a mais ou a menos,
/// código de resposta fora do contrato, tipo de conteúdo errado ou JSON fora do schema.
/// </summary>
public class ContratoTests(ExtratoFactory factory) : IClassFixture<ExtratoFactory>
{
    private const string Rota = "/contas/{contaId}/extrato";
    private static readonly string[] RotasDeInfraestrutura = ["/", "/health", "/contratos/extrato.openapi.yaml"];

    private readonly HttpClient _http = factory.CreateClient();

    [Fact]
    public void Exemplos_do_contrato_seguem_os_proprios_schemas()
    {
        Assert.Empty(Contrato.Validar("Extrato", Contrato.Exemplo("GET", Rota, 200)));
        foreach (var status in new[] { 404, 422, 503 })
        {
            Assert.Empty(Contrato.Validar("Problema", Contrato.Exemplo("GET", Rota, status)));
        }
    }

    [Fact]
    public void Servico_implementa_exatamente_as_operacoes_do_contrato()
    {
        var implementadas = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => !e.RoutePattern.RawText!.StartsWith("/scalar"))
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(m => $"{m} {SemRestricoes(e.RoutePattern.RawText!)}"))
            .Where(o => !RotasDeInfraestrutura.Contains(o.Split(' ')[1]))
            .ToHashSet();

        Assert.Equal(Contrato.Operacoes().Order(), implementadas.Order());
    }

    [Fact]
    public async Task Resposta_200_segue_o_contrato()
    {
        var resposta = await _http.GetAsync($"/contas/{MonolitoFalso.ContaDaAna}/extrato?limite=2", TestContext.Current.CancellationToken);

        await AssertSegueOContrato(resposta, 200, "Extrato");
    }

    [Fact]
    public async Task Conta_inexistente_responde_404_do_contrato() =>
        await AssertSegueOContrato(await _http.GetAsync($"/contas/{Guid.NewGuid()}/extrato", TestContext.Current.CancellationToken), 404, "Problema");

    [Theory]
    [InlineData("de=2026-01-01T00:00:00Z&ate=2026-11-01T00:00:00Z", "periodo-invalido")]
    [InlineData("de=2026-11-10T00:00:00Z&ate=2026-11-01T00:00:00Z", "periodo-invalido")]
    [InlineData("limite=0", "parametro-invalido")]
    [InlineData("limite=500", "parametro-invalido")]
    [InlineData("cursor=lixo", "cursor-invalido")]
    public async Task Parametros_invalidos_respondem_422_do_contrato(string consulta, string tipo)
    {
        var resposta = await _http.GetAsync($"/contas/{MonolitoFalso.ContaDaAna}/extrato?{consulta}", TestContext.Current.CancellationToken);

        var corpo = await AssertSegueOContrato(resposta, 422, "Problema");
        Assert.Equal($"https://paguai.local/problemas/{tipo}", corpo!["type"]!.GetValue<string>());
    }

    /// <summary>Falha parcial: o Extrato está no ar, mas o monólito não responde.</summary>
    [Fact]
    public async Task Monolito_fora_do_ar_responde_503_do_contrato()
    {
        var fabrica = new ExtratoFactory();
        fabrica.Monolito.ForaDoAr = true;
        using var http = fabrica.CreateClient();

        var resposta = await http.GetAsync($"/contas/{MonolitoFalso.ContaDaAna}/extrato", TestContext.Current.CancellationToken);

        await AssertSegueOContrato(resposta, 503, "Problema");
        await fabrica.DisposeAsync();
    }

    private static async Task<JsonNode?> AssertSegueOContrato(HttpResponseMessage resposta, int status, string schema)
    {
        Assert.Equal((HttpStatusCode)status, resposta.StatusCode);
        Assert.Contains(status, Contrato.Respostas("GET", Rota));
        Assert.Equal(Contrato.TipoDeConteudo("GET", Rota, status), resposta.Content.Headers.ContentType?.MediaType);

        var corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var erros = Contrato.Validar(schema, corpo);
        Assert.True(erros.Count == 0, $"A resposta {status} não segue o schema {schema} do contrato:\n{string.Join("\n", erros)}\n{corpo}");
        return corpo;
    }

    private static string SemRestricoes(string rota) =>
        System.Text.RegularExpressions.Regex.Replace(rota, @"\{(\w+)(:[^}]*)?\}", "{$1}");
}
