namespace PagUai.Api.Compartilhado;

/// <summary>Algo que aconteceu num módulo e que outros módulos podem querer saber.</summary>
public interface IEvento;

/// <summary>Quem quer reagir a um evento implementa esta interface.</summary>
public interface IManipulador<in TEvento> where TEvento : IEvento
{
    Task TratarAsync(TEvento evento, CancellationToken ct);
}

/// <summary>
/// Publicação de eventos DENTRO do processo: chama cada manipulador na hora, na mesma transação.
///
/// Serve para um módulo avisar os outros sem depender deles. Exemplo: Contas publica
/// <c>ContaAberta</c> e Pagamentos cria o saldo, sem que Contas conheça Pagamentos.
/// Eventos para OUTROS SERVIÇOS (Sprint 3) não passam por aqui: veja <see cref="EventosDeIntegracao"/>.
/// </summary>
public sealed class PublicadorDeEventos(IServiceProvider servicos)
{
    public async Task PublicarAsync<TEvento>(TEvento evento, CancellationToken ct) where TEvento : IEvento
    {
        foreach (var manipulador in servicos.GetServices<IManipulador<TEvento>>())
        {
            await manipulador.TratarAsync(evento, ct);
        }
    }
}
