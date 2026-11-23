using Yarp.ReverseProxy.Configuration;

namespace PagUai.Gateway;

/// <summary>
/// As rotas do gateway, escritas em código para ficarem fáceis de ler.
///
/// Sprint 3: o Strangler Fig do Extrato terminou. O Extrato antigo foi apagado do monólito, então a
/// rota do extrato vai SEMPRE para o serviço de Extrato, e a configuração Strangler:Extrato deixou de
/// existir. Todo o resto continua indo para o monólito.
///
/// Sprint 2 (para comparar, veja a tag sprint-02): a configuração decidia se o extrato ia para o
/// serviço novo ou para o monólito; era o "voltar atrás" sem mudar código.
/// </summary>
public static class Rotas
{
    public const string Monolito = "monolito";
    public const string Extrato = "extrato";

    public static IReadOnlyList<RouteConfig> Criar() =>
    [
        new RouteConfig
        {
            RouteId = "extrato",
            ClusterId = Extrato,
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
