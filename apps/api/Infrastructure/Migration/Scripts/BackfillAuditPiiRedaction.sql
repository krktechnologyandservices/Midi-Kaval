-- BackfillAuditPiiRedaction.sql
-- ==============================
-- Purpose: Remove PII fields (beneficiaryName, beneficiaryAge, beneficiaryContact)
--          from existing audit_events metadata_json for case.merged events.
--
-- Run: psql -d <database_name> -f BackfillAuditPiiRedaction.sql
--
-- IMPORTANT:
-- 1. This script is OPTIONAL — existing audit events are append-only and the
--    code changes in Story 18.1 only affect NEW events.
-- 2. This script is IDEMPOTENT — safe to run multiple times on the same database.
--    The #- operator is a no-op if the specified path does not exist.
-- 3. Run against a production backup first to verify impact.
-- 4. The SELECT COUNT(*) queries report rows modified for audit trail.
-- 5. Wrapped in a transaction so before/after counts are consistent.
--
-- Target: case.merged events with draftSnapshot containing beneficiary PII fields.
-- What it does: Removes beneficiaryName, beneficiaryAge, beneficiaryContact
--               from the draftSnapshot key within metadata_json.
-- ==============================

BEGIN;

-- === STEP 1: Report rows with PII before migration ===
SELECT COUNT(*) AS rows_with_pii_before
FROM audit_events
WHERE event_type = 'case.merged'
  AND metadata_json IS NOT NULL
  AND metadata_json::jsonb ? 'draftSnapshot'
  AND (metadata_json::jsonb #>> '{draftSnapshot,beneficiaryName}' IS NOT NULL
    OR metadata_json::jsonb #>> '{draftSnapshot,beneficiaryAge}' IS NOT NULL
    OR metadata_json::jsonb #>> '{draftSnapshot,beneficiaryContact}' IS NOT NULL);

-- === STEP 2: Remove PII fields from draftSnapshot ===
UPDATE audit_events
SET metadata_json = metadata_json::jsonb
    #- '{draftSnapshot,beneficiaryName}'
    #- '{draftSnapshot,beneficiaryAge}'
    #- '{draftSnapshot,beneficiaryContact}'
WHERE event_type = 'case.merged'
  AND metadata_json IS NOT NULL
  AND metadata_json::jsonb ? 'draftSnapshot';

-- === STEP 3: Report rows with PII after migration (should be 0) ===
SELECT COUNT(*) AS rows_with_pii_after
FROM audit_events
WHERE event_type = 'case.merged'
  AND metadata_json IS NOT NULL
  AND metadata_json::jsonb ? 'draftSnapshot'
  AND (metadata_json::jsonb #>> '{draftSnapshot,beneficiaryName}' IS NOT NULL
    OR metadata_json::jsonb #>> '{draftSnapshot,beneficiaryAge}' IS NOT NULL
    OR metadata_json::jsonb #>> '{draftSnapshot,beneficiaryContact}' IS NOT NULL);

COMMIT;
