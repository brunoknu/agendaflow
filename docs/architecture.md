# Arquitetura — AgendaFlow

## Visão Geral

Monólito modular com quatro camadas. Frontend React servido separadamente. Comunicação via API REST com cookies de sessão.

```
┌──────────────────────────────────────────────────────┐
│                   AgendaFlow.Web                     │
│            React + TypeScript (Vite)                  │
│  /login  /app/*  /agendar/:slug  /agendamento/*      │
└─────────────────────┬────────────────────────────────┘
                      │ HTTP (cookies + CSRF token)
┌─────────────────────▼────────────────────────────────┐
│                  AgendaFlow.Api                       │
│   Controllers · Middleware · Rate Limiting · Auth    │
└──────────┬──────────────────────────────┬────────────┘
           │                              │
┌──────────▼──────────┐    ┌─────────────▼────────────┐
│ AgendaFlow.Application│    │ AgendaFlow.Infrastructure │
│  Use Cases · DTOs    │    │  EF Core · Identity      │
│  Contracts · Rules   │    │  Repositories · Email    │
└──────────┬──────────┘    │  Outbox · RLS            │
           │               └─────────────┬────────────┘
┌──────────▼──────────┐                  │
│  AgendaFlow.Domain  │    ┌─────────────▼────────────┐
│  Entities · Enums   │    │       PostgreSQL 16        │
│  Value Objects      │    │  Tables · Constraints     │
│  Exceptions         │    │  Row-Level Security       │
└─────────────────────┘    └──────────────────────────┘
```

## Dependências entre camadas

- **Domain** não depende de ninguém.
- **Application** depende apenas de Domain.
- **Infrastructure** depende de Domain e Application (implementa contratos).
- **Api** depende de Application e Infrastructure.
- **Web** consome a Api via HTTP.

## Fluxo de uma requisição autenticada

1. Request chega no ASP.NET Core pipeline.
2. `GlobalExceptionMiddleware` envolve o pipeline.
3. `UseAuthentication()` valida o cookie de sessão.
4. `UseAuthorization()` verifica permissões.
5. `TenantResolutionMiddleware` consulta o banco com o `userId` da sessão e define o `TenantContext`.
6. Controller recebe a requisição, chama o serviço de aplicação.
7. Serviço de aplicação usa repositórios para ler/escrever dados.
8. EF Core aplica global query filters (TenantId) em todas as queries.
9. PostgreSQL aplica Row-Level Security como defesa adicional.
10. Resposta retorna com Problem Details em caso de erro.

## Multi-tenancy

Três camadas de isolamento:

1. **TenantContext** (scoped por request) — determina o tenant do usuário autenticado via sessão do servidor, nunca via input do cliente.
2. **EF Core Global Query Filters** — todas as queries ao banco incluem automaticamente `WHERE tenant_id = @tenantId`.
3. **PostgreSQL Row-Level Security** — mesmo que um bug bypasse os filtros da aplicação, o banco recusa acesso a linhas de outros tenants.

## Autenticação

ASP.NET Core Identity com cookies HttpOnly. O token CSRF é gerado pelo servidor e enviado no header `X-XSRF-TOKEN` em todas as requisições mutantes.

## Outbox Pattern

Notificações (emails) são escritas no banco de dados como `OutboxMessage` na mesma transação do agendamento. Um `BackgroundService` processa a fila periodicamente com retry exponencial, garantindo que o email seja enviado mesmo se o processo reiniciar imediatamente após a transação.

## Tratamento de concorrência

Ver [ADR-003](decisions/ADR-003-appointment-concurrency.md). A `EXCLUSION CONSTRAINT` do PostgreSQL é a garantia final.
