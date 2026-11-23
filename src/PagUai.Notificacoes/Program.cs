using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PagUai.Mensageria;
using PagUai.Notificacoes;

// PagUai: fintech FICTÍCIA usada na disciplina "Arquitetura de Microsserviços e APIs".
// Sprint 3: o serviço de NOTIFICAÇÕES. Era um módulo do monólito, chamado dentro da transação do
// PIX: se o e-mail falhava, o PIX era desfeito. Agora é um processo separado (Worker Service), com
// banco próprio, que CONSOME o evento PagamentoAprovado do RabbitMQ. Não tem API HTTP.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<NotificacaoOptions>(builder.Configuration.GetSection("Notificacoes"));
builder.Services.AddDbContext<NotificacoesDbContext>(o =>
    NotificacoesDbContext.Configurar(o, builder.Configuration.GetConnectionString("Notificacoes")));
builder.Services.AddScoped<IEnviadorDeEmail, EnviadorSmtp>();

builder.Services.AddMensageria("paguai-notificacoes");
builder.Services.AddConsumidorDeEventos<AoAprovarPagamento>(AoAprovarPagamento.Fila);

var host = builder.Build();

await Migrar(host.Services);

await host.RunAsync();

static async Task Migrar(IServiceProvider servicos)
{
    await using var escopo = servicos.CreateAsyncScope();
    var db = escopo.ServiceProvider.GetRequiredService<NotificacoesDbContext>();
    var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BancoDeDados");
    for (var tentativa = 1; ; tentativa++)
    {
        try
        {
            await BancoProprio.CriarSeNaoExistirAsync(db.Database.GetConnectionString());
            // Cria antes a tabela de histórico, para o EF não registrar um erro ao procurá-la na primeira vez.
            await db.GetService<IHistoryRepository>().CreateIfNotExistsAsync();
            await db.Database.MigrateAsync();
            logger.LogInformation("Banco de Notificações pronto.");
            return;
        }
        catch (Exception ex) when (tentativa < 10)
        {
            logger.LogWarning("Banco ainda indisponível (tentativa {Tentativa}/10): {Erro}", tentativa, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Necessário para os testes.
public partial class Program;
