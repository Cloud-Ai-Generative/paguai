using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Antifraude.Contratos;
using PagUai.Api.Modulos.Antifraude.Interno;

namespace PagUai.Api.Modulos.Antifraude;

/// <summary>Contexto de Antifraude: decide se um pagamento pode seguir.</summary>
public static class ModuloAntifraude
{
    public static IServiceCollection AddModuloAntifraude(this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<AntifraudeOptions>(configuracao.GetSection("Antifraude"));
        services.AddContextoDoModulo<AntifraudeDbContext>(AntifraudeDbContext.NomeSchema);
        services.AddScoped<IAntifraude, ServicoAntifraude>();
        return services;
    }

    public static Task MigrarAsync(IServiceProvider servicos) => BancoDeDados.MigrarAsync<AntifraudeDbContext>(servicos);
}
