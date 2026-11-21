# ADR 0003: Qual módulo sai primeiro do monólito

> **Modelo para você preencher (Sprint 1).** Use os critérios da aba Sprint 1 da ODA. Responda às perguntas-guia, apague as perguntas e faça o commit no seu fork. Depois compare com a versão aprovada pela Helena, na ODA.

- **Status:** proposta
- **Proposto por:** _(seu nome)_, arquiteto(a)
- **Revisão e aprovação:** Helena Rocha, Coordenadora de Arquitetura
- **Data:** _(data de hoje)_

## Contexto

_Perguntas-guia:_
- _O que a Joana (Product Owner) espera ganhar com a primeira extração?_
- _O que o Rafa (Tech Lead) teme? Quanto custa operar cada serviço novo?_
- _Por que extrair um módulo de cada vez, e não todos juntos?_

## Critérios

Avalie os seis módulos. Use **alto**, **médio** ou **baixo**.

| Módulo | Escala diferente do resto | Ritmo de mudança diferente | Isolamento de falha | Precisa de transação com outros módulos | Risco de errar a fronteira |
| --- | --- | --- | --- | --- | --- |
| Contas | | | | | |
| Pagamentos | | | | | |
| Cobranças | | | | | |
| Antifraude | | | | | |
| Extrato | | | | | |
| Notificações | | | | | |

## Decisão

_Perguntas-guia:_
- _Qual módulo sai primeiro? Por quê ele e não os outros?_
- _Como o resto do sistema continua funcionando enquanto ele sai?_

## Alternativas consideradas

| Alternativa | Por que não agora |
| --- | --- |
| _..._ | _..._ |

## Consequências

_Perguntas-guia:_
- _O que a PagUai ganha com essa primeira extração?_
- _Que problemas novos aparecem quando existe uma rede entre dois módulos?_
