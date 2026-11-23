namespace PagUai.Api.Modulos.Contas.Contratos;

// ── MENSAGENS que Contas publica para OUTROS SERVIÇOS (pelo RabbitMQ) ──────────────
// O formato oficial está em contratos/eventos.asyncapi.yaml. Mudou aqui, mude lá (e o contrário).

/// <summary>Dados do evento ContaAberta (chave conta.aberta, versão 1).</summary>
public record DadosDeContaAberta(Guid ContaId, string Titular, string ChavePix, decimal DepositoInicial, DateTime AbertaEm);

public static class MensagensDeContas
{
    public const string ContaAberta = "ContaAberta";
    public const string ChaveContaAberta = "conta.aberta";
}
