using Microsoft.EntityFrameworkCore;
using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Antifraude.Contratos;
using PagUai.Api.Modulos.Contas.Contratos;
using PagUai.Api.Modulos.Notificacoes.Contratos;
using PagUai.Api.Modulos.Pagamentos.Contratos;

namespace PagUai.Api.Modulos.Pagamentos.Interno;

/// <summary>
/// Executa a transferência usando SÓ os contratos públicos dos outros módulos.
///
/// O que a Sprint 1 resolveu: Pagamentos não grava mais tabelas de outros módulos.
/// O que continua de propósito:
///  1. O Antifraude é chamado em processo e Pagamentos espera a resposta (Sprints 6 e 9).
///  2. A notificação roda DENTRO da transação: se ela falhar, a transferência falha (Sprint 3).
/// </summary>
public class ServicoDePagamentos(
    PagamentosDbContext db,
    UnidadeDeTrabalho unidade,
    IContas contas,
    IAntifraude antifraude,
    INotificacoes notificacoes) : IPagamentos
{
    public Task<TransferenciaRealizada> TransferirAsync(Guid contaOrigemId, string chavePixDestino, decimal valor, string descricao, CancellationToken ct) =>
        unidade.ExecutarAsync(async () =>
        {
            if (valor <= 0)
            {
                throw ErroNegocio.Invalido("O valor da transferência deve ser maior que zero.");
            }

            var origem = await contas.ObterAsync(contaOrigemId, ct) ?? throw ErroNegocio.NaoEncontrado("Conta de origem");
            var destino = await contas.ObterPorChavePixAsync(chavePixDestino, ct) ?? throw ErroNegocio.NaoEncontrado("Chave PIX de destino");

            if (origem.Id == destino.Id)
            {
                throw ErroNegocio.Invalido("Não é possível transferir para a própria conta.");
            }

            var saldoOrigem = await db.Saldos.FindAsync([origem.Id], ct) ?? throw ErroNegocio.NaoEncontrado("Saldo da conta de origem");
            var saldoDestino = await db.Saldos.FindAsync([destino.Id], ct) ?? throw ErroNegocio.NaoEncontrado("Saldo da conta de destino");

            if (saldoOrigem.Valor < valor)
            {
                throw ErroNegocio.Invalido("Saldo insuficiente.");
            }

            var transferencia = new Transferencia
            {
                ContaOrigemId = origem.Id,
                ContaDestinoId = destino.Id,
                ChavePixDestino = chavePixDestino,
                Valor = valor
            };

            // 1. Antifraude em processo (síncrono), pelo contrato público.
            var parecer = await antifraude.AnalisarAsync(transferencia.Id, origem.Id, valor, ct);
            if (!parecer.Aprovado)
            {
                transferencia.Status = StatusTransferencia.ReprovadaAntifraude;
                db.Transferencias.Add(transferencia);
                await db.SaveChangesAsync(ct);
                return ParaContrato(transferencia);
            }

            // 2. Movimenta saldos e lançamentos: são de Pagamentos.
            var agora = DateTime.UtcNow;
            saldoOrigem.Valor -= valor;
            saldoOrigem.AtualizadoEm = agora;
            saldoDestino.Valor += valor;
            saldoDestino.AtualizadoEm = agora;
            transferencia.Status = StatusTransferencia.Concluida;
            db.Transferencias.Add(transferencia);
            db.Lancamentos.Add(new Lancamento { ContaId = origem.Id, Tipo = TipoLancamento.Debito, Valor = valor, Descricao = $"{descricao} para {destino.Titular}" });
            db.Lancamentos.Add(new Lancamento { ContaId = destino.Id, Tipo = TipoLancamento.Credito, Valor = valor, Descricao = $"{descricao} de {origem.Titular}" });
            await db.SaveChangesAsync(ct);

            // 3. Notificação dentro da transação, pelo contrato público.
            await notificacoes.NotificarAsync(origem.Id, origem.Titular, $"Você enviou R$ {valor:N2} para {destino.Titular}.", ct);
            await notificacoes.NotificarAsync(destino.Id, destino.Titular, $"Você recebeu R$ {valor:N2} de {origem.Titular}.", ct);

            return ParaContrato(transferencia);
        }, ct);

    public async Task<decimal?> ObterSaldoAsync(Guid contaId, CancellationToken ct) =>
        await db.Saldos.Where(s => s.ContaId == contaId).Select(s => (decimal?)s.Valor).FirstOrDefaultAsync(ct);

    public async Task<PaginaDeMovimentos> ListarMovimentosAsync(Guid contaId, DateTime de, DateTime ate, int limite, string? cursor, CancellationToken ct)
    {
        var consulta = db.Lancamentos.Where(l => l.ContaId == contaId && l.Data >= de && l.Data <= ate);

        // Paginação por cursor (keyset): continua depois do último item da página anterior.
        if (CursorDeMovimentos.TentarLer(cursor, out var dataCursor, out var idCursor))
        {
            consulta = consulta.Where(l => l.Data < dataCursor || (l.Data == dataCursor && l.Id.CompareTo(idCursor) < 0));
        }
        else if (!string.IsNullOrEmpty(cursor))
        {
            throw ErroNegocio.Invalido("Cursor inválido.");
        }

        var itens = await consulta
            .OrderByDescending(l => l.Data).ThenByDescending(l => l.Id)
            .Take(limite + 1)
            .Select(l => new Movimento(l.Id, l.Data, l.Tipo.ToString(), l.Valor, l.Descricao))
            .ToListAsync(ct);

        var temMais = itens.Count > limite;
        if (temMais)
        {
            itens.RemoveAt(itens.Count - 1);
        }

        return new PaginaDeMovimentos(itens, temMais ? CursorDeMovimentos.Criar(itens[^1].Data, itens[^1].Id) : null);
    }

    private static TransferenciaRealizada ParaContrato(Transferencia t) =>
        new(t.Id, t.ContaOrigemId, t.ContaDestinoId, t.Valor, t.Status == StatusTransferencia.Concluida, t.Status.ToString(), t.CriadaEm);
}
