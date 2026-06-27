# Segurança — AgendaFlow

Referência: OWASP ASVS nível 2 (sem certificação formal).

## Autenticação

| Controle | Implementação |
|----------|---------------|
| Hash de senha | ASP.NET Core Identity (PBKDF2 com salt) |
| Confirmação de e-mail | Obrigatória antes do primeiro login |
| Lockout | 5 tentativas → 15 min de bloqueio |
| Recuperação de senha | Token one-time com expiração, armazenado como hash |
| Anti-enumeração | Todas as respostas de auth retornam a mesma mensagem |
| Cookie | HttpOnly, SameSite=Strict, Secure (produção) |
| Expiração de sessão | 8 horas com sliding renewal |
| Invalidação | SecurityStamp atualizado após mudança de senha → sessões antigas expiram |

## Autorização

- **Deny by default**: endpoints protegidos por `[Authorize]`.
- **Tenant isolation**: TenantId não é aceito da requisição — apenas da sessão do servidor.
- **EF Core global filters** + **PostgreSQL RLS**: dois filtros independentes de isolamento por tenant.
- **Resource-level checks**: antes de retornar/editar um recurso, verifica se pertence ao tenant do usuário.
- **Retorna 404 em vez de 403** para recursos de outros tenants (evita enumeração de IDs).

## CSRF

Tokens antiforgery com header `X-XSRF-TOKEN`. O frontend solicita o token em `/api/antiforgery/token` após autenticação e o envia em todas as requisições mutantes (POST, PUT, PATCH, DELETE).

## Rate Limiting

| Endpoint | Limite | Janela |
|----------|--------|--------|
| Auth (login) | 5 req | 1 minuto por IP |
| Recuperação de senha | 3 req | 15 minutos por IP |
| Booking público | 10 req | 1 minuto por IP |
| API autenticada | 120 req | 1 minuto por usuário |

Resposta: HTTP 429 com `Retry-After`.

## Security Headers

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), geolocation=()`
- `Content-Security-Policy: default-src 'self'; ...`
- `Strict-Transport-Security` (produção apenas)

## Banco de Dados

- Usuário da aplicação (`agendaflow_app`) não possui tabelas.
- Migrations executadas por role separada (`agendaflow_migrator`).
- Row-Level Security habilitado e forçado nas tabelas sensíveis.
- Exclusion constraint para prevenir double-booking.
- Queries parametrizadas via EF Core (sem SQL concatenado).

## Segredos

- `.env` ignorado no git.
- `.env.example` contém apenas nomes e valores fictícios.
- Em produção: variáveis de ambiente ou secret manager.
- Nenhuma senha, token ou connection string real versionada.

## Logging

Campos **nunca** gravados em logs:
- Senhas e novos valores de senha
- Cookies completos
- Authorization headers
- Tokens de recuperação/confirmação
- Connection strings
- Payloads completos de autenticação

Correlação: toda requisição recebe um `CorrelationId` incluído nas respostas de erro e nos logs.

## Limitações Conhecidas

- MFA TOTP não implementado no MVP (roadmap).
- Sem IP blocking persistente (apenas rate limiting por janela).
- Backup do banco não automatizado nesta versão (documentado em deployment.md).
- Exportação de dados do tenant (LGPD) está no roadmap.

## Checklist de Publicação

- [ ] Rotacionar todas as senhas do `.env.example`
- [ ] Configurar HTTPS com certificado válido
- [ ] Remover `Include Error Detail=true` da connection string
- [ ] Verificar `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configurar HSTS
- [ ] Habilitar backup automático do banco
- [ ] Revisar CORS (somente origem de produção)
- [ ] Configurar secret manager (Azure Key Vault, AWS Secrets Manager, etc.)
- [ ] Verificar que logs não contêm dados sensíveis
- [ ] Executar `npm audit` e `dotnet list package --vulnerable`
