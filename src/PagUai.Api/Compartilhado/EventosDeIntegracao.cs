using PagUai.Mensageria;

namespace PagUai.Api.Compartilhado;

/// <summary>
/// Eventos para OUTROS SERVIÇOS, pelo RabbitMQ (Sprint 3). Formatos: contratos/eventos.asyncapi.yaml.
///
/// Diferença para o <see cref="PublicadorDeEventos"/> (dentro do processo):
///  - lá, o manipulador roda na hora e na mesma transação; se ele falhar, tudo é desfeito;
///  - aqui, o evento só sai DEPOIS do commit, e quem consome roda em outro processo, no seu tempo.
///    Se Notificações estiver fora do ar, o PIX acontece do mesmo jeito.
/// </summary>
public sealed class EventosDeIntegracao(UnidadeDeTrabalho unidade, PublicadorNoBroker publicador, ILogger<EventosDeIntegracao> logger)
{
    public void PublicarAoConfirmar<TDados>(string tipo, string chave, TDados dados, int versao = 1) =>
        unidade.AposConfirmar(async () =>
        {
            try
            {
                await publicador.PublicarAsync(tipo, versao, chave, dados, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // DOR PROPOSITAL (dual write): o banco já confirmou e o broker não recebeu. O PIX vale,
                // mas Notificações e Extrato nunca vão saber dele. O padrão Outbox resolve: gravar o
                // evento numa tabela, na MESMA transação, e publicar depois. Veja o ADR 0005.
                logger.LogError(ex, "EVENTO PERDIDO: {Tipo} não foi publicado depois do commit. Dados: {@Dados}", tipo, dados);
            }
        });
}
