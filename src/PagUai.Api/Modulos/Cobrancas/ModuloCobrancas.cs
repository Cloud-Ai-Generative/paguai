using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Cobrancas.Interno;

namespace PagUai.Api.Modulos.Cobrancas;

/// <summary>Contexto de Cobranças: o lojista cobra e o cliente paga.</summary>
public static class ModuloCobrancas
{
    public static IServiceCollection AddModuloCobrancas(this IServiceCollection services)
    {
        services.AddContextoDoModulo<CobrancasDbContext>(CobrancasDbContext.NomeSchema);
        return services;
    }

    public static void MapModuloCobrancas(this IEndpointRouteBuilder app) => app.MapCobrancas();

    public static Task MigrarAsync(IServiceProvider servicos) => BancoDeDados.MigrarAsync<CobrancasDbContext>(servicos);
}
