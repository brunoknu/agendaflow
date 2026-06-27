-- Row-Level Security (RLS) for AgendaFlow
-- Applied to sensitive tenant-scoped tables after EF Core migrations.
-- The application role (agendaflow_app) is NOT a table owner, so RLS is enforced.

-- ── Helper function ────────────────────────────────────────────
-- Returns the current tenant ID from the session/transaction context.
-- Fails with NULL if not set, causing no rows to be visible (secure default).

CREATE OR REPLACE FUNCTION app_current_tenant_id() RETURNS uuid
  LANGUAGE sql STABLE
  AS $$
    SELECT NULLIF(current_setting('app.current_tenant_id', TRUE), '')::uuid
  $$;

-- ── Enable and force RLS on tenant-scoped tables ───────────────
-- Tables the application user can read/write that need tenant isolation.

DO $$
DECLARE
  t text;
BEGIN
  FOREACH t IN ARRAY ARRAY[
    'appointments',
    'appointment_status_history',
    'availability_exceptions',
    'availability_rules',
    'booking_confirmations',
    'customers',
    'professionals',
    'professional_services',
    'services',
    'tenant_memberships'
  ]
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    -- Force RLS even for table owner (extra safety)
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', t);
  END LOOP;
END
$$;

-- ── RLS policies ──────────────────────────────────────────────
-- One policy per table: only rows belonging to the current tenant are visible.
-- If app.current_tenant_id is not set, app_current_tenant_id() returns NULL
-- and no rows match (NULL = anything is NULL/false).

CREATE POLICY tenant_isolation ON appointments
  USING (tenant_id = app_current_tenant_id());

CREATE POLICY tenant_isolation ON appointment_status_history
  USING (tenant_id = app_current_tenant_id());

CREATE POLICY tenant_isolation ON availability_exceptions
  USING (tenant_id = app_current_tenant_id());

CREATE POLICY tenant_isolation ON availability_rules
  USING (tenant_id = app_current_tenant_id());

CREATE POLICY tenant_isolation ON booking_confirmations
  USING (tenant_id = app_current_tenant_id());

CREATE POLICY tenant_isolation ON customers
  USING (tenant_id = app_current_tenant_id());

CREATE POLICY tenant_isolation ON professionals
  USING (tenant_id = app_current_tenant_id());

CREATE POLICY tenant_isolation ON professional_services
  USING (
    professional_id IN (
      SELECT id FROM professionals WHERE tenant_id = app_current_tenant_id()
    )
  );

CREATE POLICY tenant_isolation ON services
  USING (tenant_id = app_current_tenant_id());

CREATE POLICY tenant_isolation ON tenant_memberships
  USING (tenant_id = app_current_tenant_id());

-- ── Note ──────────────────────────────────────────────────────
-- The application sets the context before each operation:
--   SET LOCAL app.current_tenant_id = '<uuid>';
-- This must happen inside a transaction so the LOCAL setting is transaction-scoped
-- and cannot leak between requests or connections in the pool.
-- See: Infrastructure/Persistence/AppDbContext.cs for implementation.
