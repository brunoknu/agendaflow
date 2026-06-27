-- Migration: Add exclusion constraint to prevent overlapping appointments
-- This runs after EF Core creates the appointments table.
-- It is the definitive guard against double-booking — application-level checks are not enough.

-- Install the btree_gist extension (required for exclusion constraints on non-range types)
CREATE EXTENSION IF NOT EXISTS btree_gist;

-- Active statuses that occupy the professional's calendar:
--   0 = PendingConfirmation, 1 = Confirmed, 2 = CheckedIn
-- Cancelled (4) and NoShow (5) do NOT block the schedule.
-- Completed (3) does NOT need blocking (appointment is past).
--
-- Using tstzrange with half-open interval [blocked_start, blocked_end) so that
-- two adjacent appointments (one ends exactly when the next begins) do not conflict.

ALTER TABLE appointments
  ADD CONSTRAINT no_overlapping_appointments
  EXCLUDE USING gist (
    tenant_id WITH =,
    professional_id WITH =,
    tstzrange(blocked_start_at_utc, blocked_end_at_utc, '[)') WITH &&
  )
  WHERE (status IN (0, 1, 2));  -- PendingConfirmation, Confirmed, CheckedIn

-- Index to support the constraint and range queries
CREATE INDEX IF NOT EXISTS idx_appointments_professional_time
  ON appointments USING gist (
    tenant_id,
    professional_id,
    tstzrange(blocked_start_at_utc, blocked_end_at_utc, '[)')
  )
  WHERE status IN (0, 1, 2);

COMMENT ON CONSTRAINT no_overlapping_appointments ON appointments IS
  'Prevents two active appointments for the same professional from occupying overlapping time slots. '
  'Uses PostgreSQL exclusion constraint with tstzrange and btree_gist. '
  'Half-open intervals [start, end) allow back-to-back appointments without conflict.';
