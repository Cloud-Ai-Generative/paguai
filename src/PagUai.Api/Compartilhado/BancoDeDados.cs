using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PagUai.Api.Compartilhado;

public static class BancoDeDados
{
    /// <summary>
    /// Aplica as migrations de um módulo, com algumas tentativas, porque no Docker o PostgreSQL pode
    /// demorar a ficar pronto. Cada módulo guarda o histórico das suas migrations no próprio schema.
    /// </summary>
    public static async Task MigrarAsync<TContexto>(IServiceProvider servicos, CancellationToken ct = default)
        where TContexto : ContextoDoModulo
    {
        await using var escopo = servicos.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<TContexto>();
        var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BancoDeDados");

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                // Cria antes a tabela de histórico do schema, para o EF não registrar um erro
                // ao procurá-la na primeira execução.
                await contexto.GetService<IHistoryRepository>().CreateIfNotExistsAsync(ct);
                await contexto.Database.MigrateAsync(ct);
                logger.LogInformation("Schema {Schema} pronto.", contexto.Schema);
                return;
            }
            catch (Exception ex) when (tentativa < 10)
            {
                logger.LogWarning("Banco ainda indisponível (tentativa {Tentativa}/10): {Erro}", tentativa, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    /// <summary>Opções comuns dos contextos: a conexão da requisição e o histórico de migrations no schema do módulo.</summary>
    public static IServiceCollection AddContextoDoModulo<TContexto>(this IServiceCollection services, string schema)
        where TContexto : ContextoDoModulo =>
        services.AddDbContext<TContexto>((sp, o) =>
            o.UseNpgsql(sp.GetRequiredService<UnidadeDeTrabalho>().Conexao,
                    npgsql => npgsql.MigrationsHistoryTable("__historico_migrations", schema))
             .UseSnakeCaseNamingConvention());
}
