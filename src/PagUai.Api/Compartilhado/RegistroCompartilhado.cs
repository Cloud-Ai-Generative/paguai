namespace PagUai.Api.Compartilhado;

public static class RegistroCompartilhado
{
    public static IServiceCollection AddCompartilhado(this IServiceCollection services)
    {
        services.AddScoped<UnidadeDeTrabalho>();
        services.AddScoped<PublicadorDeEventos>();
        services.AddProblemDetails();
        services.AddExceptionHandler<TratadorDeErros>();
        return services;
    }
}
