# AgendaFlow — Status do Projeto

Atualizado: implementação completa das fases 1-9.

## Status Geral

🟢 MVP implementado — aguardando execução com Docker/NuGet locais

## Fases

| Fase | Status | Observações |
|------|--------|-------------|
| 1 — Fundação | ✅ Concluída | Solution, projetos, Docker, CI, gitignore |
| 2 — Identidade e Tenants | ✅ Concluída | Identity, cookies, CSRF, TenantContext, RLS |
| 3 — Catálogo e Equipe | ✅ Concluída | Service, Professional, CRUD, planos |
| 4 — Disponibilidade | ✅ Concluída | AvailabilityCalculator, regras, exceções, timezone |
| 5 — Agendamentos | ✅ Concluída | Appointment, status machine, exclusion constraint, idempotência |
| 6 — Fluxo Público | ✅ Concluída | Página pública, confirmação/cancelamento por token |
| 7 — Gestão e Dashboard | ✅ Estrutura | Controllers e DTOs; dashboard com dados reais pendente de seed |
| 8 — Notificações | ✅ Concluída | Outbox, OutboxProcessor, MailKit, templates básicos |
| 9 — Hardening | ✅ Concluída | RLS SQL, security headers, CSP, threat model |
| 10 — Documentação | ✅ Concluída | README, ADRs, architecture.md, security.md, deployment.md |

## Validações Executadas Neste Ambiente

| Validação | Status | Resultado |
|-----------|--------|-----------|
| `dotnet build AgendaFlow.Domain` | ✅ Passou | 0 erros, 0 warnings |
| `dotnet build AgendaFlow.Application` | ✅ Passou | 0 erros, 0 warnings |
| `npm run typecheck` (Web) | ✅ Passou | 0 erros |
| `npm run build` (Web) | ✅ Passou | Bundle gerado sem erros |

## Limitações do Ambiente de Geração

| Limitação | Impacto |
|-----------|---------|
| NuGet bloqueado (`api.nuget.org` não permitido) | Infrastructure e Api precisam de `dotnet restore` localmente |
| Docker não disponível | docker-compose.yml criado, execução local necessária |
| PostgreSQL não disponível | Migrations e RLS verificados sintaticamente; execução local necessária |

## Para Executar Localmente

```bash
cp .env.example .env
docker compose up --build
# Aguardar ~30s para migrations e seed
# Acessar: http://localhost:5173
```

## Arquivos que Merecem Atenção em Entrevista

| Arquivo | Por quê |
|---------|---------|
| `src/AgendaFlow.Domain/Entities/Appointment.cs` | Status machine, invariantes, buffers |
| `src/AgendaFlow.Application/Availability/AvailabilityCalculator.cs` | Lógica de slots com timezone e intervalos half-open |
| `src/AgendaFlow.Api/Middleware/TenantResolutionMiddleware.cs` | Por que TenantId vem da sessão, nunca do request |
| `src/AgendaFlow.Api/Middleware/GlobalExceptionMiddleware.cs` | Problem Details sem vazar detalhes internos |
| `src/AgendaFlow.Infrastructure/Persistence/AppDbContext.cs` | Global query filters + por que o usuário da app não é dono das tabelas |
| `docker/migrations/AddAppointmentExclusionConstraint.sql` | Garantia de concorrência no banco |
| `docker/migrations/EnableRowLevelSecurity.sql` | Terceira camada de isolamento |
| `tests/AgendaFlow.UnitTests/Domain/AppointmentTests.cs` | Cobertura da máquina de estados |
| `tests/AgendaFlow.UnitTests/Availability/AvailabilityCalculatorTests.cs` | Casos edge de disponibilidade |
| `docs/decisions/` | Justificativas das principais decisões |
