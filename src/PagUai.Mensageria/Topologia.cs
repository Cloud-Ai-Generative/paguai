using RabbitMQ.Client;

namespace PagUai.Mensageria;

/// <summary>
/// Uma fila de um consumidor e o que acontece quando ele falha.
///
///   exchange paguai.eventos (topic)
///        │  chave pagamento.aprovado
///        ▼
///   fila {Nome} ──falhou, ainda há tentativas──► fila {Nome}.retry (espera, depois volta)
///        │
///        └──falhou na última tentativa──────────► fila {Nome}.dlq (dead letter: alguém precisa olhar)
/// </summary>
/// <param name="Nome">Nome da fila (um por consumidor: cada consumidor recebe a sua cópia do evento).</param>
/// <param name="Chaves">Chaves de roteamento que esta fila assina, como "pagamento.aprovado".</param>
/// <param name="MaximoDeTentativas">Total de tentativas antes de mandar para a dead letter.</param>
/// <param name="EsperaEntreTentativas">Quanto tempo a mensagem espera na fila de retry.</param>
/// <param name="Prefetch">Quantas mensagens o consumidor recebe sem ter confirmado as anteriores.</param>
public sealed record ConfiguracaoDaFila(
    string Nome,
    IReadOnlyList<string> Chaves,
    int MaximoDeTentativas = 3,
    TimeSpan? EsperaEntreTentativas = null,
    ushort Prefetch = 10)
{
    public TimeSpan Espera => EsperaEntreTentativas ?? TimeSpan.FromSeconds(5);
    public string FilaDeRetry => $"{Nome}.retry";
    public string FilaDeDeadLetter => $"{Nome}.dlq";
}

/// <summary>
/// Declara exchange e filas. Declarar é idempotente: se já existem com os mesmos argumentos, nada muda.
/// No Docker Compose, o próprio RabbitMQ já cria tudo isto ao subir (infra/rabbitmq/definitions.json),
/// para nenhum evento se perder antes de os consumidores ligarem. Os argumentos aqui e lá precisam ser
/// iguais; se forem diferentes, o RabbitMQ recusa a declaração (PRECONDITION_FAILED).
/// </summary>
public static class Topologia
{
    /// <summary>Exchange do tipo topic onde a PagUai publica todos os eventos.</summary>
    public const string Exchange = "paguai.eventos";

    public static Task DeclararExchangeAsync(IChannel canal, CancellationToken ct) =>
        canal.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);

    public static async Task DeclararFilaAsync(IChannel canal, ConfiguracaoDaFila fila, CancellationToken ct)
    {
        await DeclararExchangeAsync(canal, ct);

        // Fila principal. Uma mensagem rejeitada (sem devolver à fila) vai para a dead letter.
        await canal.QueueDeclareAsync(fila.Nome, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = fila.FilaDeDeadLetter
            }, cancellationToken: ct);

        // Fila de retry: ninguém consome. A mensagem espera o tempo de vida (TTL) e volta para a fila principal.
        await canal.QueueDeclareAsync(fila.FilaDeRetry, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-message-ttl"] = (int)fila.Espera.TotalMilliseconds,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = fila.Nome
            }, cancellationToken: ct);

        // Dead letter: mensagens que esgotaram as tentativas. Ficam aqui até alguém investigar e reprocessar.
        await canal.QueueDeclareAsync(fila.FilaDeDeadLetter, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);

        foreach (var chave in fila.Chaves)
        {
            await canal.QueueBindAsync(fila.Nome, Exchange, chave, cancellationToken: ct);
        }
    }
}
