using Microsoft.EntityFrameworkCore;
using PagUai.Api.Infra;
using PagUai.Api.Modulos.Antifraude;
using PagUai.Api.Modulos.Extrato;
using PagUai.Api.Modulos.Notificacoes;

namespace PagUai.Api.Modulos.Transferencias;

/// <summary>
/// Executa uma transferência do início ao fim, numa ÚNICA transação de banco.
///
/// DORES PROPOSITAIS deste monólito (ver README):
///  1. Chama o Antifraude em processo e espera a resposta (Sprints 6 e 9).
///  2. Grava diretamente as tabelas do Extrato (Sprint 1).
///  3. Envia a notificação DENTRO da transação: se ela falhar, a transferência falha (Sprint 3).
/// </summary>
public class TransferenciaService(
    PagUaiDbContext db,
    AntifraudeService antifraude,
    NotificacaoService notificacoes)
{
    public async Task<Transferencia> TransferirAsync(Guid contaOrigemId, string chavePixDestino, decimal valor, string descricao, CancellationToken ct)
    {
        if (valor <= 0)
        {
            throw ErroNegocio.Invalido("O valor da transferência deve ser maior que zero.");
        }

        // Se quem chamou já abriu uma transação (por exemplo, o pagamento de cobrança), participa dela.
        var transacaoPropria = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        await using var _ = transacaoPropria;

        var origem = await db.Contas.FirstOrDefaultAsync(c => c.Id == contaOrigemId, ct)
            ?? throw ErroNegocio.NaoEncontrado("Conta de origem");
        var destino = await db.Contas.FirstOrDefaultAsync(c => c.ChavePix == chavePixDestino, ct)
            ?? throw ErroNegocio.NaoEncontrado("Chave PIX de destino");

        if (origem.Id == destino.Id)
        {
            throw ErroNegocio.Invalido("Não é possível transferir para a própria conta.");
        }

        if (origem.Saldo < valor)
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

        // 1. Antifraude em processo (síncrono).
        var analise = await antifraude.AnalisarAsync(transferencia.Id, origem.Id, valor, ct);
        if (analise.Decisao == DecisaoAntifraude.Reprovada)
        {
            transferencia.Status = StatusTransferencia.ReprovadaAntifraude;
            db.Transferencias.Add(transferencia);
            await db.SaveChangesAsync(ct);
            if (transacaoPropria is not null)
            {
                await transacaoPropria.CommitAsync(ct);
            }

            return transferencia;
        }

        // 2. Movimenta saldos e grava o extrato diretamente.
        origem.Saldo -= valor;
        destino.Saldo += valor;
        transferencia.Status = StatusTransferencia.Concluida;
        db.Transferencias.Add(transferencia);
        db.Lancamentos.Add(new Lancamento { ContaId = origem.Id, Tipo = TipoLancamento.Debito, Valor = valor, Descricao = $"{descricao} para {destino.Titular}" });
        db.Lancamentos.Add(new Lancamento { ContaId = destino.Id, Tipo = TipoLancamento.Credito, Valor = valor, Descricao = $"{descricao} de {origem.Titular}" });

        // 3. Notificação dentro da transação.
        await notificacoes.NotificarAsync(origem.Id, origem.Titular, $"Você enviou R$ {valor:N2} para {destino.Titular}.", ct);
        await notificacoes.NotificarAsync(destino.Id, destino.Titular, $"Você recebeu R$ {valor:N2} de {origem.Titular}.", ct);

        await db.SaveChangesAsync(ct);
        if (transacaoPropria is not null)
        {
            await transacaoPropria.CommitAsync(ct);
        }

        return transferencia;
    }
}
