using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PagUai.Extrato;

/// <summary>
/// MODELO DE LEITURA do Extrato (Sprint 3): uma cópia dos dados, no formato que o extrato precisa,
/// montada com os eventos ContaAberta e PagamentoAprovado. Quem manda no saldo continua sendo
/// Pagamentos; esta cópia só serve para consulta e pode estar alguns instantes atrás
/// (consistência eventual).
/// </summary>
public class ContaDoExtrato
{
    public Guid Id { get; set; }
    public string Titular { get; set; } = string.Empty;
    public decimal Saldo { get; set; }

    /// <summary>Momento do último fato aplicado ao saldo (abertura da conta ou último PIX).</summary>
    public DateTime SaldoEm { get; set; }
}

public class LancamentoDoExtrato
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContaId { get; set; }

    /// <summary>O evento que gerou este lançamento.</summary>
    public Guid EventoId { get; set; }
    public DateTime Data { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = string.Empty;
}

/// <summary>IDEMPOTÊNCIA: eventos já aplicados. O mesmo evento duas vezes não duplica lançamentos.</summary>
public class EventoProcessado
{
    public Guid EventoId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateTime ProcessadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>O banco do Extrato: um database só dele (extrato). Nenhum outro serviço acessa estas tabelas.</summary>
public class ExtratoDbContext(DbContextOptions<ExtratoDbContext> options) : DbContext(options)
{
    public DbSet<ContaDoExtrato> Contas => Set<ContaDoExtrato>();
    public DbSet<LancamentoDoExtrato> Lancamentos => Set<LancamentoDoExtrato>();
    public DbSet<EventoProcessado> EventosProcessados => Set<EventoProcessado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContaDoExtrato>(e =>
        {
            e.ToTable("contas");
            e.Property(c => c.Titular).HasMaxLength(200);
            e.Property(c => c.Saldo).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LancamentoDoExtrato>(e =>
        {
            e.ToTable("lancamentos");
            e.Property(l => l.Tipo).HasMaxLength(10);
            e.Property(l => l.Valor).HasPrecision(18, 2);
            e.Property(l => l.Descricao).HasMaxLength(300);
            e.HasIndex(l => new { l.ContaId, l.Data, l.Id }); // a consulta do extrato (e o cursor) usa esta ordem
        });

        modelBuilder.Entity<EventoProcessado>(e =>
        {
            e.ToTable("eventos_processados");
            e.HasKey(p => p.EventoId);
            e.Property(p => p.Tipo).HasMaxLength(100);
        });
    }

    public static void Configurar(DbContextOptionsBuilder opcoes, string? conexao) =>
        opcoes.UseNpgsql(conexao).UseSnakeCaseNamingConvention();
}

/// <summary>Usado só pelo comando dotnet ef (gerar migrations), sem subir o serviço.</summary>
public class FabricaParaMigrations : IDesignTimeDbContextFactory<ExtratoDbContext>
{
    public ExtratoDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<ExtratoDbContext>();
        ExtratoDbContext.Configurar(opcoes, "Host=localhost;Database=extrato");
        return new ExtratoDbContext(opcoes.Options);
    }
}
