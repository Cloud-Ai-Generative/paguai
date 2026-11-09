using Microsoft.EntityFrameworkCore;
using PagUai.Api.Modulos.Antifraude;
using PagUai.Api.Modulos.Cobrancas;
using PagUai.Api.Modulos.Contas;
using PagUai.Api.Modulos.Extrato;
using PagUai.Api.Modulos.Notificacoes;
using PagUai.Api.Modulos.Transferencias;

namespace PagUai.Api.Infra;

/// <summary>
/// UM único DbContext para TODOS os módulos.
/// DOR PROPOSITAL (Sprint 1): qualquer módulo consegue ler e escrever as tabelas de qualquer outro.
/// </summary>
public class PagUaiDbContext(DbContextOptions<PagUaiDbContext> options) : DbContext(options)
{
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Transferencia> Transferencias => Set<Transferencia>();
    public DbSet<Cobranca> Cobrancas => Set<Cobranca>();
    public DbSet<AnaliseAntifraude> AnalisesAntifraude => Set<AnaliseAntifraude>();
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conta>(e =>
        {
            e.ToTable("contas");
            e.Property(c => c.Titular).HasMaxLength(120).IsRequired();
            e.Property(c => c.Documento).HasMaxLength(20).IsRequired();
            e.Property(c => c.ChavePix).HasMaxLength(120).IsRequired();
            e.Property(c => c.Saldo).HasPrecision(18, 2);
            e.HasIndex(c => c.ChavePix).IsUnique();
            e.HasIndex(c => c.Documento).IsUnique();
        });

        modelBuilder.Entity<Transferencia>(e =>
        {
            e.ToTable("transferencias");
            e.Property(t => t.ChavePixDestino).HasMaxLength(120).IsRequired();
            e.Property(t => t.Valor).HasPrecision(18, 2);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(t => new { t.ContaOrigemId, t.CriadaEm });
        });

        modelBuilder.Entity<Cobranca>(e =>
        {
            e.ToTable("cobrancas");
            e.Property(c => c.Descricao).HasMaxLength(200).IsRequired();
            e.Property(c => c.Valor).HasPrecision(18, 2);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<AnaliseAntifraude>(e =>
        {
            e.ToTable("analises_antifraude");
            e.Property(a => a.Decisao).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Motivo).HasMaxLength(200);
        });

        modelBuilder.Entity<Lancamento>(e =>
        {
            e.ToTable("lancamentos");
            e.Property(l => l.Valor).HasPrecision(18, 2);
            e.Property(l => l.Tipo).HasConversion<string>().HasMaxLength(10);
            e.Property(l => l.Descricao).HasMaxLength(200);
            e.HasIndex(l => new { l.ContaId, l.Data });
        });

        modelBuilder.Entity<Notificacao>(e =>
        {
            e.ToTable("notificacoes");
            e.Property(n => n.Mensagem).HasMaxLength(500);
        });
    }
}
