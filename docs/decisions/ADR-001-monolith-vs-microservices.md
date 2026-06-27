# ADR-001: Monólito modular em vez de microserviços

**Data:** 2025-06

## Contexto

O sistema precisa gerenciar tenants, serviços, profissionais, disponibilidade e agendamentos. A equipe é pequena. O produto está no início do ciclo de vida e os requisitos ainda evoluem. Não há SLA que exija escala independente de componentes.

## Decisão

Utilizar um monólito modular com separação clara de responsabilidades entre Domain, Application, Infrastructure e Api.

## Alternativas consideradas

**Microserviços:** Cada domínio (tenants, agendamentos, notificações) seria um serviço independente. Rejeitado porque:
- Adiciona complexidade operacional (service discovery, distributed tracing, múltiplos deploys) sem benefício real na escala atual.
- Transações distribuídas para criação de agendamentos tornariam a garantia de consistência muito mais difícil.
- Sem requisito de escala independente que justifique o custo.

## Consequências

- Implantação simples: um contêiner da API, um do banco.
- Transações locais garantem consistência dos agendamentos.
- Se no futuro houver requisito de escala independente (ex.: workers de notificação com pico diferente), a separação modular existente facilita extrair um serviço específico.
