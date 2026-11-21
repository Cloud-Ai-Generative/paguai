# ADR 0002: Organizar o monólito em módulos com fronteiras verificadas

- **Status:** aceita (Sprint 1)
- **Proposto por:** o(a) arquiteto(a) do time (você, na Sprint 1)
- **Revisão e aprovação:** Helena Rocha, Coordenadora de Arquitetura

> Esta é a versão aprovada. Se você escreveu a sua proposta na Sprint 1, compare: o que se avalia é se a decisão está bem justificada, não se as palavras são as mesmas.

## Contexto

O monólito cresceu sem fronteiras. Transferências gravava as tabelas do Extrato e das Notificações, Cobranças usava o serviço interno de Transferências, o Antifraude lia a tabela de transferências e uma única classe `Conta` misturava cadastro e saldo. Extrair serviços nesse estado criaria um monólito distribuído. O time ainda não tem automação para operar seis serviços.

## Decisão

Organizar o monólito em seis módulos, um por bounded context: Contas, Pagamentos, Cobranças, Antifraude, Extrato e Notificações.

- Cada módulo expõe um contrato público (pasta `Contratos`) e esconde o resto (pasta `Interno`).
- Cada módulo tem um `DbContext` e um schema próprios no PostgreSQL, sem chave estrangeira entre schemas.
- Transferências passa a se chamar **Pagamentos** e se torna dono do saldo e dos lançamentos. O Extrato vira só de leitura.
- Contas avisa a abertura de conta por um evento em processo (`ContaAberta`), para não depender de Pagamentos.
- Testes de arquitetura, rodando a cada build, impedem um módulo de usar o interior de outro.

## Alternativas consideradas

| Alternativa | Por que não agora |
| --- | --- |
| Extrair microsserviços já | Monólito distribuído: as mesmas dependências, agora com rede, seis deploys e seis bancos. Custo de operação alto para o time atual. |
| Um projeto `.csproj` por módulo | Válida e mais forte (o compilador impede o acesso). Adiada pelo custo de manter muitos projetos; pode ser adotada depois sem mudar os contratos. |
| Só reorganizar pastas, sem regra verificada | O acoplamento volta aos poucos. |

## Consequências

- Cada módulo pode mudar por dentro sem afetar os outros, e as próximas extrações começam de um contrato pronto.
- Continuam: deploy único, escala do sistema inteiro e a transação do PIX passando por vários módulos (a falha da notificação ainda derruba o pagamento, Sprint 3).
- O contrato da API mudou: `GET /contas` não traz mais o saldo, que passa a ser consultado em `GET /saldos/{contaId}`.
