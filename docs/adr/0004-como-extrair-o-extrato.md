# ADR 0004: Como extrair o Extrato (Strangler Fig, gateway e contrato)

> **Modelo para você preencher (Sprint 2).** Use a ata da **Sala de Contrato** (ODA, aba Sprint 2) como ponto de partida: ela já traz as decisões do contrato. Responda às perguntas-guia, apague as perguntas e faça o commit no seu fork. Depois compare com a versão aprovada pela Helena, na ODA.

- **Status:** proposta
- **Proposto por:** _(seu nome)_, arquiteto(a)
- **Participantes:** Lívia Andrade (front-end, representante dos consumidores da API), Joana Lima (Product Owner), Rafa Souza (Tech Lead)
- **Revisão e aprovação:** Helena Rocha, Coordenadora de Arquitetura
- **Data:** _(data de hoje)_

## Contexto

_Perguntas-guia:_
- _O ADR 0003 decidiu que o Extrato sai primeiro. Que restrições existem? (Dica: o app já usa o extrato do monólito.)_
- _Por que não dá para desligar o Extrato antigo e ligar o novo de uma vez?_

## Decisão

_Perguntas-guia:_
- _Como o tráfego passa do monólito para o serviço novo? Quem decide para onde vai cada rota?_
- _Como voltar atrás se o serviço novo der problema?_
- _De onde o Extrato tira os dados, se ele não tem banco?_
- _O contrato veio antes ou depois do código? Por quê?_

### Decisões do contrato (da Sala de Contrato)

| Tema | Decisão | Por quê |
| --- | --- | --- |
| Valores em dinheiro | | |
| Paginação | | |
| Período padrão e máximo | | |
| Saldo no extrato | | |
| Formato dos erros | | |
| Versão | | |
| Segurança | | |

## Alternativas consideradas

| Alternativa | Por que não agora |
| --- | --- |
| _(por exemplo: trocar tudo de uma vez, sem gateway)_ | _..._ |
| _(por exemplo: o Extrato com o próprio banco, alimentado por eventos)_ | _..._ |
| _(por exemplo: contrato gerado a partir do código)_ | _..._ |

## Consequências

_Perguntas-guia:_
- _O que acontece com o PIX quando o Extrato cai? E com o Extrato quando o monólito cai?_
- _Quantas chamadas de rede uma consulta de extrato faz agora? O que isso custa?_
- _O que ficou de fora de propósito e volta em outra sprint (timeout, autenticação, versão)?_
