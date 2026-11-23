# ADR 0004: Como extrair o Extrato (Strangler Fig, gateway e contrato)

- **Status:** aceita (Sprint 2)
- **Proposto por:** o(a) arquiteto(a) do time (você, na Sprint 2)
- **Participantes:** Lívia Andrade (front-end, representante dos consumidores da API), Joana Lima (Product Owner), Rafa Souza (Tech Lead)
- **Revisão e aprovação:** Helena Rocha, Coordenadora de Arquitetura

> Esta é a versão aprovada. Compare com a sua proposta da Sprint 2.

## Contexto

O ADR 0003 decidiu extrair o Extrato primeiro. O app e o portal do lojista já usam `GET /contas/{id}/extrato` do monólito, então a troca precisa ser **invisível** para quem consome e **reversível** se o serviço novo falhar.

## Decisão

Um gateway (YARP) passa a ser a porta de entrada e desvia só a rota do extrato para o serviço novo. A configuração `Strangler__Extrato` permite voltar ao Extrato antigo sem mudar código.

O serviço nasce **contract-first**: o contrato foi combinado com a Lívia na Sala de Contrato, publicado como mock (Prism) e verificado no código por testes de contrato e pelo lint no CI. O Extrato não tem banco: consulta Contas e Pagamentos pela API do monólito.

### Decisões do contrato (da Sala de Contrato)

| Tema | Decisão |
| --- | --- |
| Valores em dinheiro | Número com duas casas decimais, como o app já lê; texto fica como candidato para a Sprint 7 |
| Saldo | Dentro do extrato, com o momento da consulta (`saldoEm`) |
| Paginação | Cursor opcional; sem `limite`, até 100 lançamentos |
| Período | Padrão de 30 dias e máximo de 90, com 422 |
| Erros | Problem Details (RFC 9457) com um `type` por erro; 503 quando uma dependência cair |
| Versão | Sem prefixo: só mudanças que não quebram |
| Segurança | Declarada no contrato como vulnerabilidade proposital até a Sprint 11 |

## Alternativas consideradas

| Alternativa | Por que não agora |
| --- | --- |
| Trocar a URL no app | Rejeitada: não há como voltar atrás sem uma nova versão do app. |
| Desviar só uma parte do tráfego (canary) | Válida; adiada até haver métricas por versão (Sprint 10). |
| O Extrato com banco próprio, alimentado por eventos | Adiada: depende da mensageria (Sprint 3). |
| Contrato gerado a partir do código | Rejeitada para um serviço novo: o consumidor só participaria depois. |

## Consequências

- Um deploy ou uma falha do Extrato não derruba mais o PIX.
- Em troca, cada consulta faz três chamadas HTTP ao monólito, e o Extrato depende dele: com o monólito fora, responde 503; com o monólito lento, fica esperando, porque ainda não há timeout (dor proposital).
- O Extrato antigo será apagado do monólito na Sprint 3.

> **Atualização (Sprint 3, ADR 0005):** a alternativa adiada foi adotada. O Extrato passou a ter banco próprio, alimentado por eventos, e não chama mais o monólito. O Extrato antigo e a configuração `Strangler__Extrato` foram apagados.
