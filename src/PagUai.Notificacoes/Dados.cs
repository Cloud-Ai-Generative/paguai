using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PagUai.Notificacoes;

/// <summary>Um e-mail enviado. Guardado para consulta e auditoria.</summary>
public class Notificacao
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>O evento que gerou esta notificação.</summary>
    public Guid EventoId { get; set; }
    public Guid ContaId { get; set; }
    public string Destinatario { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public DateTime EnviadaEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// IDEMPOTÊNCIA: cada evento tratado fica registrado aqui. Se o mesmo evento chegar de novo
/// (o RabbitMQ entrega "pelo menos uma vez"), o serviço percebe e não manda o e-mail duas vezes.
/// </summary>
public class EventoProcessado
{
    public Guid EventoId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateTime ProcessadoEm { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// O banco de Notificações: um database só dele (notificacoes), no mesmo servidor PostgreSQL do
/// laboratório. Nenhum outro serviço acessa estas tabelas.
/// </summary>
public class NotificacoesDbContext(DbContextOptions<NotificacoesDbContext> options) : DbContext(options)
{
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<EventoProcessado> EventosProcessados => Set<EventoProcessado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notificacao>(e =>
        {
            e.ToTable("notificacoes");
            e.Property(n => n.Destinatario).HasMaxLength(200);
            e.Property(n => n.Mensagem).HasMaxLength(500);
            e.HasIndex(n => n.EventoId);
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
public class FabricaParaMigrations : IDesignTimeDbContextFactory<NotificacoesDbContext>
{
    public NotificacoesDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<NotificacoesDbContext>();
        NotificacoesDbContext.Configurar(opcoes, "Host=localhost;Database=notificacoes");
        return new NotificacoesDbContext(opcoes.Options);
    }
}
