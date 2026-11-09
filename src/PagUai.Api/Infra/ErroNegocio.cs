using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PagUai.Api.Infra;

/// <summary>
/// Erro de regra de negócio, convertido em Problem Details (RFC 9457).
/// </summary>
public class ErroNegocio(int status, string titulo, string detalhe) : Exception(detalhe)
{
    public int Status { get; } = status;
    public string Titulo { get; } = titulo;

    public static ErroNegocio NaoEncontrado(string recurso) =>
        new(StatusCodes.Status404NotFound, "Recurso não encontrado", $"{recurso} não encontrado(a).");

    public static ErroNegocio Invalido(string detalhe) =>
        new(StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", detalhe);
}

public class TratadorDeErros(IProblemDetailsService problemDetails, ILogger<TratadorDeErros> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        ProblemDetails problema;
        if (exception is ErroNegocio erro)
        {
            problema = new ProblemDetails { Status = erro.Status, Title = erro.Titulo, Detail = erro.Message };
        }
        else
        {
            logger.LogError(exception, "Erro inesperado ao processar {Metodo} {Caminho}", http.Request.Method, http.Request.Path);
            problema = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro interno",
                Detail = exception.Message
            };
        }

        http.Response.StatusCode = problema.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problema,
            Exception = exception
        });
    }
}
