using PagUai.Api.Modulos.Antifraude;

namespace PagUai.Api.Tests;

/// <summary>Testes de unidade: rodam sem banco e sem Docker.</summary>
public class RegrasAntifraudeTests
{
    [Fact]
    public void Aprova_transferencia_comum()
    {
        var r = RegrasAntifraude.Avaliar(valor: 150m, transferenciasNoUltimoMinuto: 0, limiteValor: 5000m, limiteTransferenciasPorMinuto: 4);
        Assert.Equal(DecisaoAntifraude.Aprovada, r.Decisao);
    }

    [Fact]
    public void Reprova_valor_acima_do_limite()
    {
        var r = RegrasAntifraude.Avaliar(6000m, 0, 5000m, 4);
        Assert.Equal(DecisaoAntifraude.Reprovada, r.Decisao);
        Assert.Contains("limite", r.Motivo);
    }

    [Fact]
    public void Reprova_muitas_transferencias_em_um_minuto()
    {
        var r = RegrasAntifraude.Avaliar(10m, 4, 5000m, 4);
        Assert.Equal(DecisaoAntifraude.Reprovada, r.Decisao);
    }

    [Fact]
    public void Valor_exatamente_no_limite_e_aprovado()
    {
        var r = RegrasAntifraude.Avaliar(5000m, 0, 5000m, 4);
        Assert.Equal(DecisaoAntifraude.Aprovada, r.Decisao);
    }
}
