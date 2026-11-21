using Microsoft.EntityFrameworkCore;
using PagUai.Api.Compartilhado;

namespace PagUai.Api.Modulos.Notificacoes.Interno;

public class NotificacoesDbContext(DbContextOptions<NotificacoesDbContext> options, UnidadeDeTrabalho unidade)
    : ContextoDoModulo(options, unidade)
{
    public const string NomeSchema = "notificacoes";
    public override string Schema => NomeSchema;

    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Notificacao>(e =>
        {
            e.ToTable("notificacoes");
            e.Property(n => n.Mensagem).HasMaxLength(500);
        });
    }
}
