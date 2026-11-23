# ADR 0005: Como os serviços conversam por eventos (mensageria)

> **Modelo para você preencher (Sprint 3).** Responda às perguntas-guia, apague as perguntas e faça o commit no seu fork. Depois compare com a versão aprovada pela Helena, na ODA (aba Sprint 3).

- **Status:** proposta
- **Proposto por:** _(seu nome)_, arquiteto(a)
- **Participantes:** Rafa Souza (Tech Lead), Joana Lima (Product Owner)
- **Revisão e aprovação:** Helena Rocha, Coordenadora de Arquitetura
- **Data:** _(data de hoje)_

## Contexto

_Perguntas-guia:_
- _Desde a Sprint 0, o que acontece com o PIX quando o e-mail falha? Por que separar em módulos (Sprint 1) não resolveu?_
- _O Extrato da Sprint 2 faz três chamadas HTTP ao monólito a cada consulta. O que acontece com ele quando o monólito cai ou fica lento?_
- _Quem precisa saber que um PIX aconteceu? Quem precisa esperar a resposta de quem?_

## Decisão

_Perguntas-guia:_
- _Pagamentos chama Notificações ou anuncia um fato? Qual é o nome do fato e por que ele está no passado?_
- _Qual broker? Por que fila por consumidor e não uma fila só? (Dica: exchange do tipo topic, chave de roteamento.)_
- _O que vai dentro do evento? Por que o evento leva os titulares, e não só os ids?_
- _Quando o evento é publicado: antes ou depois do commit? O que pode dar errado em cada caso?_
- _O Extrato continua perguntando ao monólito ou passa a ter os próprios dados?_
- _Onde fica o contrato dos eventos? (Dica: contratos/eventos.asyncapi.yaml.)_

### Confiabilidade

| Tema | Decisão | Por quê |
| --- | --- | --- |
| Confirmação (ack) | | |
| Entrega "pelo menos uma vez" e idempotência | | |
| Retry | | |
| Dead letter | | |
| Mensagem persistente e confirmação do publicador | | |
| Tipo de fila (classic ou quorum) | | |

## Alternativas consideradas

| Alternativa | Por que não agora |
| --- | --- |
| _(por exemplo: Kafka, um log de eventos, no lugar do RabbitMQ)_ | _..._ |
| _(por exemplo: uma biblioteca de mais alto nível, como MassTransit ou Wolverine)_ | _..._ |
| _(por exemplo: padrão Outbox já nesta sprint)_ | _..._ |
| _(por exemplo: chamada HTTP assíncrona, sem broker)_ | _..._ |

## Consequências

_Perguntas-guia:_
- _O que acontece com o PIX quando Notificações está fora do ar? E com o e-mail?_
- _Quanto tempo o extrato pode ficar atrás de Pagamentos? Como o contrato do Extrato avisa isso?_
- _O que acontece se o processo cair entre o commit e a publicação? Como isso aparece nos logs?_
- _O que passou a existir para operar (broker, filas, dead letter) e quem cuida disso?_
