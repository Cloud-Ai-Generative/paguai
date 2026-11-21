# PagUai: do monólito aos microsserviços

> **Empresa fictícia.** A PagUai, seus personagens, clientes, números e o "PIX" deste projeto são **inventados** para fins didáticos. Não há relação com empresas reais nem com o sistema PIX do Banco Central.

> **Código didático com vulnerabilidades propositais.** Algumas falhas (por exemplo, extrato sem autenticação) existem de propósito para serem corrigidas nas sprints de segurança. **Não use este código em produção.**

Projeto prático da disciplina **Arquitetura de Microsserviços e APIs** (Pós-graduação em Arquitetura de Software Distribuído, PUC Minas Virtual). Prof. Wellington Dimas da Cruz.

## A história

A PagUai é uma fintech mineira fundada em 2019 em Belo Horizonte ("Pagamento sem complicação, uai!"). Começou como um **monólito**, decisão correta para a época (veja o [ADR 0001](docs/adr/0001-comecar-com-monolito.md)). Hoje precisa crescer sem parar de funcionar. Você é o(a) arquiteto(a) recém-contratado(a) para conduzir essa evolução: você propõe as decisões e escreve os ADRs; a Helena, Coordenadora de Arquitetura, revisa e aprova.

## Sprint 2: Strangler Fig, o primeiro serviço sai do monólito

Um **gateway** (YARP) passa a ser a porta de entrada. O endereço continua o mesmo (`http://localhost:8080`), mas agora o gateway decide quem atende: a rota do extrato vai para o **serviço de Extrato**, o primeiro extraído do monólito; todo o resto continua no monólito modular.

```
                        ┌──────────────────────── docker compose ─────────────────────────┐
  app, portal,          │                                                                  │
  Scalar, .http  ──────►│  PagUai.Gateway (YARP) :8080                                     │
                        │     │ GET /contas/{id}/extrato          │ todo o resto           │
                        │     ▼                                   ▼                        │
                        │  PagUai.Extrato :8082  ── HTTP ──►  PagUai.Api (monólito) :8081  │
                        │  contract-first, sem banco           Contas · Pagamentos · ...   │
                        │                                      │              │            │
                        │                                 PostgreSQL 17   Mailpit          │
                        └──────────────────────────────────────────────────────────────────┘
```

- **Strangler Fig:** no `docker-compose.yml`, `Strangler__Extrato` diz para onde vai a rota do extrato: `"servico"` (padrão) ou `"monolito"`, o Extrato antigo, que ainda está no monólito. É o "voltar atrás" sem mudar código. O Extrato antigo será apagado na Sprint 3.
- **API First:** o contrato do serviço, [`contratos/extrato.openapi.yaml`](contratos/extrato.openapi.yaml), foi escrito **antes** do código, na Sala de Contrato da ODA. O teste `ContratoTests` falha se o serviço divergir dele, e o CI faz o lint do contrato com Spectral. O monólito continua **code-first** (o contrato dele é gerado do código), para você comparar.
- **Mock:** `docker compose --profile mock up -d mock` sobe o Prism em `http://localhost:4010`, respondendo o que o contrato descreve, sem código nenhum.
- **Cada resposta diz quem atendeu:** cabeçalho `X-PagUai-Servico: extrato` ou `monolito`.
- **Dor proposital (Sprint 9):** o Extrato chama o monólito **sem timeout configurado**. Se o monólito ficar lento, o Extrato fica lento junto.

> **Vindo de outra sprint?** Rode `docker compose down` antes de subir: os contêineres e as portas mudaram.

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
| Notificações | Avisa as contas (e-mail no Mailpit) | `notificacoes` |
| Extrato | Só leitura: monta o extrato perguntando a Contas e a Pagamentos | nenhum |

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
| Caixa de e-mails das notificações (Mailpit) | http://localhost:8025 |
| PostgreSQL | `localhost:5432` (banco, usuário: `paguai`). Schemas dos módulos: `docker compose exec postgres psql -U paguai -d paguai -c "\dn"` |

Requisições prontas: [`requests/paguai.http`](requests/paguai.http) (extensão REST Client).

### Problemas comuns

| Sintoma | Causa | Solução |
| --- | --- | --- |
| `Bind for 0.0.0.0:8080 failed: port is already allocated` | Outro programa ou contêiner já usa a porta | Descubra quem é: `sudo ss -ltnp \| grep :8080` (Linux), `lsof -i :8080` (macOS), `netstat -ano \| findstr :8080` (Windows) e `docker ps`. Pare o processo ou suba em outra porta: `PAGUAI_API_PORT=9080 docker compose up -d` |
| Depois de corrigir a porta, a API não acha o banco (`Resource temporarily unavailable` nos logs) | O contêiner da API ficou com a rede incompleta após a primeira falha | Recrie tudo: `docker compose down` e depois `docker compose up -d`. Não basta religar o contêiner |
| `buildx Docker CLI plugin not found` | Docker sem o plugin buildx | Apenas um aviso; o build funciona. Para eliminá-lo, instale o pacote `docker-buildx-plugin` |
| Tabelas da Sprint 0 (`public.contas`, `public.transferencias`...) ainda aparecem no banco | O volume do banco foi criado por outra sprint; a API cria os schemas novos e ignora as tabelas antigas | Para começar limpo: `docker compose down -v` e depois `docker compose up -d --build` |
| `curl` sem resposta em `/health` | A API ainda está subindo ou caiu | `docker compose ps` e `docker compose logs api --tail 40` |
| `502 Bad Gateway` no extrato | O serviço de Extrato está parado ou ainda subindo; o gateway não tem para onde mandar | `docker compose ps` e `docker compose logs extrato --tail 40`. Para voltar ao Extrato antigo: `Strangler__Extrato: "monolito"` e `docker compose up -d gateway` |
| `503` com `dependencia-indisponivel` no extrato | O Extrato está no ar, mas não consegue falar com o monólito | `docker compose logs api --tail 40` |

Portas configuráveis: `PAGUAI_API_PORT` (gateway, 8080), `PAGUAI_MONOLITO_PORT` (8081), `PAGUAI_EXTRATO_PORT` (8082), `PAGUAI_DB_PORT` (5432), `PAGUAI_MAIL_UI_PORT` (8025), `PAGUAI_SMTP_PORT` (1025) e `PAGUAI_MOCK_PORT` (4010).

### Endpoints

| Método e rota | O que faz |
| --- | --- |
| `GET /health` | API e banco no ar? |
| `POST /contas` · `GET /contas` · `GET /contas/{id}` | Abrir e consultar contas (Contas) |
| `POST /transferencias` | PIX simulado: passa pelo antifraude e notifica as duas contas (Pagamentos) |
| `GET /saldos/{contaId}` | Saldo da conta (Pagamentos) |
| `POST /cobrancas` · `GET /cobrancas/{id}` · `POST /cobrancas/{id}/pagar` | Cobranças dos lojistas |
| `GET /contas/{id}/extrato?de=&ate=&limite=&cursor=` | Extrato, no **serviço de Extrato** (padrão: últimos 30 dias, até 100 lançamentos; máximo de 90 dias) |
| `GET /pagamentos/contas/{id}/movimentos` | Uso interno: movimentos paginados por cursor, consultados pelo serviço de Extrato |

### As dores do monólito (de propósito)

| Dor | Como ver | Resolvida na |
| --- | --- | --- |
| Tudo é publicado junto | Qualquer mudança exige `docker compose up --build` da API inteira | Sprint 0 (diagnóstico) |
| ~~Módulos mexem nas tabelas uns dos outros~~ | Resolvida: cada módulo tem contrato, DbContext e schema próprios, verificados pelos testes de arquitetura | Sprint 1 ✅ |
| ~~Um deploy do Extrato derruba o checkout~~ | Resolvida: o Extrato é um serviço com deploy próprio; com ele parado, o PIX continua (`docker compose stop extrato`) | Sprint 2 ✅ |
| Extrato sem timeout ao chamar o monólito | `docker compose pause api` e consulte o extrato: a requisição fica pendurada por até 100 s. Depois, `docker compose unpause api` | Sprint 9 |
| Notificação dentro da transação (continua, mesmo com módulos) | `Notificacoes__SimularFalha=true` no `docker-compose.yml`: a transferência falha junto | Sprint 3 |
| Antifraude lento trava tudo | Aumente `Antifraude__LatenciaMs` | Sprints 6 e 9 |
| Extrato sem autenticação (BOLA) | Qualquer id abre o extrato de qualquer conta | Sprints 11 e 12 |

### Rodando os testes

```bash
dotnet test
```

Os testes de integração do monólito sobem um PostgreSQL em contêiner (Testcontainers), então o Docker precisa estar em execução. Os testes de contrato do Extrato e os de rotas do gateway não precisam de banco:

```bash
dotnet test --project tests/PagUai.Extrato.Tests   # o serviço segue o contrato?
dotnet test --project tests/PagUai.Gateway.Tests   # o Strangler Fig manda cada rota para o lugar certo?
```

Só os testes de arquitetura do monólito:

```bash
dotnet test --filter-class "*ArquiteturaTests"
```

## Como as sprints evoluem neste repositório

Cada sprint é uma branch (`sprint/NN-nome`) que vira Pull Request e, ao final, uma **tag** (`sprint-00`, `sprint-01`...). Para estudar o estado exato de uma sprint:

```bash
git checkout sprint-02
```

## Decisões de arquitetura (ADRs)

| ADR | Decisão | Status |
| --- | --- | --- |
| [0001](docs/adr/0001-comecar-com-monolito.md) | Começar a PagUai como um monólito | aceita (decisão herdada) |
| [0002](docs/adr/0002-monolito-modular.md) | Organizar o monólito em módulos com fronteiras verificadas | aceita (Sprint 1) |
| [0003](docs/adr/0003-primeiro-a-sair.md) | O Extrato é o primeiro módulo a sair do monólito | aceita (Sprint 1) |
| [0004](docs/adr/0004-como-extrair-o-extrato.md) | Como extrair o Extrato: Strangler Fig, gateway e contrato | **para você escrever** (Sprint 2) |

A cada sprint, um ADR vem como modelo, com perguntas-guia. Escreva a sua proposta, faça o commit no seu fork e depois compare com a versão aprovada pela Helena, na ODA. A versão aprovada entra no repositório na tag da sprint seguinte.

Ou baixe o ZIP da tag em **Releases/Tags** no GitHub.
