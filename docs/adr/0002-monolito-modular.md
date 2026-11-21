# ADR 0002: Organizar o monólito em módulos com fronteiras verificadas

> **Modelo para você preencher (Sprint 1).** Responda às perguntas-guia de cada seção com as suas palavras, apague as perguntas e faça o commit no seu fork. Depois compare com a versão aprovada pela Helena, na ODA (aba Sprint 1). Não existe uma única resposta certa: o que se avalia num ADR é se a decisão está **bem justificada** para o contexto.

- **Status:** proposta
- **Proposto por:** _(seu nome)_, arquiteto(a)
- **Revisão e aprovação:** Helena Rocha, Coordenadora de Arquitetura
- **Data:** _(data de hoje)_

## Contexto

_Perguntas-guia:_
- _Que problemas a Sprint 0 mostrou na organização do código? (Dica: quem grava as tabelas de quem? O que você viu em `TransferenciaService` e em `CobrancasEndpoints`?)_
- _Que contextos de negócio (bounded contexts) você identificou na PagUai? Qual é o subdomínio central?_
- _Por que ainda não é hora de extrair microsserviços?_

## Decisão

_Perguntas-guia:_
- _Como cada módulo vai expor o que os outros podem usar? E o que fica escondido?_
- _Como os dados de cada módulo serão separados, ainda num banco só?_
- _Como garantir que a regra continue valendo daqui a seis meses, com o time crescendo?_
- _Quem passa a ser dono do saldo e dos lançamentos? Por quê?_

## Alternativas consideradas

| Alternativa | Por que não agora |
| --- | --- |
| _(por exemplo: extrair microsserviços já)_ | _..._ |
| _(por exemplo: um projeto .csproj por módulo)_ | _..._ |
| _(outra que você considerou)_ | _..._ |

## Consequências

_Perguntas-guia:_
- _O que melhora? (pense em quem pode mudar o quê sem afetar os outros)_
- _O que piora ou continua igual? (pense no deploy, na transação do PIX e na falha da notificação)_
- _Que contratos da API mudaram, e quem é afetado?_
