using Microsoft.EntityFrameworkCore;
using PagUai.Api.Compartilhado;

namespace PagUai.Api.Modulos.Contas.Interno;

public class ContasDbContext(DbContextOptions<ContasDbContext> options, UnidadeDeTrabalho unidade)
    : ContextoDoModulo(options, unidade)
{
    public const string NomeSchema = "contas";
    public override string Schema => NomeSchema;

    public DbSet<Conta> Contas => Set<Conta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Conta>(e =>
        {
            e.ToTable("contas");
            e.Property(c => c.Titular).HasMaxLength(120).IsRequired();
            e.Property(c => c.Documento).HasMaxLength(20).IsRequired();
            e.Property(c => c.ChavePix).HasMaxLength(120).IsRequired();
            e.HasIndex(c => c.ChavePix).IsUnique();
            e.HasIndex(c => c.Documento).IsUnique();
        });
    }
}
