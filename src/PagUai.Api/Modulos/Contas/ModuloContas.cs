using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Contas.Contratos;
using PagUai.Api.Modulos.Contas.Interno;

namespace PagUai.Api.Modulos.Contas;

/// <summary>Contexto de Contas: cadastro de clientes e lojistas e chaves PIX.</summary>
public static class ModuloContas
{
    public static IServiceCollection AddModuloContas(this IServiceCollection services)
    {
        services.AddContextoDoModulo<ContasDbContext>(ContasDbContext.NomeSchema);
        services.AddScoped<ServicoDeContas>();
        services.AddScoped<IContas>(sp => sp.GetRequiredService<ServicoDeContas>());
        return services;
    }

    public static void MapModuloContas(this IEndpointRouteBuilder app) => app.MapContas();

    public static Task MigrarAsync(IServiceProvider servicos) => BancoDeDados.MigrarAsync<ContasDbContext>(servicos);

    /// <summary>Dados FICTÍCIOS. Nomes, documentos e chaves não pertencem a pessoas ou empresas reais.</summary>
    public static async Task InserirDadosIniciaisAsync(IServiceProvider servicos, CancellationToken ct = default)
    {
        await using var escopo = servicos.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<ContasDbContext>();
        if (db.Contas.Any())
        {
            return;
        }

        var contas = escopo.ServiceProvider.GetRequiredService<ServicoDeContas>();
        await contas.AbrirAsync("Loja do Seu Geraldo", "00000000000101", "loja.geraldo@paguai.local", 1500m, ct);
        await contas.AbrirAsync("Ana Clara Mendes", "00000000101", "ana.mendes@paguai.local", 10000m, ct);
        await contas.AbrirAsync("Bruno Tavares", "00000000202", "bruno.tavares@paguai.local", 800m, ct);
        escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BancoDeDados")
            .LogInformation("Dados fictícios iniciais inseridos.");
    }
}
