using Microsoft.EntityFrameworkCore;
using PagUai.Api.Compartilhado;
using PagUai.Api.Modulos.Contas.Contratos;

namespace PagUai.Api.Modulos.Contas.Interno;

/// <summary>Implementa o contrato público <see cref="IContas"/> e as operações internas do módulo.</summary>
public class ServicoDeContas(ContasDbContext db, UnidadeDeTrabalho unidade, PublicadorDeEventos eventos, EventosDeIntegracao integracao) : IContas
{
    public async Task<ContaResumo?> ObterAsync(Guid contaId, CancellationToken ct) =>
        await db.Contas.Where(c => c.Id == contaId)
            .Select(c => new ContaResumo(c.Id, c.Titular, c.ChavePix))
            .FirstOrDefaultAsync(ct);

    public async Task<ContaResumo?> ObterPorChavePixAsync(string chavePix, CancellationToken ct) =>
        await db.Contas.Where(c => c.ChavePix == chavePix)
            .Select(c => new ContaResumo(c.Id, c.Titular, c.ChavePix))
            .FirstOrDefaultAsync(ct);

    public Task<Conta> AbrirAsync(string titular, string documento, string chavePix, decimal depositoInicial, CancellationToken ct) =>
        unidade.ExecutarAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(titular) || string.IsNullOrWhiteSpace(documento) || string.IsNullOrWhiteSpace(chavePix))
            {
                throw ErroNegocio.Invalido("Titular, documento e chave PIX são obrigatórios.");
            }

            if (depositoInicial < 0)
            {
                throw ErroNegocio.Invalido("O depósito inicial não pode ser negativo.");
            }

            if (await db.Contas.AnyAsync(c => c.ChavePix == chavePix || c.Documento == documento, ct))
            {
                throw new ErroNegocio(StatusCodes.Status409Conflict, "Conta já existe", "Já existe uma conta com este documento ou chave PIX.");
            }

            var conta = new Conta { Titular = titular.Trim(), Documento = documento.Trim(), ChavePix = chavePix.Trim() };
            db.Contas.Add(conta);
            await db.SaveChangesAsync(ct);

            // Contas não conhece Pagamentos: só avisa que a conta foi aberta.
            await eventos.PublicarAsync(new ContaAberta(conta.Id, conta.Titular, depositoInicial), ct);

            // Sprint 3: e avisa os outros SERVIÇOS (o Extrato monta o seu modelo de leitura com isto).
            // O evento só sai depois do commit.
            integracao.PublicarAoConfirmar(MensagensDeContas.ContaAberta, MensagensDeContas.ChaveContaAberta,
                new DadosDeContaAberta(conta.Id, conta.Titular, conta.ChavePix, depositoInicial, DateTime.UtcNow));
            return conta;
        }, ct);
}
