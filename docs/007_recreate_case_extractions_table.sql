-- Migration: Recreate case_extractions table for JavaScript rules engine
-- Date: 2026-02-11
-- Description: Drops and recreates case_extractions table with proper structure
--              for storing JavaScript extraction results

-- =============================================================================
-- IMPORTANT: Backup existing data before running this script!
-- =============================================================================
-- To backup existing data, run this first:
-- CREATE TABLE case_extractions_backup AS SELECT * FROM case_extractions;

-- =============================================================================
-- Step 1: Drop existing table and related objects
-- =============================================================================

-- Drop the existing view if it exists
BEGIN
    EXECUTE IMMEDIATE 'DROP VIEW v_extractions_by_source';
    DBMS_OUTPUT.PUT_LINE('Dropped view: v_extractions_by_source');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('View v_extractions_by_source does not exist, skipping...');
        ELSE
            RAISE;
        END IF;
END;
/

-- Drop the existing table (this will also drop all indexes and constraints)
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE case_extractions CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('Dropped table: case_extractions');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -942 THEN
            DBMS_OUTPUT.PUT_LINE('Table case_extractions does not exist, skipping...');
        ELSE
            RAISE;
        END IF;
END;
/

-- =============================================================================
-- Step 2: Create new case_extractions table
-- =============================================================================

CREATE TABLE case_extractions (
    -- Primary key
    ID                  VARCHAR2(100) PRIMARY KEY,

    -- Foreign keys
    CASE_ID             VARCHAR2(100) NOT NULL,
    AUDIT_LOG_ID        VARCHAR2(100) NOT NULL,
    RULE_ID             VARCHAR2(100) NOT NULL,

    -- Denormalized rule information (for audit trail)
    RULE_NAME           VARCHAR2(256) NOT NULL,

    -- Extraction type (from JavaScript {value, type, tags})
    -- For JavaScript: The 'type' field (e.g., "MSISDN", "IMEI", "IMSI", "ICCID")
    -- For Legacy: The regex pattern
    EXTRACTION_TYPE     VARCHAR2(100) NOT NULL,

    -- Source field where extraction occurred
    SOURCE_FIELD        VARCHAR2(100) NOT NULL,

    -- The actual extracted value (from JavaScript {value, type, tags})
    EXTRACTION_VALUE    VARCHAR2(1000) NOT NULL,

    -- Tags as JSON string (from JavaScript {value, type, tags})
    -- Format: "[]" or "[\"tag1\",\"tag2\"]"
    TAGS                VARCHAR2(4000),

    -- Timestamp
    EXTRACTED_AT        TIMESTAMP(6) DEFAULT SYSTIMESTAMP NOT NULL,

    -- Foreign key constraints
    CONSTRAINT fk_case_extractions_case
        FOREIGN KEY (CASE_ID) REFERENCES CASES(ID) ON DELETE CASCADE,

    CONSTRAINT fk_case_extractions_audit_log
        FOREIGN KEY (AUDIT_LOG_ID) REFERENCES AUDIT_LOGS(ID) ON DELETE CASCADE
);

-- =============================================================================
-- Step 3: Create indexes for performance
-- =============================================================================

-- Index on CASE_ID (most common lookup)
CREATE INDEX idx_case_extractions_case_id ON case_extractions(CASE_ID);

-- Index on AUDIT_LOG_ID
CREATE INDEX idx_case_extractions_audit_log ON case_extractions(AUDIT_LOG_ID);

-- Index on RULE_ID
CREATE INDEX idx_case_extractions_rule_id ON case_extractions(RULE_ID);

-- Index on EXTRACTION_TYPE (for filtering by type: MSISDN, IMEI, etc.)
CREATE INDEX idx_case_extractions_type ON case_extractions(EXTRACTION_TYPE);

-- Index on EXTRACTION_VALUE (for searching specific values)
CREATE INDEX idx_case_extractions_value ON case_extractions(EXTRACTION_VALUE);

-- Index on EXTRACTED_AT (for time-based queries)
CREATE INDEX idx_case_extractions_extracted ON case_extractions(EXTRACTED_AT);

-- Index on SOURCE_FIELD (for filtering by source)
CREATE INDEX idx_case_extractions_source ON case_extractions(SOURCE_FIELD);

-- Composite index for common query patterns
CREATE INDEX idx_case_extractions_type_value ON case_extractions(EXTRACTION_TYPE, EXTRACTION_VALUE);

-- =============================================================================
-- Step 4: Add table and column comments
-- =============================================================================

COMMENT ON TABLE case_extractions IS
'Stores extracted values from JavaScript rules engine.
Each extraction object {value, type, tags} returned by JavaScript is stored as one record.
Example: If JavaScript returns 6 MSISDN objects, 6 records are inserted.';

COMMENT ON COLUMN case_extractions.ID IS
'Primary key: extraction-{guid}. Unique identifier for each extraction record.';

COMMENT ON COLUMN case_extractions.CASE_ID IS
'Foreign key to cases.ID. Links extraction to its parent case.';

COMMENT ON COLUMN case_extractions.AUDIT_LOG_ID IS
'Foreign key to audit_logs.ID. Links extraction to the audit log that was processed.';

COMMENT ON COLUMN case_extractions.RULE_ID IS
'Rule identifier from RULES.ID (JavaScript rules engine).';

COMMENT ON COLUMN case_extractions.RULE_NAME IS
'Denormalized rule name from RULES.NAME for easy querying and audit trail.
Example: "Security Team", "Fraud Detection"';

COMMENT ON COLUMN case_extractions.EXTRACTION_TYPE IS
'Type of extraction from JavaScript {value, type, tags}.
Examples: "MSISDN", "IMEI", "IMSI", "ICCID", "IP", "CGI", "SITE ID", "CELL ID"';

COMMENT ON COLUMN case_extractions.SOURCE_FIELD IS
'The audit log field where the extraction occurred.
Examples: "sqlText", "bindVariables", "dbUser"';

COMMENT ON COLUMN case_extractions.EXTRACTION_VALUE IS
'The actual extracted value from JavaScript {value, type, tags}.
Examples: "9647508282748" (MSISDN), "353123456789012" (IMEI)';

COMMENT ON COLUMN case_extractions.TAGS IS
'Tags from JavaScript {value, type, tags} stored as JSON string.
Examples: "[]" (no tags), "[\"suspicious\"]", "[\"vip\",\"priority\"]"';

COMMENT ON COLUMN case_extractions.EXTRACTED_AT IS
'Timestamp when the extraction was performed by the consumer.';

-- =============================================================================
-- Step 5: Create helper view
-- =============================================================================

CREATE OR REPLACE VIEW v_extractions_summary AS
SELECT
    ce.ID,
    ce.CASE_ID,
    ce.AUDIT_LOG_ID,
    ce.RULE_ID,
    ce.RULE_NAME,
    ce.EXTRACTION_TYPE,
    ce.SOURCE_FIELD,
    ce.EXTRACTION_VALUE,
    ce.TAGS,
    ce.EXTRACTED_AT,
    -- Case information
    c.CASE_STATUS,
    c.VALID AS CASE_VALID,
    c.CREATED_AT AS CASE_CREATED_AT,
    -- Audit log information
    al.DB_USER,
    al.ACTION,
    al.OWNER,
    al.NAME AS OBJECT_NAME,
    al.RETURN_CODE,
    al.TIMESTAMP AS AUDIT_TIMESTAMP
FROM case_extractions ce
JOIN cases c ON ce.CASE_ID = c.ID
JOIN audit_logs al ON ce.AUDIT_LOG_ID = al.ID;

COMMENT ON TABLE v_extractions_summary IS
'Helper view showing all extractions with related case and audit log information.
Use this for easy querying without manual joins.';

-- =============================================================================
-- Step 6: Insert sample data for testing
-- =============================================================================

-- Sample data (requires existing case and audit_log)
-- Uncomment and adjust IDs if you want to insert test data

-- INSERT INTO case_extractions VALUES (
--     'extraction-sample-001',
--     'case-sample-001',         -- Must exist in cases table
--     'audit-sample-001',        -- Must exist in audit_logs table
--     'rule-sample-001',
--     'Security Team',
--     'MSISDN',
--     'sqlText',
--     '9647508282748',
--     '[]',
--     SYSTIMESTAMP
-- );
--
-- INSERT INTO case_extractions VALUES (
--     'extraction-sample-002',
--     'case-sample-001',
--     'audit-sample-001',
--     'rule-sample-001',
--     'Security Team',
--     'IMEI',
--     'sqlText',
--     '353123456789012',
--     '[\"suspicious\"]',
--     SYSTIMESTAMP
-- );

-- =============================================================================
-- Step 7: Verification queries
-- =============================================================================

-- Verify table structure
SELECT column_name, data_type, data_length, nullable
FROM user_tab_columns
WHERE table_name = 'CASE_EXTRACTIONS'
ORDER BY column_id;

-- Verify indexes
SELECT index_name, column_name, column_position
FROM user_ind_columns
WHERE table_name = 'CASE_EXTRACTIONS'
ORDER BY index_name, column_position;

-- Verify constraints
SELECT constraint_name, constraint_type, search_condition
FROM user_constraints
WHERE table_name = 'CASE_EXTRACTIONS';

-- Count records
SELECT COUNT(*) AS RECORD_COUNT FROM case_extractions;

COMMIT;

-- =============================================================================
-- Display completion message
-- =============================================================================

BEGIN
    DBMS_OUTPUT.PUT_LINE('===========================================');
    DBMS_OUTPUT.PUT_LINE('Migration 007 completed successfully!');
    DBMS_OUTPUT.PUT_LINE('Recreated table: case_extractions');
    DBMS_OUTPUT.PUT_LINE('Created view: v_extractions_summary');
    DBMS_OUTPUT.PUT_LINE('Created 8 indexes for performance');
    DBMS_OUTPUT.PUT_LINE('===========================================');
    DBMS_OUTPUT.PUT_LINE('');
    DBMS_OUTPUT.PUT_LINE('Table structure:');
    DBMS_OUTPUT.PUT_LINE('  ID                 VARCHAR2(100)   - Primary key');
    DBMS_OUTPUT.PUT_LINE('  CASE_ID            VARCHAR2(100)   - FK to cases');
    DBMS_OUTPUT.PUT_LINE('  AUDIT_LOG_ID       VARCHAR2(100)   - FK to audit_logs');
    DBMS_OUTPUT.PUT_LINE('  RULE_ID            VARCHAR2(100)   - Rule identifier');
    DBMS_OUTPUT.PUT_LINE('  RULE_NAME          VARCHAR2(256)   - Rule name');
    DBMS_OUTPUT.PUT_LINE('  EXTRACTION_TYPE    VARCHAR2(100)   - Type (MSISDN, IMEI, etc.)');
    DBMS_OUTPUT.PUT_LINE('  SOURCE_FIELD       VARCHAR2(100)   - Field name');
    DBMS_OUTPUT.PUT_LINE('  EXTRACTION_VALUE   VARCHAR2(1000)  - Extracted value');
    DBMS_OUTPUT.PUT_LINE('  TAGS               VARCHAR2(4000)  - JSON tags array');
    DBMS_OUTPUT.PUT_LINE('  EXTRACTED_AT       TIMESTAMP(6)    - Timestamp');
    DBMS_OUTPUT.PUT_LINE('===========================================');
END;
/
