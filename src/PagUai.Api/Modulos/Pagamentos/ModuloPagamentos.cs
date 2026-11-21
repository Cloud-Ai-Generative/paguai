using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Contas.Contratos;
using PagUai.Api.Modulos.Pagamentos.Contratos;
using PagUai.Api.Modulos.Pagamentos.Interno;

namespace PagUai.Api.Modulos.Pagamentos;

/// <summary>
/// Contexto de Pagamentos (subdomínio central da PagUai): transferências PIX, saldo e lançamentos.
/// Na Sprint 0 este módulo se chamava Transferências.
/// </summary>
public static class ModuloPagamentos
{
    public static IServiceCollection AddModuloPagamentos(this IServiceCollection services)
    {
        services.AddContextoDoModulo<PagamentosDbContext>(PagamentosDbContext.NomeSchema);
        services.AddScoped<IPagamentos, ServicoDePagamentos>();
        services.AddScoped<IManipulador<ContaAberta>, AoAbrirConta>();
        return services;
    }

    public static void MapModuloPagamentos(this IEndpointRouteBuilder app) => app.MapPagamentos();

    public static Task MigrarAsync(IServiceProvider servicos) => BancoDeDados.MigrarAsync<PagamentosDbContext>(servicos);
}
