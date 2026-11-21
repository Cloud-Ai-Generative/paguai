using Microsoft.EntityFrameworkCore;

namespace PagUai.Api.Compartilhado;

/// <summary>
/// Base dos DbContexts dos módulos. Cada módulo tem o SEU contexto, que só enxerga as SUAS tabelas,
/// todas no schema do módulo (por exemplo, <c>pagamentos.saldos</c>).
/// </summary>
public abstract class ContextoDoModulo(DbContextOptions options, UnidadeDeTrabalho unidade) : DbContext(options)
{
    /// <summary>Nome do schema do módulo no PostgreSQL.</summary>
    public abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.HasDefaultSchema(Schema);

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Grava dentro da transação da requisição, se houver uma.
        await Database.UseTransactionAsync(unidade.Transacao, ct);
        return await base.SaveChangesAsync(ct);
    }
}
