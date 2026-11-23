using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using PagUai.Mensageria;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PagUai.Api.Tests;

/// <summary>
/// Uma fila temporária, só do teste, que assina os eventos que o monólito publica.
/// Serve para ver o que o monólito publicou, sem atrapalhar as filas de verdade.
/// </summary>
public sealed class EscutaDeEventos : IAsyncDisposable
{
    private readonly Channel<JsonObject> _recebidos = Channel.CreateUnbounded<JsonObject>();
    private readonly List<JsonObject> _vistos = [];
    private IConnection _conexao = null!;
    private IChannel _canal = null!;

    public static async Task<EscutaDeEventos> AbrirAsync(string url)
    {
        var escuta = new EscutaDeEventos();
        escuta._conexao = await new ConnectionFactory { Uri = new Uri(url) }.CreateConnectionAsync();
        escuta._canal = await escuta._conexao.CreateChannelAsync();
        await Topologia.DeclararExchangeAsync(escuta._canal, CancellationToken.None);
        var fila = await escuta._canal.QueueDeclareAsync(); // nome gerado pelo broker, exclusiva, apagada ao fechar
        foreach (var chave in new[] { "conta.aberta", "pagamento.aprovado" })
        {
            await escuta._canal.QueueBindAsync(fila.QueueName, Topologia.Exchange, chave);
        }

        var consumidor = new AsyncEventingBasicConsumer(escuta._canal);
        consumidor.ReceivedAsync += (_, entrega) =>
        {
            escuta._recebidos.Writer.TryWrite(JsonNode.Parse(entrega.Body.Span)!.AsObject());
            return Task.CompletedTask;
        };
        await escuta._canal.BasicConsumeAsync(fila.QueueName, autoAck: true, consumidor);
        return escuta;
    }

    /// <summary>Espera um evento do tipo pedido cujos dados satisfaçam o filtro. Null se não chegar a tempo.</summary>
    public async Task<JsonObject?> EsperarAsync(string tipo, Func<JsonObject, bool> filtro, TimeSpan? limite = null)
    {
        bool Serve(JsonObject e) => e["tipo"]!.GetValue<string>() == tipo && filtro(e["dados"]!.AsObject());

        var achado = _vistos.FirstOrDefault(Serve);
        if (achado is not null)
        {
            return achado;
        }

        using var tempo = new CancellationTokenSource(limite ?? TimeSpan.FromSeconds(10));
        try
        {
            while (true)
            {
                var evento = await _recebidos.Reader.ReadAsync(tempo.Token);
                _vistos.Add(evento);
                if (Serve(evento))
                {
                    return evento;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public static bool Texto(JsonObject dados, string campo, object valor) =>
        string.Equals(dados[campo]?.ToString(), valor.ToString(), StringComparison.OrdinalIgnoreCase);

    public async ValueTask DisposeAsync()
    {
        await _canal.DisposeAsync();
        await _conexao.DisposeAsync();
    }
}
