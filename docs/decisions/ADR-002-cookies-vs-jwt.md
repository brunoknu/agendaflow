# ADR-002: Autenticação com cookies HttpOnly em vez de JWT

**Data:** 2025-06

## Contexto

A aplicação React precisa de autenticação. As opções principais são cookies HttpOnly gerenciados pelo servidor ou JWT armazenado no cliente (localStorage ou memory).

## Decisão

Utilizar ASP.NET Core Identity com cookies HttpOnly + SameSite=Strict.

## Alternativas consideradas

**JWT em localStorage:** Vulnerável a XSS — qualquer script malicioso na página consegue ler e enviar o token. Rejeitado por razão de segurança.

**JWT em cookie HttpOnly:** Funcionaria, mas adiciona complexidade de rotação de tokens e revogação. Cookies de sessão do Identity já fazem isso nativamente com SecurityStamp.

**JWT em memória (sem localStorage):** Perde o token ao recarregar a página, exigindo silent refresh. Complexidade sem benefício real para uma SPA hospedada na mesma origem que a API.

## Consequências

- Cookies HttpOnly não são acessíveis por JavaScript — XSS não consegue roubar sessão.
- CSRF é necessário para requisições mutantes — implementado com antiforgery token em header.
- Frontend e API precisam estar na mesma origem em produção (ou configuração cuidadosa de CORS+credenciais).
- Invalidação de sessão é trivial: SecurityStamp muda e todas as sessões expiram imediatamente.
- Sem necessidade de armazenar tokens no cliente.
