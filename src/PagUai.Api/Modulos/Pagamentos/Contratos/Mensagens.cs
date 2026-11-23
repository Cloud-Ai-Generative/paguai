namespace PagUai.Api.Modulos.Pagamentos.Contratos;

// ── MENSAGENS que Pagamentos publica para OUTROS SERVIÇOS (pelo RabbitMQ) ──────────
// O formato oficial está em contratos/eventos.asyncapi.yaml. Mudou aqui, mude lá (e o contrário).

/// <summary>
/// Dados do evento PagamentoAprovado (chave pagamento.aprovado, versão 1).
/// Leva o que os consumidores precisam para não terem de perguntar de volta ao monólito
/// (os titulares, por exemplo): é o que mantém Notificações e Extrato independentes dele.
/// </summary>
public record DadosDePagamentoAprovado(
    Guid TransferenciaId,
    Guid ContaOrigemId,
    string TitularOrigem,
    Guid ContaDestinoId,
    string TitularDestino,
    string ChavePixDestino,
    decimal Valor,
    string Descricao,
    DateTime AprovadoEm);

public static class MensagensDePagamentos
{
    public const string PagamentoAprovado = "PagamentoAprovado";
    public const string ChavePagamentoAprovado = "pagamento.aprovado";
}
