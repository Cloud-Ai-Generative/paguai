using Microsoft.EntityFrameworkCore;
using PagUai.Api.Compartilhado;

namespace PagUai.Api.Modulos.Antifraude.Interno;

public class AntifraudeDbContext(DbContextOptions<AntifraudeDbContext> options, UnidadeDeTrabalho unidade)
    : ContextoDoModulo(options, unidade)
{
    public const string NomeSchema = "antifraude";
    public override string Schema => NomeSchema;

    public DbSet<AnaliseAntifraude> Analises => Set<AnaliseAntifraude>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AnaliseAntifraude>(e =>
        {
            e.ToTable("analises");
            e.Property(a => a.Valor).HasPrecision(18, 2);
            e.Property(a => a.Decisao).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Motivo).HasMaxLength(200);
            e.HasIndex(a => new { a.ContaOrigemId, a.AnalisadaEm });
        });
    }
}
