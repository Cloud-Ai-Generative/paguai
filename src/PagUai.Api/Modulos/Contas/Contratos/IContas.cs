using PagUai.Api.Compartilhado;

namespace PagUai.Api.Modulos.Contas.Contratos;

// ── CONTRATO PÚBLICO do módulo Contas ──────────────────────────────────────────
// É tudo o que os outros módulos podem usar. O resto (pasta Interno) é privado:
// os testes de arquitetura falham se outro módulo tentar usar algo de lá.

/// <summary>O que os outros módulos precisam saber de uma conta. Note: não há saldo aqui (o saldo é de Pagamentos).</summary>
public record ContaResumo(Guid Id, string Titular, string ChavePix);

public interface IContas
{
    Task<ContaResumo?> ObterAsync(Guid contaId, CancellationToken ct);
    Task<ContaResumo?> ObterPorChavePixAsync(string chavePix, CancellationToken ct);
}

/// <summary>Publicado quando uma conta é aberta. Pagamentos reage criando o saldo com o depósito inicial.</summary>
public record ContaAberta(Guid ContaId, string Titular, decimal DepositoInicial) : IEvento;
