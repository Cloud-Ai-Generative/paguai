using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PagUai.Api.Modulos.Antifraude.Contratos;

namespace PagUai.Api.Modulos.Antifraude.Interno;

public class AntifraudeOptions
{
    public decimal LimiteValor { get; set; } = 5000m;
    public int LimiteTransferenciasPorMinuto { get; set; } = 4;

    /// <summary>
    /// Latência artificial da análise, em milissegundos.
    /// DOR PROPOSITAL (Sprints 6 e 9): aumente este valor para ver o monólito inteiro ficar lento.
    /// </summary>
    public int LatenciaMs { get; set; } = 300;
}

/// <summary>
/// Antifraude chamado EM PROCESSO pelo módulo de Pagamentos, agora por um contrato público.
/// </summary>
public class ServicoAntifraude(AntifraudeDbContext db, IOptions<AntifraudeOptions> options, ILogger<ServicoAntifraude> logger) : IAntifraude
{
    private readonly AntifraudeOptions _opcoes = options.Value;

    public async Task<ParecerAntifraude> AnalisarAsync(Guid pagamentoId, Guid contaOrigemId, decimal valor, CancellationToken ct)
    {
        if (_opcoes.LatenciaMs > 0)
        {
            await Task.Delay(_opcoes.LatenciaMs, ct);
        }

        var umMinutoAtras = DateTime.UtcNow.AddMinutes(-1);
        var recentes = await db.Analises
            .CountAsync(a => a.ContaOrigemId == contaOrigemId && a.AnalisadaEm >= umMinutoAtras, ct);

        var resultado = RegrasAntifraude.Avaliar(valor, recentes, _opcoes.LimiteValor, _opcoes.LimiteTransferenciasPorMinuto);

        db.Analises.Add(new AnaliseAntifraude
        {
            PagamentoId = pagamentoId,
            ContaOrigemId = contaOrigemId,
            Valor = valor,
            Score = resultado.Score,
            Decisao = resultado.Decisao,
            Motivo = resultado.Motivo
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Antifraude: pagamento {PagamentoId} {Decisao} (score {Score})",
            pagamentoId, resultado.Decisao, resultado.Score);
        return new ParecerAntifraude(resultado.Decisao == DecisaoAntifraude.Aprovada, resultado.Score, resultado.Motivo);
    }
}
