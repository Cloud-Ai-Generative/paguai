using Yarp.ReverseProxy.Configuration;

namespace PagUai.Gateway;

/// <summary>
/// As rotas do gateway, escritas em código para ficarem fáceis de ler.
///
/// STRANGLER FIG: o gateway fica na frente de tudo. Rota por rota, o tráfego passa do monólito
/// para os serviços novos. Nesta sprint, só a rota do extrato pode ir para o serviço novo, e a
/// configuração Strangler:Extrato decide para onde ela vai:
///   "servico"  → serviço de Extrato (padrão)
///   "monolito" → Extrato antigo, ainda dentro do monólito (o "voltar atrás", sem mudar código)
/// Todo o resto continua indo para o monólito.
/// </summary>
public static class Rotas
{
    public const string Monolito = "monolito";
    public const string Extrato = "extrato";

    public static string DestinoDoExtrato(IConfiguration configuracao)
    {
        var valor = (configuracao["Strangler:Extrato"] ?? "servico").Trim().ToLowerInvariant();
        return valor switch
        {
            "servico" => Extrato,
            "monolito" => Monolito,
            _ => throw new InvalidOperationException(
                $"Strangler:Extrato = \"{valor}\" não é válido. Use \"servico\" ou \"monolito\".")
        };
    }

    public static IReadOnlyList<RouteConfig> Criar(string destinoDoExtrato) =>
    [
        new RouteConfig
        {
            RouteId = "extrato",
            ClusterId = destinoDoExtrato,
            Order = 1,
            Match = new RouteMatch { Path = "/contas/{contaId}/extrato", Methods = ["GET"] }
        },
        new RouteConfig
        {
            RouteId = "todo-o-resto",
            ClusterId = Monolito,
            Order = 100,
            Match = new RouteMatch { Path = "{**caminho}" }
        }
    ];

    public static IReadOnlyList<ClusterConfig> Destinos(IConfiguration configuracao) =>
    [
        new ClusterConfig
        {
            ClusterId = Monolito,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["monolito"] = new() { Address = configuracao["Destinos:Monolito"] ?? "http://localhost:8081" }
            }
        },
        new ClusterConfig
        {
            ClusterId = Extrato,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["extrato"] = new() { Address = configuracao["Destinos:Extrato"] ?? "http://localhost:8082" }
            }
        }
    ];
}
