using System.Text.Json;

namespace PagUai.Mensageria;

/// <summary>
/// O envelope de TODO evento publicado pela PagUai (veja contratos/eventos.asyncapi.yaml).
///
/// Os campos do envelope são iguais para qualquer evento; só <see cref="Dados"/> muda.
///  - <c>id</c>: identificador único do evento. É ele que os consumidores usam para ser idempotentes.
///  - <c>tipo</c>: nome do evento no passado, porque um evento é um FATO que já aconteceu ("PagamentoAprovado").
///  - <c>versao</c>: versão do formato de <c>dados</c>. Mudou de um jeito que quebra? Nova versão.
///  - <c>ocorridoEm</c>: quando o fato aconteceu (não quando a mensagem foi entregue).
/// </summary>
public sealed record Envelope<TDados>(Guid Id, string Tipo, int Versao, DateTimeOffset OcorridoEm, TDados Dados);

/// <summary>Um evento que chegou da fila, com os dados ainda em JSON. Cada consumidor lê só o que precisa.</summary>
public sealed class EventoRecebido
{
    public required Guid Id { get; init; }
    public required string Tipo { get; init; }
    public required int Versao { get; init; }
    public required DateTimeOffset OcorridoEm { get; init; }
    public required JsonElement Dados { get; init; }

    /// <summary>Quantas vezes este evento já foi tentado nesta fila (1 na primeira entrega).</summary>
    public int Tentativa { get; init; } = 1;

    /// <summary>
    /// Lê os dados no formato que o CONSUMIDOR espera. Campos a mais são ignorados (leitor tolerante):
    /// o publicador pode acrescentar campos sem quebrar ninguém.
    /// </summary>
    public T Ler<T>() =>
        Dados.Deserialize<T>(Json.Opcoes) ?? throw new MensagemInvalida($"O evento {Tipo} {Id} veio sem dados.");
}

/// <summary>A mensagem não pode ser processada nunca (JSON quebrado, tipo desconhecido). Vai direto para a dead letter, sem retry.</summary>
public class MensagemInvalida(string motivo) : Exception(motivo);

public static class Json
{
    /// <summary>camelCase, como as APIs REST da PagUai.</summary>
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);
}
