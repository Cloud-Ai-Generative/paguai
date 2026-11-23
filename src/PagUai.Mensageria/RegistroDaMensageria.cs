using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace PagUai.Mensageria;

public static class RegistroDaMensageria
{
    /// <summary>Conexão e publicador. A conexão vem de ConnectionStrings:RabbitMq.</summary>
    public static IServiceCollection AddMensageria(this IServiceCollection services, string nomeDoCliente)
    {
        services.TryAddSingleton(sp =>
        {
            var conexao = ActivatorUtilities.CreateInstance<ConexaoRabbitMq>(sp);
            conexao.NomeDoCliente = nomeDoCliente;
            return conexao;
        });
        services.TryAddSingleton<PublicadorNoBroker>();
        return services;
    }

    /// <summary>Um consumidor (BackgroundService) para a fila, chamando <typeparamref name="TManipulador"/> a cada mensagem.</summary>
    public static IServiceCollection AddConsumidorDeEventos<TManipulador>(this IServiceCollection services, ConfiguracaoDaFila fila)
        where TManipulador : class, IManipuladorDeEventos
    {
        services.AddScoped<TManipulador>();
        services.AddSingleton(sp => ActivatorUtilities.CreateInstance<ConsumidorDeEventos<TManipulador>>(sp, fila));
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ConsumidorDeEventos<TManipulador>>());
        return services;
    }
}
