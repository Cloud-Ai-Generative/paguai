using Microsoft.EntityFrameworkCore;
using PagUai.Api.Infra;

namespace PagUai.Api.Modulos.Antifraude;

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
/// Antifraude chamado EM PROCESSO pelo módulo de Transferências.
/// </summary>
public class AntifraudeService(PagUaiDbContext db, Microsoft.Extensions.Options.IOptions<AntifraudeOptions> options, ILogger<AntifraudeService> logger)
{
    private readonly AntifraudeOptions _opcoes = options.Value;

    public async Task<AnaliseAntifraude> AnalisarAsync(Guid transferenciaId, Guid contaOrigemId, decimal valor, CancellationToken ct)
    {
        if (_opcoes.LatenciaMs > 0)
        {
            await Task.Delay(_opcoes.LatenciaMs, ct);
        }

        var umMinutoAtras = DateTime.UtcNow.AddMinutes(-1);
        var recentes = await db.Transferencias
            .CountAsync(t => t.ContaOrigemId == contaOrigemId && t.CriadaEm >= umMinutoAtras, ct);

        var resultado = RegrasAntifraude.Avaliar(valor, recentes, _opcoes.LimiteValor, _opcoes.LimiteTransferenciasPorMinuto);

        var analise = new AnaliseAntifraude
        {
            TransferenciaId = transferenciaId,
            Score = resultado.Score,
            Decisao = resultado.Decisao,
            Motivo = resultado.Motivo
        };
        db.AnalisesAntifraude.Add(analise);

        logger.LogInformation("Antifraude: transferência {TransferenciaId} {Decisao} (score {Score})",
            transferenciaId, analise.Decisao, analise.Score);
        return analise;
    }
}
