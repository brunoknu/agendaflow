# ADR-003: Prevenção de sobreposição de agendamentos via exclusion constraint

**Data:** 2025-06

## Contexto

Duas requisições concorrentes podem verificar disponibilidade ao mesmo tempo, encontrar o horário livre e ambas criarem um agendamento — resultando em double-booking. A verificação na aplicação (read-then-write) não é suficiente.

## Decisão

Usar uma `EXCLUSION CONSTRAINT` do PostgreSQL com `btree_gist` e `tstzrange`:

```sql
ALTER TABLE appointments
  ADD CONSTRAINT no_overlapping_appointments
  EXCLUDE USING gist (
    tenant_id WITH =,
    professional_id WITH =,
    tstzrange(blocked_start_at_utc, blocked_end_at_utc, '[)') WITH &&
  )
  WHERE (status IN (0, 1, 2));
```

A criação do agendamento acontece dentro de uma transação. Se duas transações concorrentes tentarem inserir linhas conflitantes, o PostgreSQL garante que apenas uma terá sucesso — a outra receberá um erro de constraint que é traduzido para HTTP 409.

## Alternativas consideradas

**Pessimistic locking (SELECT FOR UPDATE):** Funcionaria, mas exige bloquear todo o intervalo e é complexo de implementar corretamente com slots variáveis.

**Verificação na aplicação + retry:** Sem garantia no banco, ainda sujeito a race condition entre a verificação e o insert.

**Distributed lock (Redis):** Adiciona dependência de infra e complexidade sem necessidade quando o banco já resolve.

## Consequências

- Garantia de exclusão mútua no banco, independente de quantas réplicas da API existam.
- Intervalo half-open `[start, end)` permite agendamentos adjacentes (um começa quando o anterior termina).
- A constraint filtra por status (apenas agendamentos ativos bloqueiam). Cancelamentos liberam o slot automaticamente.
- A aplicação captura a `DbUpdateException` e retorna 409 com Problem Details sem expor detalhes do banco.
