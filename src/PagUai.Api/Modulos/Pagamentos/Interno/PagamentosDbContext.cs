using Microsoft.EntityFrameworkCore;
using PagUai.Api.Compartilhado;

namespace PagUai.Api.Modulos.Pagamentos.Interno;

public class PagamentosDbContext(DbContextOptions<PagamentosDbContext> options, UnidadeDeTrabalho unidade)
    : ContextoDoModulo(options, unidade)
{
    public const string NomeSchema = "pagamentos";
    public override string Schema => NomeSchema;

    public DbSet<Saldo> Saldos => Set<Saldo>();
    public DbSet<Transferencia> Transferencias => Set<Transferencia>();
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Saldo>(e =>
        {
            e.ToTable("saldos");
            e.HasKey(s => s.ContaId);
            e.Property(s => s.ContaId).ValueGeneratedNever();
            e.Property(s => s.Valor).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Transferencia>(e =>
        {
            e.ToTable("transferencias");
            e.Property(t => t.ChavePixDestino).HasMaxLength(120).IsRequired();
            e.Property(t => t.Valor).HasPrecision(18, 2);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(t => new { t.ContaOrigemId, t.CriadaEm });
        });

        modelBuilder.Entity<Lancamento>(e =>
        {
            e.ToTable("lancamentos");
            e.Property(l => l.Valor).HasPrecision(18, 2);
            e.Property(l => l.Tipo).HasConversion<string>().HasMaxLength(10);
            e.Property(l => l.Descricao).HasMaxLength(200);
            e.HasIndex(l => new { l.ContaId, l.Data });
        });
    }
}
