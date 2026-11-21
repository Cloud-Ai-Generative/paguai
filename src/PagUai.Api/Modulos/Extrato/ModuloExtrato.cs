using PagUai.Api.Modulos.Extrato.Interno;

namespace PagUai.Api.Modulos.Extrato;

/// <summary>Contexto de Extrato: só leitura. É o primeiro candidato a sair do monólito (Sprint 2).</summary>
public static class ModuloExtrato
{
    public static IServiceCollection AddModuloExtrato(this IServiceCollection services) => services;

    public static void MapModuloExtrato(this IEndpointRouteBuilder app) => app.MapExtrato();
}
