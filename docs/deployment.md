# Deployment — AgendaFlow

## Pré-requisitos

- Docker 24+
- Docker Compose v2

## Desenvolvimento local

```bash
# 1. Clonar e configurar ambiente
git clone <repo>
cd agendaflow
cp .env.example .env
# Editar .env com senhas locais de desenvolvimento

# 2. Subir todos os serviços
docker compose up --build

# 3. Acessar
# Frontend:    http://localhost:5173
# API:         http://localhost:8080
# Swagger:     http://localhost:8080/swagger
# Mailpit:     http://localhost:8025
```

## Migrations

As migrations são executadas automaticamente pelo EF Core quando a API inicia em Development.

Em produção, aplicar explicitamente antes do deploy:

```bash
dotnet ef database update \
  --project src/AgendaFlow.Infrastructure \
  --startup-project src/AgendaFlow.Api \
  --connection "Host=...;Username=agendaflow_migrator;Password=..."
```

## Variáveis de Ambiente (Produção)

| Variável | Descrição |
|----------|-----------|
| `ConnectionStrings__DefaultConnection` | Connection string com usuário app (não migrator) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Email__Host` | Servidor SMTP |
| `Email__Port` | Porta SMTP |
| `Email__Username` | Usuário SMTP |
| `Email__Password` | Senha SMTP |
| `Cors__AllowedOrigins__0` | URL do frontend em produção |

## HTTPS

Em produção, use um reverse proxy (nginx, Caddy, ou load balancer da nuvem) para terminar TLS.  
O contêiner da API escuta HTTP na porta 8080 — o TLS deve ser terminado antes.

## Health Checks

```
GET /health/live   → liveness (processo ativo)
GET /health/ready  → readiness (banco disponível)
```

## Rollback

O EF Core mantém histórico de migrations. Para reverter:

```bash
dotnet ef database update <MigrationAnterior> \
  --project src/AgendaFlow.Infrastructure \
  --startup-project src/AgendaFlow.Api
```

## Backup

Backup manual do PostgreSQL:

```bash
docker exec agendaflow-db \
  pg_dump -U postgres agendaflow | gzip > backup-$(date +%Y%m%d).sql.gz
```

Em produção: configure backups automáticos via serviço gerenciado ou cron job.

## Limitações Conhecidas

- Sem zero-downtime deploy nesta versão (requer orchestrator como Kubernetes).
- Backup automatizado não incluído — responsabilidade do operador.
- Secret manager (Key Vault etc.) não configurado — usar variáveis de ambiente no deploy inicial.
