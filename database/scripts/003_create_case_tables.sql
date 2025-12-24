-- Migration Script: Create Case and Case Extraction Tables
-- Description: Implements case-based extraction architecture
-- Date: 2025-12-23
-- Phase 1: Database Schema Changes

-- =============================================================================
-- Table 1: cases
-- Purpose: Store cases created when extraction rules successfully extract values
-- =============================================================================
CREATE TABLE cases (
    ID VARCHAR2(100) PRIMARY KEY,
    AUDIT_LOG_ID VARCHAR2(100) NOT NULL,
    CASE_STATUS VARCHAR2(50) DEFAULT 'OPEN',
    VALID VARCHAR2(3) DEFAULT NULL,  -- 'YES', 'NO', or NULL (defaults to NULL when case is created)
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    RESOLVED_AT TIMESTAMP,
    RESOLVED_BY VARCHAR2(128),
    RESOLUTION_NOTES VARCHAR2(4000),
    CONSTRAINT FK_CASE_AUDIT_LOG FOREIGN KEY (AUDIT_LOG_ID)
        REFERENCES audit_logs(ID) ON DELETE CASCADE,
    CONSTRAINT CHK_CASE_STATUS CHECK (CASE_STATUS IN ('OPEN', 'RESOLVED', 'ASSIGNED')),
    CONSTRAINT CHK_VALID CHECK (VALID IN ('YES', 'NO') OR VALID IS NULL),
    CONSTRAINT UK_CASES_AUDIT_LOG UNIQUE (AUDIT_LOG_ID)  -- One case per audit_log
);

-- Indexes for cases
CREATE INDEX IDX_CASES_STATUS ON cases(CASE_STATUS);
CREATE INDEX IDX_CASES_VALID ON cases(VALID);
CREATE INDEX IDX_CASES_CREATED_AT ON cases(CREATED_AT);

-- =============================================================================
-- Table 2: case_extractions
-- Purpose: Store extracted values ONLY for cases with denormalized rule info
-- =============================================================================
CREATE TABLE case_extractions (
    ID VARCHAR2(100) PRIMARY KEY,
    CASE_ID VARCHAR2(100) NOT NULL,
    AUDIT_LOG_ID VARCHAR2(100) NOT NULL,
    RULE_ID VARCHAR2(100) NOT NULL,
    RULE_NAME VARCHAR2(100) NOT NULL,        -- Denormalized from target_rules for easy querying
    REGEX_PATTERN VARCHAR2(1000) NOT NULL,   -- Denormalized from target_rules for audit trail
    SOURCE_FIELD VARCHAR2(100) NOT NULL,     -- Denormalized from target_rules (where value was extracted from)
    FIELD_VALUE VARCHAR2(4000),              -- The actual extracted value
    EXTRACTED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT FK_EXTRACTION_CASE FOREIGN KEY (CASE_ID)
        REFERENCES cases(ID) ON DELETE CASCADE,
    CONSTRAINT FK_EXTRACTION_AUDIT_LOG FOREIGN KEY (AUDIT_LOG_ID)
        REFERENCES audit_logs(ID) ON DELETE CASCADE,
    CONSTRAINT FK_EXTRACTION_RULE FOREIGN KEY (RULE_ID)
        REFERENCES target_rules(ID) ON DELETE CASCADE
);

-- Indexes for case_extractions
CREATE INDEX IDX_EXTRACTIONS_CASE_ID ON case_extractions(CASE_ID);
CREATE INDEX IDX_EXTRACTIONS_AUDIT_LOG ON case_extractions(AUDIT_LOG_ID);
CREATE INDEX IDX_EXTRACTIONS_RULE_ID ON case_extractions(RULE_ID);
CREATE INDEX IDX_EXTRACTIONS_RULE_NAME ON case_extractions(RULE_NAME);
CREATE INDEX IDX_EXTRACTIONS_VALUE ON case_extractions(FIELD_VALUE);
CREATE INDEX IDX_EXTRACTIONS_NAME_VALUE ON case_extractions(RULE_NAME, FIELD_VALUE);

-- =============================================================================
-- Verification
-- =============================================================================
-- Verify tables created
SELECT 'cases' as table_name, COUNT(*) as row_count FROM cases
UNION ALL
SELECT 'case_extractions', COUNT(*) FROM case_extractions;

COMMIT;
