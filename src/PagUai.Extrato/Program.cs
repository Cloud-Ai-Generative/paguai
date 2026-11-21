using PagUai.Extrato;
using Scalar.AspNetCore;

// PagUai: fintech FICTÍCIA usada na disciplina "Arquitetura de Microsserviços e APIs".
// Sprint 2: o PRIMEIRO serviço extraído do monólito, com o padrão Strangler Fig.
// API FIRST: o contrato (contratos/extrato.openapi.yaml) veio antes deste código.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

// DOR PROPOSITAL (Sprint 9): nenhum timeout configurado aqui. Veja ClienteDoMonolito.
builder.Services.AddHttpClient<ClienteDoMonolito>(http =>
    http.BaseAddress = new Uri(builder.Configuration["Monolito:Url"] ?? "http://localhost:8081"));

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

app.Run();

// Necessário para os testes (WebApplicationFactory).
public partial class Program;
