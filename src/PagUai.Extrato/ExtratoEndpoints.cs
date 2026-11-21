namespace PagUai.Extrato;

// Respostas no formato do CONTRATO (contratos/extrato.openapi.yaml). Se mudar aqui, o teste
// ContratoTests falha até o contrato ser atualizado, revisado e aprovado.
public record LancamentoResponse(DateTime Data, string Tipo, decimal Valor, string Descricao);
public record ExtratoResponse(Guid ContaId, string Titular, decimal SaldoAtual, DateTime SaldoEm, DateTime De, DateTime Ate,
    IReadOnlyList<LancamentoResponse> Lancamentos, string? ProximoCursor);

public static class ExtratoEndpoints
{
    public const int LimitePadrao = 100;
    public const int LimiteMaximo = 200;
    public const int DiasPadrao = 30;
    public const int DiasMaximo = 90;

    public static void MapExtrato(this IEndpointRouteBuilder app)
    {
        // VULNERABILIDADE PROPOSITAL (Sprints 11 e 12), registrada também no contrato:
        // não há autenticação nem verificação de dono da conta (BOLA, OWASP API1).
        app.MapGet("/contas/{contaId:guid}/extrato", async (Guid contaId, DateTime? de, DateTime? ate, int? limite, string? cursor,
            ClienteDoMonolito monolito, CancellationToken ct) =>
        {
            var tamanho = limite ?? LimitePadrao;
            if (tamanho is < 1 or > LimiteMaximo)
            {
                return Problemas.Criar(422, "parametro-invalido", "Parâmetro inválido", $"O limite deve estar entre 1 e {LimiteMaximo}.");
            }

            var fim = (ate ?? DateTime.UtcNow).ToUniversalTime();
            var inicio = (de ?? fim.AddDays(-DiasPadrao)).ToUniversalTime();
            if (inicio > fim)
            {
                return Problemas.Criar(422, "periodo-invalido", "Período inválido", "A data inicial deve ser anterior à data final.");
            }

            if (fim - inicio > TimeSpan.FromDays(DiasMaximo))
            {
                return Problemas.Criar(422, "periodo-invalido", "Período inválido", $"O período máximo é de {DiasMaximo} dias.");
            }

            try
            {
                // Três chamadas pela rede, uma depois da outra. Cada uma soma latência.
                var conta = await monolito.ObterContaAsync(contaId, ct);
                var saldo = await monolito.ObterSaldoAsync(contaId, ct);
                var pagina = await monolito.ListarMovimentosAsync(contaId, inicio, fim, tamanho, cursor, ct);

                return Results.Ok(new ExtratoResponse(conta.Id, conta.Titular, saldo.Saldo, saldo.ConsultadoEm, inicio, fim,
                    pagina.Itens.Select(m => new LancamentoResponse(m.Data, m.Tipo, m.Valor, m.Descricao)).ToList(),
                    pagina.ProximoCursor));
            }
            catch (ContaNaoEncontrada)
            {
                return Problemas.Criar(404, "conta-nao-encontrada", "Conta não encontrada", "Não existe conta com este id.");
            }
            catch (CursorInvalido)
            {
                return Problemas.Criar(422, "cursor-invalido", "Cursor inválido", "Use o valor de proximoCursor recebido na página anterior.");
            }
            catch (HttpRequestException)
            {
                // Falha parcial: o Extrato está no ar, mas não consegue falar com o monólito.
                return Problemas.Criar(503, "dependencia-indisponivel", "Dependência indisponível",
                    "Não foi possível consultar Contas ou Pagamentos. Tente novamente em instantes.");
            }
        })
        .WithName("consultarExtrato");
    }
}

/// <summary>Erros no formato Problem Details (RFC 9457), com um <c>type</c> para cada erro, como diz o contrato.</summary>
public static class Problemas
{
    public const string Base = "https://paguai.local/problemas/";

    public static IResult Criar(int status, string tipo, string titulo, string detalhe) =>
        Results.Problem(type: Base + tipo, title: titulo, statusCode: status, detail: detalhe);
}
