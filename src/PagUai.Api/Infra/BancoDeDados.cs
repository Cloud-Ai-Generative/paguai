using Microsoft.EntityFrameworkCore;
using PagUai.Api.Modulos.Contas;

namespace PagUai.Api.Infra;

public static class BancoDeDados
{
    /// <summary>
    /// Aplica as migrations (com algumas tentativas, porque no Docker o PostgreSQL pode demorar a ficar pronto)
    /// e insere os dados fictícios iniciais.
    /// </summary>
    public static async Task PrepararAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PagUaiDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BancoDeDados");

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await db.Database.MigrateAsync(ct);
                break;
            }
            catch (Exception ex) when (tentativa < 10)
            {
                logger.LogWarning("Banco ainda indisponível (tentativa {Tentativa}/10): {Erro}", tentativa, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        if (await db.Contas.AnyAsync(ct))
        {
            return;
        }

        // Dados FICTÍCIOS. Nomes, documentos e chaves não pertencem a pessoas ou empresas reais.
        db.Contas.AddRange(
            new Conta { Titular = "Loja do Seu Geraldo", Documento = "00000000000101", ChavePix = "loja.geraldo@paguai.local", Saldo = 1500m },
            new Conta { Titular = "Ana Clara Mendes", Documento = "00000000101", ChavePix = "ana.mendes@paguai.local", Saldo = 10000m },
            new Conta { Titular = "Bruno Tavares", Documento = "00000000202", ChavePix = "bruno.tavares@paguai.local", Saldo = 800m });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dados fictícios iniciais inseridos.");
    }
}
