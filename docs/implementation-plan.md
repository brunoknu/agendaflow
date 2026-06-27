# AgendaFlow — Plano de Implementação

## Visão Geral

Sistema SaaS multi-tenant para agendamento de serviços. Monólito modular com backend .NET 10 e frontend React/TypeScript.

## Ambiente

- .NET 10.0.109
- Node.js 22
- PostgreSQL 16 (via Docker)
- Docker (requerido para execução completa; builds de código executáveis aqui)

## Etapas e Dependências

### Fase 1 — Fundação

**Critérios de conclusão:** solution compila, health check responde, frontend inicia, Docker Compose funciona.

- [x] 1.1 Criar solution e projetos .NET
- [x] 1.2 Estrutura inicial do frontend (Vite + React + TypeScript)
- [x] 1.3 Docker Compose (PostgreSQL, Mailpit, API, Web)
- [x] 1.4 Configuração base (appsettings, .env.example)
- [x] 1.5 Logging estruturado (Serilog)
- [x] 1.6 Tratamento global de erros (Problem Details)
- [x] 1.7 Health checks (liveness + readiness)
- [x] 1.8 CI inicial (GitHub Actions)
- [x] 1.9 .gitignore, .dockerignore, LICENSE

### Fase 2 — Identidade e Tenants

**Critérios de conclusão:** usuário pode registrar, confirmar email, fazer login, criar tenant, ser Owner.

- [ ] 2.1 ASP.NET Core Identity com PostgreSQL
- [ ] 2.2 Entidades: ApplicationUser, Tenant, TenantMembership
- [ ] 2.3 Migrations iniciais
- [ ] 2.4 Contexto de tenant (ITenantContext, scoped)
- [ ] 2.5 Filtros globais EF por TenantId
- [ ] 2.6 Endpoints: register, login, logout, confirm-email, forgot-password, reset-password
- [ ] 2.7 Cookies: HttpOnly, SameSite, Secure
- [ ] 2.8 CSRF (antiforgery token em header)
- [ ] 2.9 Políticas de autorização (Owner, Manager, Staff, PlatformAdmin)
- [ ] 2.10 Rate limiting em autenticação
- [ ] 2.11 Testes de isolamento multi-tenant
- [ ] 2.12 Telas: login, registro, onboarding

### Fase 3 — Catálogo e Equipe

**Critérios de conclusão:** Owner cria serviço e profissional, relacionamento funciona.

- [ ] 3.1 Entidade Service com validações
- [ ] 3.2 Entidade Professional com validações
- [ ] 3.3 Entidade ProfessionalService (relacionamento)
- [ ] 3.4 CRUD de serviços (API + telas)
- [ ] 3.5 CRUD de profissionais (API + telas)
- [ ] 3.6 Gerenciamento de equipe (memberships)
- [ ] 3.7 Testes de permissão e isolamento

### Fase 4 — Disponibilidade

**Critérios de conclusão:** disponibilidade configurável, cálculo de slots correto com timezone.

- [ ] 4.1 Entidades: AvailabilityRule, AvailabilityException
- [ ] 4.2 TimeProvider abstraction
- [ ] 4.3 Lógica de cálculo de slots disponíveis
- [ ] 4.4 Suporte a NodaTime para timezone IANA
- [ ] 4.5 Exceções prevalecem sobre regras recorrentes
- [ ] 4.6 Testes unitários de disponibilidade
- [ ] 4.7 Tela de configuração de disponibilidade

### Fase 5 — Agendamentos

**Critérios de conclusão:** agendamento criado, conflito detectado, idempotência funciona.

- [ ] 5.1 Entidades: Customer, Appointment, AppointmentStatusHistory
- [ ] 5.2 Máquina de estados de status
- [ ] 5.3 Exclusion constraint PostgreSQL para sobreposição
- [ ] 5.4 Transação na criação
- [ ] 5.5 Idempotência (IdempotencyKey)
- [ ] 5.6 Testes de concorrência
- [ ] 5.7 Tela de listagem e detalhes

### Fase 6 — Fluxo Público

**Critérios de conclusão:** cliente agenda sem conta, confirma por email, cancela por link.

- [ ] 6.1 Endpoints públicos por tenantSlug
- [ ] 6.2 Resolução de tenant por slug
- [ ] 6.3 BookingConfirmation com token hash
- [ ] 6.4 Página pública /agendar/:tenantSlug
- [ ] 6.5 Fluxo de confirmação por link
- [ ] 6.6 Fluxo de cancelamento por link
- [ ] 6.7 Rate limiting no booking público
- [ ] 6.8 Testes E2E do fluxo principal

### Fase 7 — Gestão e Dashboard

**Critérios de conclusão:** painel com indicadores reais, filtros funcionais, remarcação.

- [ ] 7.1 Endpoints de listagem com filtros e paginação
- [ ] 7.2 Remarcação de agendamento
- [ ] 7.3 AuditLog
- [ ] 7.4 Dashboard com métricas reais
- [ ] 7.5 Telas de gestão completas

### Fase 8 — Notificações

**Critérios de conclusão:** emails enviados via outbox, retry funciona, Mailpit recebe.

- [ ] 8.1 OutboxMessage entity
- [ ] 8.2 OutboxProcessor (background worker)
- [ ] 8.3 Templates de email (confirmação, cancelamento, lembrete)
- [ ] 8.4 Integração Mailpit (desenvolvimento)
- [ ] 8.5 Testes de retry e idempotência

### Fase 9 — Hardening

**Critérios de conclusão:** RLS ativo, headers configurados, testes de segurança passam.

- [ ] 9.1 PostgreSQL Row-Level Security
- [ ] 9.2 Security headers (CSP, HSTS, X-Content-Type-Options)
- [ ] 9.3 Planos SaaS com limites no backend
- [ ] 9.4 Testes de segurança (IDOR, BOLA, CSRF, enumeração)
- [ ] 9.5 Threat model
- [ ] 9.6 Revisão de logs (sem dados sensíveis)
- [ ] 9.7 Análise de dependências

### Fase 10 — Documentação e Publicação

**Critérios de conclusão:** README completo, CI verde, todos os critérios de aceitação satisfeitos.

- [ ] 10.1 README voltado para recrutadores
- [ ] 10.2 docs/architecture.md
- [ ] 10.3 docs/deployment.md
- [ ] 10.4 docs/security.md + threat-model
- [ ] 10.5 docs/api-examples.http
- [ ] 10.6 ADRs
- [ ] 10.7 Revisão final (funcional, segurança, arquitetura, docs)
- [ ] 10.8 Seed de desenvolvimento
- [ ] 10.9 Screenshots reais

## Sequência de Commits Recomendada

```
chore: initialize solution and development environment
chore: add gitignore, dockerignore, and project scaffolding
feat: add domain entities for tenant and identity
feat: implement authentication with ASP.NET Core Identity
feat: add multi-tenant context and EF global filters
feat: add service and professional management
feat: implement availability rules and slot calculation
feat: add appointment booking with concurrency protection
feat: implement public booking flow
feat: add outbox and email notifications
feat: add dashboard metrics and appointment management
feat: implement SaaS plans with backend enforcement
feat: add PostgreSQL RLS for sensitive tables
feat: configure security headers and CSP
test: cover tenant isolation and booking concurrency
test: add security tests (IDOR, CSRF, authorization)
docs: add architecture, security, and deployment docs
docs: add ADRs for key decisions
chore: complete CI pipeline and dependency scanning
```

## Decisões Técnicas Principais

| Decisão | Escolha | Razão |
|---------|---------|-------|
| Arquitetura | Monólito modular | Sem requisito de escala que justifique microserviços |
| Auth | Cookies HttpOnly | Mais seguro que JWT em localStorage para SPA |
| Multi-tenancy | Schema compartilhado + TenantId | Operação simples, isolamento suficiente + RLS |
| ORM | EF Core + Npgsql | Padrão .NET, suporte nativo a PostgreSQL |
| Concorrência | Exclusion constraint + transação | Garantia no banco, não na aplicação |
| Timezone | NodaTime | Mais correto que DateTime para regras de horário |
| Notificações | Outbox pattern | Garante entrega sem depender de serviço externo |
| Frontend | Vite + React + TypeScript | Padrão atual, boa DX, sem lock-in |
