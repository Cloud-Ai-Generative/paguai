using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.RepresentationModel;

namespace PagUai.Extrato.Tests;

/// <summary>Lê contratos/extrato.openapi.yaml e valida JSON contra os schemas dele.</summary>
public static class Contrato
{
    private static readonly Lazy<JsonObject> Documento = new(Carregar);

    public static JsonObject Raiz => Documento.Value;

    public static string Caminho
    {
        get
        {
            var pasta = new DirectoryInfo(AppContext.BaseDirectory);
            while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "contratos", "extrato.openapi.yaml")))
            {
                pasta = pasta.Parent;
            }

            return pasta is null
                ? throw new FileNotFoundException("contratos/extrato.openapi.yaml não encontrado.")
                : Path.Combine(pasta.FullName, "contratos", "extrato.openapi.yaml");
        }
    }

    /// <summary>Operações do contrato, como "GET /contas/{contaId}/extrato".</summary>
    public static IEnumerable<string> Operacoes() =>
        Raiz["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject()
            .Where(m => m.Key is "get" or "post" or "put" or "patch" or "delete")
            .Select(m => $"{m.Key.ToUpperInvariant()} {p.Key}"));

    /// <summary>Códigos de resposta previstos para uma operação.</summary>
    public static IEnumerable<int> Respostas(string metodo, string caminho) =>
        Raiz["paths"]![caminho]![metodo.ToLowerInvariant()]!["responses"]!.AsObject().Select(r => int.Parse(r.Key, CultureInfo.InvariantCulture));

    public static string TipoDeConteudo(string metodo, string caminho, int status) =>
        Raiz["paths"]![caminho]![metodo.ToLowerInvariant()]!["responses"]![status.ToString(CultureInfo.InvariantCulture)]!["content"]!.AsObject().Single().Key;

    public static JsonNode? Exemplo(string metodo, string caminho, int status) =>
        Raiz["paths"]![caminho]![metodo.ToLowerInvariant()]!["responses"]![status.ToString(CultureInfo.InvariantCulture)]!["content"]!.AsObject().Single().Value!["example"];

    /// <summary>Valida um JSON contra um schema de components/schemas. Devolve os erros, ou lista vazia.</summary>
    public static IReadOnlyList<string> Validar(string nomeDoSchema, JsonNode? json)
    {
        var defs = JsonNode.Parse(Raiz["components"]!["schemas"]!.ToJsonString().Replace("#/components/schemas/", "#/$defs/"))!;
        var texto = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$ref"] = $"#/$defs/{nomeDoSchema}",
            ["$defs"] = defs
        }.ToJsonString();

        var schema = JsonSchema.FromText(texto, new BuildOptions { SchemaRegistry = new SchemaRegistry() },
            new Uri($"https://paguai.local/contratos/{Guid.NewGuid():N}"));
        using var documento = JsonDocument.Parse(json?.ToJsonString() ?? "null");
        var resultado = schema.Evaluate(documento.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
        return resultado.IsValid
            ? []
            : (resultado.Details ?? []).Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")).ToList();
    }

    private static JsonObject Carregar()
    {
        var yaml = new YamlStream();
        using (var leitor = new StreamReader(Caminho))
        {
            yaml.Load(leitor);
        }

        return (JsonObject)ParaJson(yaml.Documents[0].RootNode)!;
    }

    private static JsonNode? ParaJson(YamlNode no) => no switch
    {
        YamlMappingNode mapa => new JsonObject(mapa.Children.Select(c =>
            KeyValuePair.Create(((YamlScalarNode)c.Key).Value!, ParaJson(c.Value)))),
        YamlSequenceNode lista => new JsonArray(lista.Children.Select(ParaJson).ToArray()),
        YamlScalarNode escalar => Escalar(escalar),
        _ => null
    };

    private static JsonNode? Escalar(YamlScalarNode escalar)
    {
        var valor = escalar.Value ?? string.Empty;
        if (escalar.Style != YamlDotNet.Core.ScalarStyle.Plain)
        {
            return JsonValue.Create(valor);
        }

        if (valor is "null" or "~" or "")
        {
            return null;
        }

        if (valor is "true" or "false")
        {
            return JsonValue.Create(valor == "true");
        }

        if (decimal.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero) && !valor.Contains(':'))
        {
            return JsonValue.Create(numero);
        }

        return JsonValue.Create(valor);
    }
}
