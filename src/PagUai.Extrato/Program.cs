using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PagUai.Extrato;
using PagUai.Mensageria;
using Scalar.AspNetCore;

// PagUai: fintech FICTÍCIA usada na disciplina "Arquitetura de Microsserviços e APIs".
// Sprint 3: o Extrato deixa de perguntar ao monólito. Ele tem banco próprio (database extrato) e
// mantém um MODELO DE LEITURA atualizado pelos eventos ContaAberta e PagamentoAprovado, consumidos
// do RabbitMQ. Se o monólito cair, o extrato continua respondendo (com os dados até o último evento).
//
// Sprint 2: o PRIMEIRO serviço extraído do monólito, com o padrão Strangler Fig.
// API FIRST: o contrato (contratos/extrato.openapi.yaml) veio antes deste código.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddDbContext<ExtratoDbContext>(o =>
    ExtratoDbContext.Configurar(o, builder.Configuration.GetConnectionString("Extrato")));

// O consumidor só liga se houver um RabbitMQ configurado (os testes de contrato não precisam dele).
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("RabbitMq")))
{
    builder.Services.AddMensageria("paguai-extrato");
    builder.Services.AddConsumidorDeEventos<AtualizarExtrato>(AtualizarExtrato.Fila);
}
else
{
    builder.Services.AddScoped<AtualizarExtrato>();
}

var app = builder.Build();

app.Use(async (contexto, proximo) =>
{
    contexto.Response.Headers["X-PagUai-Servico"] = "extrato";
    await proximo();
});

// A documentação é o PRÓPRIO contrato, não algo gerado a partir do código.
var caminhoDoContrato = Path.Combine(AppContext.BaseDirectory, "contratos", "extrato.openapi.yaml");
app.MapGet("/contratos/extrato.openapi.yaml", () => Results.File(caminhoDoContrato, "application/yaml"))
    .ExcludeFromDescription();
app.MapScalarApiReference(o => o
    .WithTitle("PagUai · Serviço de Extrato (contract-first)")
    .WithOpenApiRoutePattern("/contratos/extrato.openapi.yaml"));
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new { status = "saudavel", servico = "extrato" })).ExcludeFromDescription();

app.MapExtrato();

if (!app.Configuration.GetValue<bool>("PularPreparacaoDoBanco"))
{
    await Migrar(app.Services);
}

app.Run();

static async Task Migrar(IServiceProvider servicos)
{
    await using var escopo = servicos.CreateAsyncScope();
    var db = escopo.ServiceProvider.GetRequiredService<ExtratoDbContext>();
    var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BancoDeDados");
    for (var tentativa = 1; ; tentativa++)
    {
        try
        {
            await BancoProprio.CriarSeNaoExistirAsync(db.Database.GetConnectionString());
            // Cria antes a tabela de histórico, para o EF não registrar um erro ao procurá-la na primeira vez.
            await db.GetService<IHistoryRepository>().CreateIfNotExistsAsync();
            await db.Database.MigrateAsync();
            logger.LogInformation("Banco do Extrato pronto.");
            return;
        }
        catch (Exception ex) when (tentativa < 10)
        {
            logger.LogWarning("Banco ainda indisponível (tentativa {Tentativa}/10): {Erro}", tentativa, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Necessário para os testes (WebApplicationFactory).
public partial class Program;
