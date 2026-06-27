-- AgendaFlow — Database Initialization
-- Runs once when the PostgreSQL container starts for the first time.
-- Migrations (via EF Core) are applied by the API on startup.

-- ── Application user (least privilege) ────────────────────────
-- The app user does NOT own the tables (postgres does).
-- This is required for Row-Level Security to work correctly:
-- table owners bypass RLS by default, so the app must use a non-owner role.

DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'agendaflow_app') THEN
    CREATE ROLE agendaflow_app LOGIN PASSWORD 'dev_app_password';
  END IF;
END
$$;

-- Migration runner (EF Core needs to own and alter tables)
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'agendaflow_migrator') THEN
    CREATE ROLE agendaflow_migrator LOGIN PASSWORD 'dev_migrator_password' CREATEROLE;
  END IF;
END
$$;

-- Grant database access
GRANT CONNECT ON DATABASE agendaflow TO agendaflow_app;
GRANT CONNECT ON DATABASE agendaflow TO agendaflow_migrator;

-- Future tables created by migrator will be accessible to app user
ALTER DEFAULT PRIVILEGES FOR ROLE agendaflow_migrator IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO agendaflow_app;

ALTER DEFAULT PRIVILEGES FOR ROLE agendaflow_migrator IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO agendaflow_app;

-- Schema access
GRANT USAGE ON SCHEMA public TO agendaflow_app;
GRANT USAGE ON SCHEMA public TO agendaflow_migrator;

-- ── Row-Level Security setup ────────────────────────────────────
-- RLS is enabled per-table in migrations after table creation.
-- The application sets the tenant context before queries:
--   SET LOCAL app.current_tenant_id = '<uuid>';
-- RLS policies then filter rows by this value.
-- See: docs/security.md for full RLS documentation.
