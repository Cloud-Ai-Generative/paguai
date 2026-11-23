using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Antifraude;
using PagUai.Api.Modulos.Cobrancas;
using PagUai.Api.Modulos.Contas;
using PagUai.Api.Modulos.Pagamentos;
using Scalar.AspNetCore;

// PagUai: fintech FICTÍCIA usada na disciplina "Arquitetura de Microsserviços e APIs".
// Sprint 3: o monólito não manda mais e-mail nem monta extrato. Ele PUBLICA eventos (ContaAberta,
// PagamentoAprovado) no RabbitMQ, e os serviços de Notificações e de Extrato consomem. O módulo de
// Notificações virou o serviço PagUai.Notificacoes, e o Extrato antigo foi apagado.
//
// Sprint 2: o monólito modular agora fica ATRÁS de um gateway (PagUai.Gateway), e a rota do
// extrato pode ir para o primeiro serviço extraído (PagUai.Extrato). Padrão Strangler Fig.
//
// Sprint 1: um MONÓLITO MODULAR. Ainda um processo, um deploy e um banco, mas agora cada módulo
// tem um contrato público, um DbContext e um schema só seus. Os testes de arquitetura garantem
// que nenhum módulo usa o interior de outro.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCompartilhado();
builder.Services.AddModuloContas();
builder.Services.AddModuloPagamentos();
builder.Services.AddModuloCobrancas();
builder.Services.AddModuloAntifraude(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

// Sprint 2: cada serviço se identifica na resposta, para você ver quem atendeu pelo gateway.
app.Use(async (contexto, proximo) =>
{
    contexto.Response.Headers["X-PagUai-Servico"] = "monolito";
    await proximo();
});

app.MapOpenApi();
app.MapScalarApiReference(o => o.WithTitle("PagUai API (monólito modular)"));
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapGet("/health", async (UnidadeDeTrabalho unidade, CancellationToken ct) =>
    {
        try
        {
            await unidade.Conexao.OpenAsync(ct);
            return Results.Ok(new { status = "saudavel", banco = "ok" });
        }
        catch (Exception)
        {
            return Results.Json(new { status = "degradado", banco = "indisponivel" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    })
    .WithTags("Infra")
    .WithSummary("Verifica se a API e o banco estão no ar");

app.MapModuloContas();
app.MapModuloPagamentos();
app.MapModuloCobrancas();

if (!app.Configuration.GetValue<bool>("PularPreparacaoDoBanco"))
{
    // Cada módulo cria e atualiza o SEU schema.
    await ModuloContas.MigrarAsync(app.Services);
    await ModuloPagamentos.MigrarAsync(app.Services);
    await ModuloCobrancas.MigrarAsync(app.Services);
    await ModuloAntifraude.MigrarAsync(app.Services);
    await ModuloContas.InserirDadosIniciaisAsync(app.Services);
}

app.Run();

// Necessário para os testes de integração (WebApplicationFactory).
public partial class Program;
