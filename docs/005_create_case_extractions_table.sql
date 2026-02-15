-- Migration: Add Rules Engine Support
-- Date: 2026-02-11
-- Description: Adds RULES table for JavaScript-based rules engine
-- Note: Uses existing case_extractions table for storing extraction results

-- =============================================================================
-- Table: RULES - Stores rules with conditions and actions
-- =============================================================================
CREATE TABLE RULES (
    ID               VARCHAR2(100) PRIMARY KEY,        -- Format: rule-{guid}
    TARGET_ID        VARCHAR2(100) NOT NULL,           -- FK to targets.ID
    NAME             VARCHAR2(256) NOT NULL,           -- Rule name
    DESCRIPTION      VARCHAR2(1000),                   -- Rule description
    ENABLED          NUMBER(1,0) DEFAULT 1,            -- 1=enabled, 0=disabled
    CONDITIONS       CLOB,                             -- JSON array of conditions
    ACTIONS          CLOB,                             -- JSON object of actions
    ORDER_POSITION   NUMBER NOT NULL,                  -- Lower = higher priority
    CREATED_AT       TIMESTAMP(6) DEFAULT SYSTIMESTAMP,
    UPDATED_AT       TIMESTAMP(6) DEFAULT SYSTIMESTAMP,
    CONSTRAINT fk_rules_target
        FOREIGN KEY (TARGET_ID) REFERENCES TARGETS(ID) ON DELETE CASCADE,
    CONSTRAINT chk_rules_enabled CHECK (ENABLED IN (0, 1))
);

-- Indexes for RULES
CREATE INDEX idx_rules_target_id ON RULES(TARGET_ID);
CREATE INDEX idx_rules_enabled ON RULES(ENABLED);
CREATE INDEX idx_rules_order ON RULES(TARGET_ID, ORDER_POSITION);

-- Comments
COMMENT ON TABLE RULES IS 'Rules engine rules with JavaScript extraction support';
COMMENT ON COLUMN RULES.ID IS 'Primary key: rule-{guid}';
COMMENT ON COLUMN RULES.CONDITIONS IS 'JSON array of conditions to evaluate';
COMMENT ON COLUMN RULES.ACTIONS IS 'JSON object with actions (createCase, notifyChannels)';
COMMENT ON COLUMN RULES.ORDER_POSITION IS 'Evaluation order - lower numbers first';

COMMIT;

-- =============================================================================
-- NOTE: Extraction results are stored in the existing case_extractions table
-- =============================================================================
-- The existing case_extractions table structure:
--   ID VARCHAR2(100) PRIMARY KEY
--   CASE_ID VARCHAR2(100) NOT NULL
--   AUDIT_LOG_ID VARCHAR2(100) NOT NULL
--   RULE_ID VARCHAR2(100) NOT NULL
--   RULE_NAME VARCHAR2(100) NOT NULL
--   REGEX_PATTERN VARCHAR2(1000) NOT NULL
--   SOURCE_FIELD VARCHAR2(100) NOT NULL
--   FIELD_VALUE VARCHAR2(4000)
--   EXTRACTED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
--   TAGS VARCHAR2(4000)  -- Added by migration 004_add_tagging_support.sql
--
-- For JavaScript rules engine extractions, we'll populate:
--   RULE_NAME = "JavaScript Rules Engine" (or the actual rule name)
--   SOURCE_FIELD = The field that triggered extraction (e.g., "sqlText")
--   FIELD_VALUE = The extracted value
--   TAGS = JSON array of tags (comma-separated or JSON string)
