using System.Reflection;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace PagUai.Testes;

/// <summary>
/// Lê contratos/eventos.asyncapi.yaml. Os testes comparam os campos de cada evento no contrato com
/// os tipos C# de quem publica (todos os obrigatórios) e de quem consome (só campos que existem).
/// </summary>
public static class ContratoDeEventos
{
    private static readonly Lazy<YamlMappingNode> Schemas = new(Carregar);

    public static IReadOnlySet<string> Obrigatorios(string schema) =>
        ((YamlSequenceNode)Schema(schema)["required"]).Children.Select(c => ((YamlScalarNode)c).Value!).ToHashSet();

    public static IReadOnlySet<string> Propriedades(string schema) =>
        ((YamlMappingNode)Schema(schema)["properties"]).Children.Keys.Select(c => ((YamlScalarNode)c).Value!).ToHashSet();

    /// <summary>Nomes das propriedades do tipo, como vão no JSON (camelCase).</summary>
    public static IReadOnlySet<string> CamposDoTipo(Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)).ToHashSet();

    private static YamlMappingNode Schema(string nome) => (YamlMappingNode)Schemas.Value[nome];

    private static YamlMappingNode Carregar()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "contratos", "eventos.asyncapi.yaml")))
        {
            pasta = pasta.Parent;
        }

        var caminho = pasta is null
            ? throw new FileNotFoundException("contratos/eventos.asyncapi.yaml não encontrado.")
            : Path.Combine(pasta.FullName, "contratos", "eventos.asyncapi.yaml");
        var yaml = new YamlStream();
        using var leitor = new StreamReader(caminho);
        yaml.Load(leitor);
        var raiz = (YamlMappingNode)yaml.Documents[0].RootNode;
        return (YamlMappingNode)((YamlMappingNode)raiz["components"])["schemas"];
    }
}
