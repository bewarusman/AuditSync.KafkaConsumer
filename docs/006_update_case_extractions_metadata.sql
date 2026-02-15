-- Migration: Update case_extractions table metadata for JavaScript rules engine
-- Date: 2026-02-11
-- Description: Updates table and column comments to reflect dual usage:
--              1. Legacy regex-based extractions (from target_rules)
--              2. New JavaScript-based extractions (from RULES table)

-- =============================================================================
-- Update Table and Column Comments
-- =============================================================================

-- Update table comment
COMMENT ON TABLE case_extractions IS
'Stores extracted values from both legacy regex rules (target_rules) and new JavaScript rules (RULES table).
Each extraction is linked to a case and contains denormalized rule information for audit trail.';

-- Update column comments
COMMENT ON COLUMN case_extractions.ID IS
'Primary key: extraction-{guid}';

COMMENT ON COLUMN case_extractions.CASE_ID IS
'Foreign key to cases.ID - the case this extraction belongs to';

COMMENT ON COLUMN case_extractions.AUDIT_LOG_ID IS
'Foreign key to audit_logs.ID - the audit log that was processed';

COMMENT ON COLUMN case_extractions.RULE_ID IS
'Rule identifier. For legacy: target_rules.ID. For JavaScript: RULES.ID';

COMMENT ON COLUMN case_extractions.RULE_NAME IS
'Denormalized rule name for easy querying and audit trail.
For legacy: target_rules.RULE_NAME. For JavaScript: RULES.NAME';

COMMENT ON COLUMN case_extractions.REGEX_PATTERN IS
'Denormalized pattern/description or extraction type.
For legacy: The actual regex pattern from target_rules.REGEX_PATTERN.
For JavaScript: The extraction type from JavaScript result (e.g., "MSISDN", "IMEI", "IMSI", "ICCID")';

COMMENT ON COLUMN case_extractions.SOURCE_FIELD IS
'The field where the value was extracted from.
For legacy: target_rules.SOURCE_FIELD (e.g., "SqlText", "BindVariables").
For JavaScript: The condition field that triggered extraction (e.g., "sqlText", "bindVariables")';

COMMENT ON COLUMN case_extractions.FIELD_VALUE IS
'The actual extracted value (e.g., "9647508282748", "353123456789012").
This is the sensitive data that was detected in the audit log.';

COMMENT ON COLUMN case_extractions.EXTRACTED_AT IS
'Timestamp when the extraction was performed by the consumer';

COMMENT ON COLUMN case_extractions.TAGS IS
'Tags associated with this extraction.
For legacy: Comma-separated string (e.g., "VIP,Priority") from automatic tag evaluation.
For JavaScript: JSON array as string (e.g., "[]" or "[\"suspicious\",\"vip\"]") returned by JavaScript extraction logic.';

-- =============================================================================
-- Add helpful view for querying extractions by source
-- =============================================================================

CREATE OR REPLACE VIEW v_extractions_by_source AS
SELECT
    ce.ID,
    ce.CASE_ID,
    ce.AUDIT_LOG_ID,
    ce.RULE_ID,
    ce.RULE_NAME,
    ce.SOURCE_FIELD,
    ce.FIELD_VALUE,
    ce.TAGS,
    ce.EXTRACTED_AT,
    -- Determine extraction source
    CASE
        WHEN ce.REGEX_PATTERN LIKE 'JavaScript%' THEN 'JavaScript Rules Engine'
        ELSE 'Legacy Regex Engine'
    END AS EXTRACTION_SOURCE,
    -- Case information
    c.CASE_STATUS,
    c.VALID AS CASE_VALID,
    c.CREATED_AT AS CASE_CREATED_AT,
    -- Audit log information
    al.DB_USER,
    al.ACTION,
    al.OWNER,
    al.NAME AS OBJECT_NAME,
    al.SQL_TEXT,
    al.TIMESTAMP AS AUDIT_TIMESTAMP
FROM case_extractions ce
JOIN cases c ON ce.CASE_ID = c.ID
JOIN audit_logs al ON ce.AUDIT_LOG_ID = al.ID;

COMMENT ON TABLE v_extractions_by_source IS
'Helper view that shows all extractions with their source (JavaScript vs Legacy) and related case/audit log information';

-- =============================================================================
-- Create indexes if they don't exist (safe execution)
-- =============================================================================

-- Check and create index on REGEX_PATTERN for filtering by extraction source
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM user_indexes
    WHERE index_name = 'IDX_CASE_EXTRACTIONS_PATTERN';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX idx_case_extractions_pattern ON case_extractions(REGEX_PATTERN)';
        DBMS_OUTPUT.PUT_LINE('Created index: idx_case_extractions_pattern');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index idx_case_extractions_pattern already exists');
    END IF;
END;
/

-- Check and create index on SOURCE_FIELD for filtering
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM user_indexes
    WHERE index_name = 'IDX_CASE_EXTRACTIONS_SOURCE';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE INDEX idx_case_extractions_source ON case_extractions(SOURCE_FIELD)';
        DBMS_OUTPUT.PUT_LINE('Created index: idx_case_extractions_source');
    ELSE
        DBMS_OUTPUT.PUT_LINE('Index idx_case_extractions_source already exists');
    END IF;
END;
/

-- =============================================================================
-- Sample queries for reference
-- =============================================================================

-- Query 1: Count extractions by source
-- SELECT
--     CASE
--         WHEN REGEX_PATTERN LIKE 'JavaScript%' THEN 'JavaScript Rules Engine'
--         ELSE 'Legacy Regex Engine'
--     END AS EXTRACTION_SOURCE,
--     COUNT(*) AS EXTRACTION_COUNT
-- FROM case_extractions
-- GROUP BY CASE
--     WHEN REGEX_PATTERN LIKE 'JavaScript%' THEN 'JavaScript Rules Engine'
--     ELSE 'Legacy Regex Engine'
-- END;

-- Query 2: Recent JavaScript extractions
-- SELECT
--     ce.RULE_NAME,
--     ce.SOURCE_FIELD,
--     ce.FIELD_VALUE,
--     ce.TAGS,
--     al.DB_USER,
--     ce.EXTRACTED_AT
-- FROM case_extractions ce
-- JOIN audit_logs al ON ce.AUDIT_LOG_ID = al.ID
-- WHERE ce.REGEX_PATTERN LIKE 'JavaScript%'
-- ORDER BY ce.EXTRACTED_AT DESC
-- FETCH FIRST 20 ROWS ONLY;

-- Query 3: Using the view
-- SELECT
--     EXTRACTION_SOURCE,
--     RULE_NAME,
--     FIELD_VALUE,
--     TAGS,
--     DB_USER,
--     CASE_STATUS
-- FROM v_extractions_by_source
-- WHERE EXTRACTED_AT > SYSTIMESTAMP - INTERVAL '1' DAY
-- ORDER BY EXTRACTED_AT DESC;

COMMIT;

-- Display completion message
BEGIN
    DBMS_OUTPUT.PUT_LINE('===========================================');
    DBMS_OUTPUT.PUT_LINE('Migration 006 completed successfully!');
    DBMS_OUTPUT.PUT_LINE('Updated case_extractions table metadata');
    DBMS_OUTPUT.PUT_LINE('Created view: v_extractions_by_source');
    DBMS_OUTPUT.PUT_LINE('===========================================');
END;
/
