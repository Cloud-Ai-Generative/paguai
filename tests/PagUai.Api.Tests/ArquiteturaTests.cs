using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetArchTest.Rules;
using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Antifraude.Interno;
using PagUai.Api.Modulos.Cobrancas.Interno;
using PagUai.Api.Modulos.Contas.Interno;
using PagUai.Api.Modulos.Pagamentos.Interno;

namespace PagUai.Api.Tests;

/// <summary>
/// Testes de ARQUITETURA: verificam as fronteiras entre os módulos, sem banco e sem Docker.
///
/// Todos os módulos estão no mesmo projeto, então o compilador deixa qualquer classe usar qualquer
/// outra. Sem uma regra verificada, o acoplamento volta aos poucos. Estes testes são essa regra:
/// um módulo só pode usar a pasta <c>Contratos</c> dos outros, nunca a pasta <c>Interno</c>.
/// </summary>
public class ArquiteturaTests
{
    private static readonly Assembly Api = typeof(Program).Assembly;
    private const string Raiz = "PagUai.Api.Modulos";

    // Sprint 3: Extrato e Notificações saíram do monólito (são serviços próprios).
    private static readonly string[] NomesDosModulos = ["Contas", "Pagamentos", "Cobrancas", "Antifraude"];

    public static TheoryData<string> Modulos => new(NomesDosModulos);

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Modulo_nao_usa_o_interior_de_outro_modulo(string modulo)
    {
        var interioresDosOutros = NomesDosModulos
            .Where(m => m != modulo)
            .Select(m => $"{Raiz}.{m}.Interno")
            .ToArray();

        var resultado = Types.InAssembly(Api)
            .That().ResideInNamespace($"{Raiz}.{modulo}")
            .ShouldNot().HaveDependencyOnAny(interioresDosOutros)
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            $"O módulo {modulo} usa o interior de outro módulo em: {string.Join(", ", resultado.FailingTypeNames ?? [])}. " +
            "Use o contrato público (pasta Contratos) do outro módulo.");
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Contrato_publico_nao_expoe_o_interior_do_proprio_modulo(string modulo)
    {
        var resultado = Types.InAssembly(Api)
            .That().ResideInNamespace($"{Raiz}.{modulo}.Contratos")
            .ShouldNot().HaveDependencyOn($"{Raiz}.{modulo}.Interno")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            $"O contrato de {modulo} expõe tipos internos: {string.Join(", ", resultado.FailingTypeNames ?? [])}.");
    }

    [Fact]
    public void Compartilhado_nao_depende_de_nenhum_modulo()
    {
        var resultado = Types.InAssembly(Api)
            .That().ResideInNamespace("PagUai.Api.Compartilhado")
            .ShouldNot().HaveDependencyOn(Raiz)
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            $"O código compartilhado depende de módulos: {string.Join(", ", resultado.FailingTypeNames ?? [])}.");
    }

    [Fact]
    public void Cada_modulo_tem_as_suas_tabelas_apenas_no_seu_schema()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:PagUai"] = "Host=localhost" })
            .Build();
        var unidade = new UnidadeDeTrabalho(configuracao);

        ContextoDoModulo[] contextos =
        [
            new ContasDbContext(Opcoes<ContasDbContext>(unidade), unidade),
            new PagamentosDbContext(Opcoes<PagamentosDbContext>(unidade), unidade),
            new CobrancasDbContext(Opcoes<CobrancasDbContext>(unidade), unidade),
            new AntifraudeDbContext(Opcoes<AntifraudeDbContext>(unidade), unidade)
        ];

        foreach (var contexto in contextos)
        {
            foreach (var entidade in contexto.Model.GetEntityTypes())
            {
                Assert.Equal(contexto.Schema, entidade.GetSchema());
            }
        }

        var schemas = contextos.Select(c => c.Schema).ToList();
        Assert.Equal(schemas.Count, schemas.Distinct().Count());
    }

    /// <summary>
    /// Sprint 3: os módulos não falam com o RabbitMQ diretamente. Eles usam
    /// <see cref="EventosDeIntegracao"/>, que garante a publicação só depois do commit.
    /// </summary>
    [Fact]
    public void Modulos_nao_usam_o_broker_diretamente()
    {
        var resultado = Types.InAssembly(Api)
            .That().ResideInNamespace(Raiz)
            .ShouldNot().HaveDependencyOnAny("RabbitMQ.Client", "PagUai.Mensageria")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            $"Módulos usando o broker diretamente: {string.Join(", ", resultado.FailingTypeNames ?? [])}. Use EventosDeIntegracao.");
    }

    /// <summary>Sprint 3: e-mail é com o serviço de Notificações. O monólito não pode voltar a enviar.</summary>
    [Fact]
    public void Monolito_nao_envia_email()
    {
        var resultado = Types.InAssembly(Api)
            .ShouldNot().HaveDependencyOnAny("MailKit", "MimeKit", "System.Net.Mail")
            .GetResult();

        Assert.True(resultado.IsSuccessful,
            $"O monólito voltou a enviar e-mail em: {string.Join(", ", resultado.FailingTypeNames ?? [])}.");
    }

    private static DbContextOptions<T> Opcoes<T>(UnidadeDeTrabalho unidade) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql(unidade.Conexao).UseSnakeCaseNamingConvention().Options;
}
