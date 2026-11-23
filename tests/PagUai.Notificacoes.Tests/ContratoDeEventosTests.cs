using PagUai.Testes;

namespace PagUai.Notificacoes.Tests;

/// <summary>QUEM CONSOME: Notificações só pode ler campos que existem no contrato de eventos (e que são obrigatórios).</summary>
public class ContratoDeEventosTests
{
    [Fact]
    public void Notificacoes_le_so_campos_garantidos_pelo_contrato() =>
        Assert.Subset(ContratoDeEventos.Obrigatorios("PagamentoAprovadoV1").ToHashSet(), ContratoDeEventos.CamposDoTipo(typeof(PagamentoAprovado)).ToHashSet());
}
