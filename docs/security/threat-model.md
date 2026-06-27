# Threat Model — AgendaFlow

## Ativos

| Ativo | Descrição | Criticidade |
|-------|-----------|-------------|
| Dados de clientes | Nome, e-mail, telefone, histórico de agendamentos | Alta |
| Dados de negócio | Serviços, preços, disponibilidade, profissionais | Média |
| Sessões de usuário | Cookies de autenticação | Alta |
| Tokens de confirmação | Links de confirm/cancel de agendamento | Alta |
| Credenciais do banco | Connection string com usuário/senha | Crítica |
| Isolamento multi-tenant | Dados de um tenant não acessíveis por outro | Crítica |

## Atores

| Ator | Descrição |
|------|-----------|
| Cliente público | Usuário anônimo que acessa a página de agendamento |
| Owner/Manager/Staff | Usuário autenticado com papel na empresa |
| PlatformAdmin | Administrador da plataforma |
| Atacante externo | Tenta explorar vulnerabilidades via HTTP |
| Atacante interno | Usuário legítimo de um tenant tentando acessar dados de outro |

## Pontos de Entrada

- Endpoints públicos: `/api/public/{slug}/*`
- Endpoints autenticados: `/api/*`
- Links de e-mail: `/agendamento/confirmar?token=`, `/agendamento/cancelar?token=`
- Painel admin: `/app/*`

## Fronteiras de Confiança

1. Internet → API (HTTPS, rate limiting, input validation)
2. API → Banco (conexão criptografada, RLS, least privilege)
3. Tenant A → Tenant B (TenantContext + EF filters + RLS)
4. Staff → Owner (autorização por papel)

## Principais Ameaças e Controles

| Ameaça | Controle |
|--------|---------|
| IDOR — acessar dados de outro tenant por ID | TenantContext + EF filters + RLS + retornar 404 |
| XSS roubar cookie de sessão | Cookie HttpOnly (JS não consegue ler) |
| CSRF realizar ação em nome do usuário | Antiforgery token em header X-XSRF-TOKEN |
| Brute force de senha | Rate limiting + lockout 5 tentativas / 15 min |
| Enumeração de usuários | Respostas idênticas para email existente/inexistente |
| Double-booking via requisição concorrente | Exclusion constraint PostgreSQL + transação |
| Token de confirmação reutilizado | one-time flag + hash SHA-256 (nunca texto puro) |
| Escalonamento de privilégio (Staff → Owner) | Autorização por política, não por frontend |
| SQL Injection | EF Core com queries parametrizadas |
| Credenciais expostas | .env ignorado, example com valores fictícios |

## Riscos Residuais

- **Comprometimento do servidor**: se o servidor for comprometido, todos os dados ficam expostos. Mitigação: backups, monitoramento, updates.
- **E-mail como vetor de phishing**: links de confirmação enviados por e-mail. Mitigação: expiração em 48h, one-time use, HTTPS.
- **Ataques de timing em comparação de tokens**: tokens comparados via hash (sem timing side-channel).
- **Vazamento de dados por logs**: política de logging documentada, sem PII em logs técnicos.
