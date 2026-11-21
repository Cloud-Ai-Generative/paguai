# ADR 0003: O Extrato é o primeiro módulo a sair do monólito

- **Status:** aceita (Sprint 1)
- **Proposto por:** o(a) arquiteto(a) do time (você, na Sprint 1)
- **Revisão e aprovação:** Helena Rocha, Coordenadora de Arquitetura

> Esta é a versão aprovada. Compare com a sua proposta da Sprint 1.

## Contexto

A Joana precisa mostrar progresso; o Rafa teme o custo de operar muitos serviços de uma vez. A primeira extração vai ensinar o time a operar um serviço separado (rede, deploy próprio, monitoramento), então ela deve ter o menor risco possível.

## Critérios

| Módulo | Escala própria | Ritmo próprio | Isolamento de falha | Transação com outros | Risco da fronteira |
| --- | --- | --- | --- | --- | --- |
| Contas | baixa | baixo | médio | alta (abertura cria saldo) | baixo |
| Pagamentos | alta | médio | alto | **alta** (centro do PIX) | **alto** |
| Cobranças | média | médio | médio | alta (paga via Pagamentos) | médio |
| Antifraude | alta | alto | alto | média | médio, mas no caminho crítico |
| **Extrato** | **alta** (consultas) | médio | **alto** (derrubou o checkout) | **nenhuma** (só leitura) | **baixo** |
| Notificações | média | médio | alto | alta (dentro da transação) | baixo, mas pede assincronia |

## Decisão

O Extrato sai primeiro, na Sprint 2, com o padrão **Strangler Fig**: um proxy na frente desvia só a rota do extrato para o novo serviço, e o monólito continua atendendo o resto.

## Alternativas consideradas

| Alternativa | Por que não agora |
| --- | --- |
| Antifraude | Forte candidato, mas está no caminho crítico de todo PIX. Extraí-lo antes de ter timeout e resiliência (Sprint 9) põe cada pagamento atrás de uma chamada de rede. |
| Notificações | Precisa de comunicação assíncrona (Sprint 3). Extraída como chamada síncrona, manteria o acoplamento. |
| Pagamentos | Subdomínio central e participante de quase todas as transações: o maior risco de todos. |

## Consequências

- A PagUai ganha um deploy independente para a parte que mais derrubou o checkout, com risco baixo.
- Aparece uma rede entre o Extrato e Pagamentos: latência, falhas parciais e a pergunta "o que acontece quando o Extrato cai?" (Sprint 2).
- As próximas extrações serão decididas em novos ADRs.
