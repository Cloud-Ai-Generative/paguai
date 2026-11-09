# ADR 0001: Começar a PagUai como um monólito

- **Status:** aceita (2019, decisão fictícia)
- **Decisores:** Helena Rocha (CTO)

## Contexto

Em 2019 a PagUai tinha 4 pessoas desenvolvedoras, nenhum cliente e uma hipótese de negócio ainda não validada: cobrança por link e QR Code para feirantes e pequenos comerciantes de Belo Horizonte. O domínio ainda mudava toda semana.

## Decisão

Construir **uma única aplicação** ASP.NET Core, com **um único banco** PostgreSQL e **um único deploy**, organizada em pastas por módulo (Contas, Transferências, Cobranças, Antifraude, Extrato e Notificações).

## Alternativas consideradas

| Alternativa | Por que não agora |
| --- | --- |
| Microsserviços desde o início | Time pequeno, domínio instável: as fronteiras entre serviços quase certamente estariam erradas, e mudar fronteira entre serviços é caro. |
| Monólito modular com fronteiras rígidas | Boa ideia, mas exigia mais disciplina do que o time conseguia sustentar no MVP. Fica para quando as fronteiras estiverem claras (Sprint 1). |

## Consequências

**Positivas:** entrega rápida (MVP em 3 meses), depuração simples, transações ACID entre módulos, um único lugar para rodar e monitorar.

**Negativas (que vão aparecer com o crescimento):**

- Um único deploy: qualquer mudança exige publicar tudo.
- Módulos acessam as tabelas uns dos outros pelo mesmo `DbContext`.
- Escalar uma parte (por exemplo, o Extrato) significa escalar tudo.
- Uma falha em um módulo (Notificações, Antifraude) pode derrubar o fluxo inteiro.

Essas consequências não tornam a decisão errada: ela foi correta **para o contexto de 2019**. A disciplina mostra o que muda quando o contexto muda.
