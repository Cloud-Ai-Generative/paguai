# PagUai: do monólito aos microsserviços

> **Empresa fictícia.** A PagUai, seus personagens, clientes, números e o "PIX" deste projeto são **inventados** para fins didáticos. Não há relação com empresas reais nem com o sistema PIX do Banco Central.

> **Código didático com vulnerabilidades propositais.** Algumas falhas (por exemplo, extrato sem autenticação) existem de propósito para serem corrigidas nas sprints de segurança. **Não use este código em produção.**

Projeto prático da disciplina **Arquitetura de Microsserviços e APIs** (Pós-graduação em Arquitetura de Software Distribuído, PUC Minas Virtual). Prof. Wellington Dimas da Cruz.

## A história

A PagUai é uma fintech mineira fundada em 2019 em Belo Horizonte ("Pagamento sem complicação, uai!"). Começou como um **monólito**, decisão correta para a época (veja o [ADR 0001](docs/adr/0001-comecar-com-monolito.md)). Hoje precisa crescer sem parar de funcionar. Você acaba de entrar no time de Arquitetura.

## Sprint 0: o monólito

```
┌──────────────────────── docker compose ────────────────────────┐
│  PagUai.Api (ASP.NET Core, .NET 10, Minimal APIs)              │
│  Contas · Transferências · Cobranças · Antifraude ·            │
│  Extrato · Notificações          (um DbContext, um deploy)     │
│         │                                │                     │
│    PostgreSQL 17                   Mailpit (e-mails)           │
└────────────────────────────────────────────────────────────────┘
```

### Pré-requisitos

- Git e Docker (com Docker Compose)
- Opcional: .NET 10 SDK e VS Code com C# Dev Kit
- Pouca memória (menos de 8 GB)? Use o **GitHub Codespaces**: botão `Code` → `Codespaces` → `Create codespace on main`.

Verifique sua máquina:

```bash
./scripts/check-env.sh          # Linux e macOS
pwsh ./scripts/check-env.ps1    # Windows
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
| PostgreSQL | `localhost:5432` (banco, usuário: `paguai`) |

Requisições prontas: [`requests/paguai.http`](requests/paguai.http) (extensão REST Client).

### Endpoints

| Método e rota | O que faz |
| --- | --- |
| `GET /health` | API e banco no ar? |
| `POST /contas` · `GET /contas` · `GET /contas/{id}` | Abrir e consultar contas |
| `POST /transferencias` | PIX simulado (passa pelo antifraude e notifica as duas contas) |
| `POST /cobrancas` · `GET /cobrancas/{id}` · `POST /cobrancas/{id}/pagar` | Cobranças dos lojistas |
| `GET /contas/{id}/extrato?de=&ate=` | Extrato (padrão: últimos 30 dias) |

### As dores do monólito (de propósito)

| Dor | Como ver | Resolvida na |
| --- | --- | --- |
| Tudo é publicado junto | Qualquer mudança exige `docker compose up --build` da API inteira | Sprint 0 (diagnóstico) |
| Módulos mexem nas tabelas uns dos outros | `TransferenciaService` grava `lancamentos` e `notificacoes` | Sprint 1 |
| Extrato disputa o banco com as transferências | Mesmo banco, mesmo processo | Sprint 2 |
| Notificação dentro da transação | `Notificacoes__SimularFalha=true` no `docker-compose.yml`: a transferência falha junto | Sprint 3 |
| Antifraude lento trava tudo | Aumente `Antifraude__LatenciaMs` | Sprints 6 e 9 |
| Extrato sem autenticação (BOLA) | Qualquer id abre o extrato de qualquer conta | Sprints 11 e 12 |

### Rodando os testes

```bash
dotnet test
```

Os testes de integração sobem um PostgreSQL em contêiner (Testcontainers), então o Docker precisa estar em execução.

## Como as sprints evoluem neste repositório

Cada sprint é uma branch (`sprint/NN-nome`) que vira Pull Request e, ao final, uma **tag** (`sprint-00`, `sprint-01`...). Para estudar o estado exato de uma sprint:

```bash
git checkout sprint-00
```

Ou baixe o ZIP da tag em **Releases/Tags** no GitHub.
