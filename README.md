# PagUai: do monólito aos microsserviços

> **Empresa fictícia.** A PagUai, seus personagens, clientes, números e o "PIX" deste projeto são **inventados** para fins didáticos. Não há relação com empresas reais nem com o sistema PIX do Banco Central.

> **Código didático com vulnerabilidades propositais.** Algumas falhas (por exemplo, extrato sem autenticação) existem de propósito para serem corrigidas nas sprints de segurança. **Não use este código em produção.**

Projeto prático da disciplina **Arquitetura de Microsserviços e APIs** (Pós-graduação em Arquitetura de Software Distribuído, PUC Minas Virtual). Prof. Wellington Dimas da Cruz.

## A história

A PagUai é uma fintech mineira fundada em 2019 em Belo Horizonte ("Pagamento sem complicação, uai!"). Começou como um **monólito**, decisão correta para a época (veja o [ADR 0001](docs/adr/0001-comecar-com-monolito.md)). Hoje precisa crescer sem parar de funcionar. Você é o(a) arquiteto(a) recém-contratado(a) para conduzir essa evolução: você propõe as decisões e escreve os ADRs; a Helena, Coordenadora de Arquitetura, revisa e aprova.

## Sprint 3: mensageria com filas, tópicos e brokers

Desde a Sprint 0, uma falha no e-mail desfazia o PIX do Seu Geraldo. Agora o pagamento **não espera ninguém**: depois do commit, Pagamentos **publica** o evento `PagamentoAprovado` no **RabbitMQ**, e cada interessado recebe a sua cópia, no seu tempo.

```
                  ┌──────────────────────────────── docker compose ─────────────────────────────────┐
  app, portal,    │  PagUai.Gateway (YARP) :8080                                                    │
  Scalar, .http ─►│     │ GET /contas/{id}/extrato              │ todo o resto                      │
                  │     ▼                                       ▼                                   │
                  │  PagUai.Extrato :8082                   PagUai.Api (monólito) :8081             │
                  │  modelo de leitura                      Contas · Pagamentos · Cobranças · ...   │
                  │     ▲  fila extrato                         │ publica ContaAberta e             │
                  │     │                                       ▼ PagamentoAprovado (depois do commit)
                  │     └──────────────── RabbitMQ :5672 · exchange paguai.eventos (topic)          │
                  │                          │  fila notificacoes      interface :15672             │
                  │                          ▼                                                      │
                  │                     PagUai.Notificacoes (Worker) ──► Mailpit :8025              │
                  │                                                                                 │
                  │  PostgreSQL 17: um database por serviço · paguai · extrato · notificacoes       │
                  └─────────────────────────────────────────────────────────────────────────────────┘
```

- **Publicar em vez de chamar:** Pagamentos anuncia um fato (`PagamentoAprovado`, no passado) e não conhece quem consome. O evento leva o que os consumidores precisam (os titulares, por exemplo), para eles não perguntarem de volta ao monólito.
- **Uma fila por consumidor:** a exchange `paguai.eventos` é do tipo *topic*. As filas `notificacoes` e `extrato` assinam a chave `pagamento.aprovado`, e cada uma recebe a sua cópia. A fila `extrato` também assina `conta.aberta`.
- **Notificações virou serviço:** `src/PagUai.Notificacoes` é um *Worker Service*, sem porta HTTP, com banco próprio (`notificacoes`). O módulo de Notificações saiu do monólito.
- **Extrato com dados próprios:** o Extrato monta um **modelo de leitura** (database `extrato`) com os eventos e não chama mais o monólito. Com o monólito fora do ar, o extrato continua respondendo. Em troca, ele pode ficar alguns instantes atrás de Pagamentos (**consistência eventual**); o campo `saldoEm` diz até quando o saldo está em dia.
- **Confirmação, retry e dead letter:** a mensagem só sai da fila depois que o consumidor termina (ack manual). Se ele falhar, a mensagem espera 5 s na fila `.retry` e volta; na 3ª falha, vai para a fila `.dlq`. Como a entrega é **pelo menos uma vez**, os consumidores são **idempotentes** (guardam o `id` de cada evento já tratado).
- **Contrato dos eventos:** [`contratos/eventos.asyncapi.yaml`](contratos/eventos.asyncapi.yaml) (AsyncAPI 3.0), validado no CI e verificado nos testes de quem publica e de quem consome.
- **Topologia como configuração:** o RabbitMQ cria usuário, exchange e filas ao subir ([`infra/rabbitmq/definitions.json`](infra/rabbitmq/definitions.json)). Assim nenhum evento se perde por "não havia ninguém para recebê-lo".
- **Fim do Strangler Fig do Extrato:** o Extrato antigo foi apagado do monólito, e a configuração `Strangler__Extrato` deixou de existir.
- **Dor proposital (dual write):** o evento sai **depois** do commit. Se o broker estiver fora do ar nesse instante, o PIX vale e o evento se perde (aparece no log como `EVENTO PERDIDO`). O padrão **Outbox** resolve; veja o [ADR 0005](docs/adr/0005-mensageria.md).

> **Vindo da Sprint 2?** Os serviços e os bancos mudaram. Comece limpo: `docker compose down -v` e depois `docker compose up -d --build`.

### Veja a mensageria funcionando

1. Faça um PIX (requisição 4 de [`requests/paguai.http`](requests/paguai.http)) e abra o Mailpit (http://localhost:8025): os dois e-mails chegam em instantes.
2. Na interface do RabbitMQ (http://localhost:15672, usuário `paguai`, senha `paguai_dev_123`), aba **Queues and Streams**, veja as filas `notificacoes` e `extrato` e as suas `.retry` e `.dlq`.
3. **Falha no e-mail:** no `docker-compose.yml`, mude `Notificacoes__SimularFalha` para `"true"` e rode `docker compose up -d notificacoes`. Faça outro PIX: ele conclui normalmente. Acompanhe `docker compose logs -f notificacoes`: tentativa 1, 2 e 3, e a mensagem vai para `notificacoes.dlq`.
4. **Reprocessar:** volte `Notificacoes__SimularFalha` para `"false"`, rode `docker compose up -d notificacoes` e mova as mensagens da dead letter de volta para a fila. Pela interface: fila `notificacoes.dlq` → **Move messages** → destino `notificacoes`. Ou pelo terminal:

   ```bash
   docker compose exec rabbitmq rabbitmqctl set_parameter shovel reprocessar-notificacoes '{"src-uri":"amqp://","src-queue":"notificacoes.dlq","dest-uri":"amqp://","dest-queue":"notificacoes","src-delete-after":"queue-length"}'
   ```

5. **Extrato independente:** `docker compose stop api` e consulte o extrato direto no serviço (http://localhost:8082/contas/{id}/extrato): ele responde. Depois, `docker compose start api`.

### O Strangler Fig e o API First (Sprint 2)

O **gateway** (YARP) é a porta de entrada (`http://localhost:8080`) e decide quem atende: a rota do extrato vai para o serviço de Extrato; todo o resto, para o monólito. O contrato do Extrato, [`contratos/extrato.openapi.yaml`](contratos/extrato.openapi.yaml), foi escrito **antes** do código (API First), tem mock com Prism (`docker compose --profile mock up -d mock` → http://localhost:4010) e é verificado pelo teste `ContratoTests` e pelo lint no CI. Cada resposta diz quem atendeu: cabeçalho `X-PagUai-Servico`. Para ver como era o "voltar atrás" com `Strangler__Extrato`, use `git checkout sprint-02`.

### O monólito modular (Sprint 1)

Ainda **um processo, um deploy e um banco**. O que mudou: cada contexto de negócio virou um **módulo** com fronteira verificada.

```
┌──────────────────────────── docker compose ────────────────────────────┐
│  PagUai.Api (ASP.NET Core, .NET 10, Minimal APIs)                      │
│                                                                        │
│   Contas ◄── Pagamentos ──► Antifraude          Extrato (só leitura)   │
│     ▲        │      ▲   └──► Notificações        lê Contas e Pagamentos │
│     │        │      │                                                  │
│     └── Cobranças ──┘        cada seta = contrato público (Contratos/) │
│                                                                        │
│   PostgreSQL 17: um banco, um schema por módulo                        │
│   contas · pagamentos · cobrancas · antifraude · notificacoes          │
│                                          Mailpit (e-mails)             │
└────────────────────────────────────────────────────────────────────────┘
```

| Módulo | Responsabilidade | Schema |
| --- | --- | --- |
| Contas | Cadastro de clientes e lojistas e chaves PIX | `contas` |
| Pagamentos | Transferências PIX, **saldo** e **lançamentos** (na Sprint 0 se chamava Transferências) | `pagamentos` |
| Cobranças | O lojista cobra e o cliente paga | `cobrancas` |
| Antifraude | Decide se um pagamento pode seguir | `antifraude` |
| ~~Notificações~~ | Saiu do monólito na Sprint 3: agora é o serviço `PagUai.Notificacoes` | ~~`notificacoes`~~ |
| ~~Extrato~~ | Saiu do monólito: serviço na Sprint 2; o módulo antigo foi apagado na Sprint 3 | nenhum |

Cada módulo tem três partes:

```
Modulos/Pagamentos/
├── Contratos/          ← PÚBLICO: o que os outros módulos podem usar (IPagamentos e os tipos dele)
├── Interno/            ← PRIVADO: entidades, DbContext, serviços, endpoints e migrations
└── ModuloPagamentos.cs ← registro do módulo no Program.cs
```

Os **testes de arquitetura** (`tests/PagUai.Api.Tests/ArquiteturaTests.cs`) falham se um módulo usar a pasta `Interno` de outro. Como todos os módulos estão no mesmo projeto, o compilador sozinho não impede: o teste é a regra.

**Mudanças de contrato nesta sprint:** `GET /contas` não traz mais o saldo (o saldo é de Pagamentos); use `GET /saldos/{contaId}` ou o extrato.

> **Vindo da Sprint 0?** O banco mudou de estrutura: cada módulo agora tem o seu schema. Para não ficar com as tabelas antigas sem uso, apague o volume antes de subir: `docker compose down -v`.

### Pré-requisitos

- Git e Docker (com Docker Compose)
- Opcional: .NET 10 SDK e VS Code com C# Dev Kit
- Pouca memória (menos de 8 GB)? Use o **GitHub Codespaces**: botão `Code` → `Codespaces` → `Create codespace on main`.

Verifique sua máquina:

```bash
./scripts/check-env.sh          # Linux e macOS
powershell -ExecutionPolicy Bypass -File .\scripts\check-env.ps1   # Windows
```

### Subindo o sistema

```bash
docker compose up -d --build
```

| O quê | Onde |
| --- | --- |
| Porta de entrada (gateway): toda a API | http://localhost:8080 |
| Monólito, direto, com Scalar (code-first) | http://localhost:8081 · contrato gerado: http://localhost:8081/openapi/v1.json |
| Serviço de Extrato, direto, com Scalar (contract-first) | http://localhost:8082 · contrato: http://localhost:8082/contratos/extrato.openapi.yaml |
| Mock do contrato do Extrato (Prism) | `docker compose --profile mock up -d mock` → http://localhost:4010 |
| Interface do RabbitMQ (filas, mensagens, dead letter) | http://localhost:15672 · usuário `paguai`, senha `paguai_dev_123` |
| Caixa de e-mails das notificações (Mailpit) | http://localhost:8025 |
| PostgreSQL | `localhost:5432` (usuário `paguai`). Um database por serviço: `docker compose exec postgres psql -U paguai -d paguai -c "\l"`. Schemas dos módulos do monólito: `docker compose exec postgres psql -U paguai -d paguai -c "\dn"` |

Requisições prontas: [`requests/paguai.http`](requests/paguai.http) (extensão REST Client).

### Problemas comuns

| Sintoma | Causa | Solução |
| --- | --- | --- |
| `Bind for 0.0.0.0:8080 failed: port is already allocated` | Outro programa ou contêiner já usa a porta | Descubra quem é: `sudo ss -ltnp \| grep :8080` (Linux), `lsof -i :8080` (macOS), `netstat -ano \| findstr :8080` (Windows) e `docker ps`. Pare o processo ou suba em outra porta: `PAGUAI_API_PORT=9080 docker compose up -d` |
| Depois de corrigir a porta, a API não acha o banco (`Resource temporarily unavailable` nos logs) | O contêiner da API ficou com a rede incompleta após a primeira falha | Recrie tudo: `docker compose down` e depois `docker compose up -d`. Não basta religar o contêiner |
| `buildx Docker CLI plugin not found` | Docker sem o plugin buildx | Apenas um aviso; o build funciona. Para eliminá-lo, instale o pacote `docker-buildx-plugin` |
| `curl` sem resposta em `/health` | A API ainda está subindo ou caiu | `docker compose ps` e `docker compose logs api --tail 40` |
| `502 Bad Gateway` no extrato | O serviço de Extrato está parado ou ainda subindo; o gateway não tem para onde mandar | `docker compose ps` e `docker compose logs extrato --tail 40` |
| `503` com `dependencia-indisponivel` no extrato | O Extrato está no ar, mas o banco dele não responde | `docker compose ps postgres` e `docker compose logs extrato --tail 40` |
| `404` no extrato de uma conta que acabou de ser aberta | Consistência eventual: o evento `ContaAberta` ainda não chegou ao Extrato | Espere um instante. Se continuar, veja se há mensagens em `extrato.dlq` na interface do RabbitMQ |
| O PIX conclui, mas o e-mail não chega | Notificações está parado ou falhando; a mensagem está na fila, no retry ou na dead letter | `docker compose logs notificacoes --tail 40` e a fila `notificacoes.dlq` na interface do RabbitMQ (veja "Reprocessar", acima) |
| `EVENTO PERDIDO` nos logs do monólito | O RabbitMQ estava fora do ar no instante da publicação (dual write) | O PIX vale, mas Notificações e Extrato não souberam dele. É a dor proposital que o padrão Outbox resolve |
| `PRECONDITION_FAILED` ou `inequivalent arg` nos logs | A fila já existe no RabbitMQ com outros argumentos (por exemplo, criada por outra versão) | `docker compose down -v` e suba de novo |
| Tabelas ou schemas de sprints anteriores no banco | O volume do banco foi criado por outra sprint | `docker compose down -v` e depois `docker compose up -d --build` |

Portas configuráveis: `PAGUAI_API_PORT` (gateway, 8080), `PAGUAI_MONOLITO_PORT` (8081), `PAGUAI_EXTRATO_PORT` (8082), `PAGUAI_DB_PORT` (5432), `PAGUAI_RABBITMQ_PORT` (5672), `PAGUAI_RABBITMQ_UI_PORT` (15672), `PAGUAI_MAIL_UI_PORT` (8025), `PAGUAI_SMTP_PORT` (1025) e `PAGUAI_MOCK_PORT` (4010).

### Endpoints

| Método e rota | O que faz |
| --- | --- |
| `GET /health` | API e banco no ar? |
| `POST /contas` · `GET /contas` · `GET /contas/{id}` | Abrir e consultar contas (Contas) |
| `POST /transferencias` | PIX simulado: passa pelo antifraude e publica `PagamentoAprovado` (Pagamentos) |
| `GET /saldos/{contaId}` | Saldo da conta (Pagamentos) |
| `POST /cobrancas` · `GET /cobrancas/{id}` · `POST /cobrancas/{id}/pagar` | Cobranças dos lojistas |
| `GET /contas/{id}/extrato?de=&ate=&limite=&cursor=` | Extrato, no **serviço de Extrato**, pelo modelo de leitura (padrão: últimos 30 dias, até 100 lançamentos; máximo de 90 dias) |
| `GET /pagamentos/contas/{id}/movimentos` | Movimentos paginados por cursor, direto de Pagamentos (o Extrato usava até a Sprint 2) |

Eventos publicados (veja [`contratos/eventos.asyncapi.yaml`](contratos/eventos.asyncapi.yaml)):

| Evento | Chave | Quem publica | Quem consome |
| --- | --- | --- | --- |
| `ContaAberta` | `conta.aberta` | Contas (monólito) | Extrato |
| `PagamentoAprovado` | `pagamento.aprovado` | Pagamentos (monólito) | Notificações e Extrato |

### As dores do monólito (de propósito)

| Dor | Como ver | Resolvida na |
| --- | --- | --- |
| Tudo é publicado junto | Qualquer mudança exige `docker compose up --build` da API inteira | Sprint 0 (diagnóstico) |
| ~~Módulos mexem nas tabelas uns dos outros~~ | Resolvida: cada módulo tem contrato, DbContext e schema próprios, verificados pelos testes de arquitetura | Sprint 1 ✅ |
| ~~Um deploy do Extrato derruba o checkout~~ | Resolvida: o Extrato é um serviço com deploy próprio; com ele parado, o PIX continua (`docker compose stop extrato`) | Sprint 2 ✅ |
| ~~Extrato sem timeout ao chamar o monólito~~ | Resolvida de outro jeito: o Extrato não chama mais o monólito (`docker compose stop api` e o extrato continua) | Sprint 3 ✅ |
| ~~Notificação dentro da transação~~ | Resolvida: com `Notificacoes__SimularFalha=true`, o PIX conclui e o e-mail vai para retry e dead letter | Sprint 3 ✅ |
| Evento perdido entre o commit e a publicação (dual write) | `docker compose stop rabbitmq`, faça um PIX e procure `EVENTO PERDIDO` em `docker compose logs api`. Depois, `docker compose start rabbitmq` | Padrão Outbox (ADR 0005) |
| Antifraude lento trava tudo | Aumente `Antifraude__LatenciaMs` | Sprints 6 e 9 |
| Extrato sem autenticação (BOLA) | Qualquer id abre o extrato de qualquer conta | Sprints 11 e 12 |

### Rodando os testes

```bash
dotnet test
```

Os testes de integração sobem PostgreSQL e RabbitMQ em contêineres (Testcontainers), então o Docker precisa estar em execução. Para usar servidores que já estão rodando (por exemplo, os do `docker compose`), defina `PAGUAI_TEST_DB` e `PAGUAI_TEST_RABBITMQ`:

```bash
PAGUAI_TEST_DB="Host=localhost;Port=5432;Database=paguai;Username=paguai;Password=paguai_dev_123" \
PAGUAI_TEST_RABBITMQ="amqp://paguai:paguai_dev_123@localhost:5672" dotnet test
```

Por projeto:

```bash
dotnet test --project tests/PagUai.Api.Tests            # monólito: módulos, PIX e eventos publicados depois do commit
dotnet test --project tests/PagUai.Mensageria.Tests     # ack, retry, dead letter e mensagem inválida
dotnet test --project tests/PagUai.Notificacoes.Tests   # e-mails e idempotência
dotnet test --project tests/PagUai.Extrato.Tests        # contrato OpenAPI e modelo de leitura
dotnet test --project tests/PagUai.Gateway.Tests        # cada rota vai para o lugar certo?
```

Só os testes de arquitetura do monólito:

```bash
dotnet test --filter-class "*ArquiteturaTests"
```

## Como as sprints evoluem neste repositório

Cada sprint é uma branch (`sprint/NN-nome`) que vira Pull Request e, ao final, uma **tag** (`sprint-00`, `sprint-01`...). Para estudar o estado exato de uma sprint:

```bash
git checkout sprint-03
```

## Decisões de arquitetura (ADRs)

| ADR | Decisão | Status |
| --- | --- | --- |
| [0001](docs/adr/0001-comecar-com-monolito.md) | Começar a PagUai como um monólito | aceita (decisão herdada) |
| [0002](docs/adr/0002-monolito-modular.md) | Organizar o monólito em módulos com fronteiras verificadas | aceita (Sprint 1) |
| [0003](docs/adr/0003-primeiro-a-sair.md) | O Extrato é o primeiro módulo a sair do monólito | aceita (Sprint 1) |
| [0004](docs/adr/0004-como-extrair-o-extrato.md) | Como extrair o Extrato: Strangler Fig, gateway e contrato | aceita (Sprint 2) |
| [0005](docs/adr/0005-mensageria.md) | Como os serviços conversam por eventos (mensageria) | **para você escrever** (Sprint 3) |

A cada sprint, um ADR vem como modelo, com perguntas-guia. Escreva a sua proposta, faça o commit no seu fork e depois compare com a versão aprovada pela Helena, na ODA. A versão aprovada entra no repositório na tag da sprint seguinte.

Ou baixe o ZIP da tag em **Releases/Tags** no GitHub.
