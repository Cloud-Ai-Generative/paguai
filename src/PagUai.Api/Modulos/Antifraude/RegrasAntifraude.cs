namespace PagUai.Api.Modulos.Antifraude;

public record ResultadoAntifraude(DecisaoAntifraude Decisao, int Score, string Motivo);

/// <summary>
/// Regras simples e didáticas de antifraude. Função pura: fácil de testar sem banco de dados.
/// </summary>
public static class RegrasAntifraude
{
    public static ResultadoAntifraude Avaliar(decimal valor, int transferenciasNoUltimoMinuto, decimal limiteValor, int limiteTransferenciasPorMinuto)
    {
        var score = 0;
        if (valor > limiteValor)
        {
            score += 70;
        }

        if (transferenciasNoUltimoMinuto >= limiteTransferenciasPorMinuto)
        {
            score += 50;
        }

        if (score >= 50)
        {
            var motivo = valor > limiteValor
                ? $"Valor acima do limite de {limiteValor:N2}"
                : $"Mais de {limiteTransferenciasPorMinuto - 1} transferências no último minuto";
            return new ResultadoAntifraude(DecisaoAntifraude.Reprovada, score, motivo);
        }

        return new ResultadoAntifraude(DecisaoAntifraude.Aprovada, score, "Sem indícios de fraude");
    }
}
