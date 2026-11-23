using PagUai.Mensageria;

namespace PagUai.Api.Compartilhado;

public static class RegistroCompartilhado
{
    public static IServiceCollection AddCompartilhado(this IServiceCollection services)
    {
        services.AddScoped<UnidadeDeTrabalho>();
        services.AddScoped<PublicadorDeEventos>();
        services.AddMensageria("paguai-api (monolito)");
        services.AddScoped<EventosDeIntegracao>();
        services.AddProblemDetails();
        services.AddExceptionHandler<TratadorDeErros>();
        return services;
    }
}
