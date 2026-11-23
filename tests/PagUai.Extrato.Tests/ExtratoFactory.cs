using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PagUai.Testes;

namespace PagUai.Extrato.Tests;

/// <summary>
/// Sobe o serviço de Extrato com um banco de verdade (PostgreSQL; veja tests/Comum) e sem RabbitMQ.
/// O modelo de leitura é preenchido direto no banco, como se os eventos já tivessem chegado.
/// </summary>
public class ExtratoFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly Guid ContaDaAna = Guid.Parse("2a981875-052f-4076-abee-bf4e0b1a7abd");

    /// <summary>Para simular falhas: uma conexão que não funciona. O banco não é preparado.</summary>
    public string? ConexaoForcada { get; init; }

    private string _conexao = string.Empty;

    public async ValueTask InitializeAsync()
    {
        if (ConexaoForcada is not null)
        {
            _conexao = ConexaoForcada;
            return;
        }

        _conexao = await AmbientePostgres.ConexaoAsync("extrato_testes");

        // Sobe o serviço (que aplica as migrations) e grava a conta da Ana, se ainda não existir.
        await using var escopo = Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<ExtratoDbContext>();
        if (await db.Contas.FindAsync(ContaDaAna) is null)
        {
            var agora = DateTime.UtcNow;
            db.Contas.Add(new ContaDoExtrato { Id = ContaDaAna, Titular = "Ana Clara Mendes", Saldo = 9900m, SaldoEm = agora.AddMinutes(-1) });
            db.Lancamentos.AddRange(
                new LancamentoDoExtrato { ContaId = ContaDaAna, Data = agora.AddDays(-1), Tipo = "Credito", Valor = 10000m, Descricao = "Depósito inicial" },
                new LancamentoDoExtrato { ContaId = ContaDaAna, Data = agora.AddMinutes(-2), Tipo = "Debito", Valor = 50m, Descricao = "PIX para Loja do Seu Geraldo" },
                new LancamentoDoExtrato { ContaId = ContaDaAna, Data = agora.AddMinutes(-1), Tipo = "Debito", Valor = 50m, Descricao = "PIX para Loja do Seu Geraldo" });
            await db.SaveChangesAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Extrato", _conexao);
        builder.UseSetting("ConnectionStrings:RabbitMq", "");
        if (ConexaoForcada is not null)
        {
            builder.UseSetting("PularPreparacaoDoBanco", "true");
        }
    }
}
