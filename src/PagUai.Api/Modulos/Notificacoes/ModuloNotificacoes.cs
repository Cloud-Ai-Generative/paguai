using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Notificacoes.Contratos;
using PagUai.Api.Modulos.Notificacoes.Interno;

namespace PagUai.Api.Modulos.Notificacoes;

/// <summary>Contexto de Notificações: avisa clientes e lojistas (e-mail no laboratório).</summary>
public static class ModuloNotificacoes
{
    public static IServiceCollection AddModuloNotificacoes(this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<NotificacaoOptions>(configuracao.GetSection("Notificacoes"));
        services.AddContextoDoModulo<NotificacoesDbContext>(NotificacoesDbContext.NomeSchema);
        services.AddScoped<INotificacoes, ServicoDeNotificacoes>();
        return services;
    }

    public static Task MigrarAsync(IServiceProvider servicos) => BancoDeDados.MigrarAsync<NotificacoesDbContext>(servicos);
}
