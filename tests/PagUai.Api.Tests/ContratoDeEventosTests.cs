using PagUai.Api.Modulos.Contas.Contratos;
using PagUai.Api.Modulos.Pagamentos.Contratos;
using PagUai.Testes;

namespace PagUai.Api.Tests;

/// <summary>QUEM PUBLICA: o monólito precisa mandar exatamente os campos que o contrato de eventos promete.</summary>
public class ContratoDeEventosTests
{
    [Theory]
    [InlineData(typeof(DadosDeContaAberta), "ContaAbertaV1")]
    [InlineData(typeof(DadosDePagamentoAprovado), "PagamentoAprovadoV1")]
    public void Publicador_envia_os_campos_do_contrato(Type tipo, string schema)
    {
        Assert.Equal(ContratoDeEventos.Propriedades(schema).Order(), ContratoDeEventos.CamposDoTipo(tipo).Order());
        Assert.Subset(ContratoDeEventos.CamposDoTipo(tipo).ToHashSet(), ContratoDeEventos.Obrigatorios(schema).ToHashSet());
    }
}
