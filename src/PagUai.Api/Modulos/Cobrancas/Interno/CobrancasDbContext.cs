using Microsoft.EntityFrameworkCore;
using PagUai.Api.Compartilhado;

namespace PagUai.Api.Modulos.Cobrancas.Interno;

public class CobrancasDbContext(DbContextOptions<CobrancasDbContext> options, UnidadeDeTrabalho unidade)
    : ContextoDoModulo(options, unidade)
{
    public const string NomeSchema = "cobrancas";
    public override string Schema => NomeSchema;

    public DbSet<Cobranca> Cobrancas => Set<Cobranca>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Cobranca>(e =>
        {
            e.ToTable("cobrancas");
            e.Property(c => c.Descricao).HasMaxLength(200).IsRequired();
            e.Property(c => c.Valor).HasPrecision(18, 2);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
