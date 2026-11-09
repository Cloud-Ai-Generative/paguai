using Microsoft.EntityFrameworkCore;
using PagUai.Api.Infra;
using PagUai.Api.Modulos.Antifraude;
using PagUai.Api.Modulos.Cobrancas;
using PagUai.Api.Modulos.Contas;
using PagUai.Api.Modulos.Extrato;
using PagUai.Api.Modulos.Notificacoes;
using PagUai.Api.Modulos.Transferencias;
using Scalar.AspNetCore;

// PagUai: fintech FICTÍCIA usada na disciplina "Arquitetura de Microsserviços e APIs".
// Sprint 0: um MONÓLITO. Um processo, um deploy, um banco.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PagUaiDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("PagUai"))
     .UseSnakeCaseNamingConvention());

builder.Services.Configure<AntifraudeOptions>(builder.Configuration.GetSection("Antifraude"));
builder.Services.Configure<NotificacaoOptions>(builder.Configuration.GetSection("Notificacoes"));

builder.Services.AddScoped<AntifraudeService>();
builder.Services.AddScoped<NotificacaoService>();
builder.Services.AddScoped<TransferenciaService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorDeErros>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference(o => o.WithTitle("PagUai API (monólito)"));
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapGet("/health", async (PagUaiDbContext db, CancellationToken ct) =>
        await db.Database.CanConnectAsync(ct)
            ? Results.Ok(new { status = "saudavel", banco = "ok" })
            : Results.Json(new { status = "degradado", banco = "indisponivel" }, statusCode: StatusCodes.Status503ServiceUnavailable))
    .WithTags("Infra")
    .WithSummary("Verifica se a API e o banco estão no ar");

app.MapContas();
app.MapTransferencias();
app.MapCobrancas();
app.MapExtrato();

if (!app.Configuration.GetValue<bool>("PularPreparacaoDoBanco"))
{
    await BancoDeDados.PrepararAsync(app.Services);
}

app.Run();

// Necessário para os testes de integração (WebApplicationFactory).
public partial class Program;
