# PagUai: do monólito aos microsserviços

> **Empresa fictícia.** A PagUai, seus personagens, clientes, números e o "PIX" deste projeto são **inventados** para fins didáticos. Não há relação com empresas reais nem com o sistema PIX do Banco Central.

> **Código didático com vulnerabilidades propositais.** Algumas falhas (por exemplo, extrato sem autenticação) existem de propósito para serem corrigidas nas sprints de segurança. **Não use este código em produção.**

Projeto prático da disciplina **Arquitetura de Microsserviços e APIs** (Pós-graduação em Arquitetura de Software Distribuído, PUC Minas Virtual). Prof. Wellington Dimas da Cruz.

## A história

A PagUai é uma fintech mineira fundada em 2019 em Belo Horizonte ("Pagamento sem complicação, uai!"). Começou como um **monólito**, decisão correta para a época (veja o [ADR 0001](docs/adr/0001-comecar-com-monolito.md)). Hoje precisa crescer sem parar de funcionar. Você é o(a) arquiteto(a) recém-contratado(a) para conduzir essa evolução: você propõe as decisões e escreve os ADRs; a Helena, Coordenadora de Arquitetura, revisa e aprova.

## Sprint 1: o monólito modular

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
| API + documentação interativa (Scalar) | http://localhost:8080 |
| Contrato OpenAPI | http://localhost:8080/openapi/v1.json |
| Caixa de e-mails das notificações (Mailpit) | http://localhost:8025 |
| PostgreSQL | `localhost:5432` (banco, usuário: `paguai`). Schemas dos módulos: `docker compose exec postgres psql -U paguai -d paguai -c "\dn"` |

Requisições prontas: [`requests/paguai.http`](requests/paguai.http) (extensão REST Client).

### Problemas comuns

| Sintoma | Causa | Solução |
| --- | --- | --- |
| `Bind for 0.0.0.0:8080 failed: port is already allocated` | Outro programa ou contêiner já usa a porta | Descubra quem é: `sudo ss -ltnp \| grep :8080` (Linux), `lsof -i :8080` (macOS), `netstat -ano \| findstr :8080` (Windows) e `docker ps`. Pare o processo ou suba em outra porta: `PAGUAI_API_PORT=8081 docker compose up -d` |
| Depois de corrigir a porta, a API não acha o banco (`Resource temporarily unavailable` nos logs) | O contêiner da API ficou com a rede incompleta após a primeira falha | Recrie tudo: `docker compose down` e depois `docker compose up -d`. Não basta religar o contêiner |
| `buildx Docker CLI plugin not found` | Docker sem o plugin buildx | Apenas um aviso; o build funciona. Para eliminá-lo, instale o pacote `docker-buildx-plugin` |
| Tabelas da Sprint 0 (`public.contas`, `public.transferencias`...) ainda aparecem no banco | O volume do banco foi criado por outra sprint; a API cria os schemas novos e ignora as tabelas antigas | Para começar limpo: `docker compose down -v` e depois `docker compose up -d --build` |
| `curl` sem resposta em `/health` | A API ainda está subindo ou caiu | `docker compose ps` e `docker compose logs api --tail 40` |

Portas configuráveis: `PAGUAI_API_PORT` (8080), `PAGUAI_DB_PORT` (5432), `PAGUAI_MAIL_UI_PORT` (8025) e `PAGUAI_SMTP_PORT` (1025).

### Endpoints

| Método e rota | O que faz |
| --- | --- |
| `GET /health` | API e banco no ar? |
| `POST /contas` · `GET /contas` · `GET /contas/{id}` | Abrir e consultar contas (Contas) |
| `POST /transferencias` | PIX simulado: passa pelo antifraude e notifica as duas contas (Pagamentos) |
| `GET /saldos/{contaId}` | Saldo da conta (Pagamentos) |
| `POST /cobrancas` · `GET /cobrancas/{id}` · `POST /cobrancas/{id}/pagar` | Cobranças dos lojistas |
| `GET /contas/{id}/extrato?de=&ate=` | Extrato (padrão: últimos 30 dias) |

### As dores do monólito (de propósito)

| Dor | Como ver | Resolvida na |
| --- | --- | --- |
| Tudo é publicado junto | Qualquer mudança exige `docker compose up --build` da API inteira | Sprint 0 (diagnóstico) |
| ~~Módulos mexem nas tabelas uns dos outros~~ | Resolvida: cada módulo tem contrato, DbContext e schema próprios, verificados pelos testes de arquitetura | Sprint 1 ✅ |
| Extrato disputa o banco com as transferências | Mesmo banco, mesmo processo | Sprint 2 |
| Notificação dentro da transação (continua, mesmo com módulos) | `Notificacoes__SimularFalha=true` no `docker-compose.yml`: a transferência falha junto | Sprint 3 |
| Antifraude lento trava tudo | Aumente `Antifraude__LatenciaMs` | Sprints 6 e 9 |
| Extrato sem autenticação (BOLA) | Qualquer id abre o extrato de qualquer conta | Sprints 11 e 12 |

### Rodando os testes

```bash
dotnet test
```

Os testes de integração sobem um PostgreSQL em contêiner (Testcontainers), então o Docker precisa estar em execução. Só os testes de arquitetura, que não precisam de banco nem de Docker:

```bash
dotnet test --filter-class "*ArquiteturaTests"
```

## Como as sprints evoluem neste repositório

Cada sprint é uma branch (`sprint/NN-nome`) que vira Pull Request e, ao final, uma **tag** (`sprint-00`, `sprint-01`...). Para estudar o estado exato de uma sprint:

```bash
git checkout sprint-01
```

## Decisões de arquitetura (ADRs)

| ADR | Decisão | Status |
| --- | --- | --- |
| [0001](docs/adr/0001-comecar-com-monolito.md) | Começar a PagUai como um monólito | aceita (decisão herdada) |
| [0002](docs/adr/0002-monolito-modular.md) | Organizar o monólito em módulos com fronteiras verificadas | **para você escrever** (Sprint 1) |
| [0003](docs/adr/0003-primeiro-a-sair.md) | Qual módulo sai primeiro do monólito | **para você escrever** (Sprint 1) |

Os ADRs 0002 e 0003 vêm como modelo, com perguntas-guia. Escreva a sua proposta, faça o commit no seu fork e depois compare com a versão aprovada pela Helena, na ODA. As versões aprovadas entram no repositório na tag `sprint-02`.

Ou baixe o ZIP da tag em **Releases/Tags** no GitHub.
