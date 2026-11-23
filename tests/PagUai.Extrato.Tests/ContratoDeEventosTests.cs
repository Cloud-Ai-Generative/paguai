using PagUai.Testes;

namespace PagUai.Extrato.Tests;

/// <summary>QUEM CONSOME: o Extrato só pode ler campos que existem no contrato de eventos (e que são obrigatórios).</summary>
public class ContratoDeEventosTests
{
    [Theory]
    [InlineData(typeof(ContaAberta), "ContaAbertaV1")]
    [InlineData(typeof(PagamentoAprovado), "PagamentoAprovadoV1")]
    public void Extrato_le_so_campos_garantidos_pelo_contrato(Type tipo, string schema) =>
        Assert.Subset(ContratoDeEventos.Obrigatorios(schema).ToHashSet(), ContratoDeEventos.CamposDoTipo(tipo).ToHashSet());
}
