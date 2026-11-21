namespace PagUai.Api.Modulos.Pagamentos.Interno;

/// <summary>Saldo de uma conta. Sprint 1: saiu do cadastro de Contas e veio para Pagamentos.</summary>
public class Saldo
{
    public Guid ContaId { get; set; }
    public decimal Valor { get; set; }
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}

public enum StatusTransferencia
{
    Concluida,
    ReprovadaAntifraude
}

/// <summary>Transferência entre contas usando o PIX simulado da PagUai.</summary>
public class Transferencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContaOrigemId { get; set; }
    public Guid ContaDestinoId { get; set; }
    public string ChavePixDestino { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public StatusTransferencia Status { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}

public enum TipoLancamento
{
    Credito,
    Debito
}

/// <summary>Cada movimentação de dinheiro de uma conta. É a fonte do extrato.</summary>
public class Lancamento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContaId { get; set; }
    public TipoLancamento Tipo { get; set; }
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DateTime Data { get; set; } = DateTime.UtcNow;
}
