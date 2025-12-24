-- ============================================================================
-- AuditSync Oracle Consumer - Complete Database Recreation Script
-- ============================================================================
-- Description: Drops all existing tables and recreates the complete schema
-- WARNING: This script will DELETE ALL DATA in the following tables:
--   - case_extractions
--   - cases
--   - target_rules
--   - targets
--   - audit_logs
--   - audit_log_extracted_values (old table if exists)
--
-- Use this script for:
--   - Initial setup
--   - Development environment reset
--   - Migration from old schema to new case-based architecture
--
-- IMPORTANT:
--   - Backup your data before running this script!
--   - Run this script as the schema owner
--   - Ensure no applications are connected
--
-- Compatibility:
--   - Oracle Database 11g and higher
--   - Tested on Oracle 11g, 12c, 19c, 21c
--   - No Oracle 12c+ specific features used
--
-- Date: 2025-12-23
-- Version: 1.0 (Case-Based Architecture)
-- Oracle 11g Compatible: YES
-- ============================================================================

SET ECHO ON
SET FEEDBACK ON
SET SERVEROUTPUT ON

-- ============================================================================
-- SECTION 1: DROP ALL EXISTING TABLES
-- ============================================================================
-- Note: Tables are dropped in reverse dependency order to avoid FK constraint errors
-- Indices and constraints are automatically dropped with their tables

PROMPT ========================================
PROMPT Dropping existing tables...
PROMPT ========================================

-- Drop dependent tables first
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE case_extractions CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('✓ Dropped table: case_extractions');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -942 THEN -- Table does not exist
            RAISE;
        ELSE
            DBMS_OUTPUT.PUT_LINE('  Table case_extractions does not exist, skipping...');
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE cases CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('✓ Dropped table: cases');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -942 THEN
            RAISE;
        ELSE
            DBMS_OUTPUT.PUT_LINE('  Table cases does not exist, skipping...');
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE target_rules CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('✓ Dropped table: target_rules');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -942 THEN
            RAISE;
        ELSE
            DBMS_OUTPUT.PUT_LINE('  Table target_rules does not exist, skipping...');
        END IF;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE targets CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('✓ Dropped table: targets');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -942 THEN
            RAISE;
        ELSE
            DBMS_OUTPUT.PUT_LINE('  Table targets does not exist, skipping...');
        END IF;
END;
/

-- Drop old tables from previous architecture (if they exist)
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE audit_log_extracted_values CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('✓ Dropped old table: audit_log_extracted_values');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -942 THEN
            RAISE;
        ELSE
            DBMS_OUTPUT.PUT_LINE('  Table audit_log_extracted_values does not exist, skipping...');
        END IF;
END;
/

-- Drop audit_logs last
BEGIN
    EXECUTE IMMEDIATE 'DROP TABLE audit_logs CASCADE CONSTRAINTS';
    DBMS_OUTPUT.PUT_LINE('✓ Dropped table: audit_logs');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -942 THEN
            RAISE;
        ELSE
            DBMS_OUTPUT.PUT_LINE('  Table audit_logs does not exist, skipping...');
        END IF;
END;
/

PROMPT
PROMPT ✓ All existing tables dropped successfully
PROMPT

-- ============================================================================
-- SECTION 2: CREATE ALL TABLES
-- ============================================================================
-- Note: Tables are created in dependency order

PROMPT ========================================
PROMPT Creating new tables...
PROMPT ========================================

-- ----------------------------------------------------------------------------
-- Table 1: audit_logs
-- ----------------------------------------------------------------------------
-- Purpose: Store complete audit messages from Kafka
-- Note: Uses MERGE (upsert) logic to prevent duplicates
--       If same ID arrives again, updates ALL fields + increments PROCESS_COUNTER
-- ----------------------------------------------------------------------------

PROMPT Creating table: audit_logs...

CREATE TABLE audit_logs (
    ID VARCHAR2(100) PRIMARY KEY,
    TARGET VARCHAR2(256),
    SESSION_ID NUMBER,
    ENTRY_ID NUMBER,
    STATEMENT NUMBER,
    DB_USER VARCHAR2(128),
    USER_HOST VARCHAR2(256),
    TERMINAL VARCHAR2(128),
    OS_USER VARCHAR2(128),
    ACTION NUMBER,
    RETURN_CODE NUMBER,
    OWNER VARCHAR2(128),
    NAME VARCHAR2(128),
    AUTH_PRIVILEGES VARCHAR2(256),
    AUTH_GRANTEE VARCHAR2(128),
    NEW_OWNER VARCHAR2(128),
    NEW_NAME VARCHAR2(128),
    PRIVILEGE_USED VARCHAR2(256),
    TEXT CLOB,
    BIND_VARIABLES CLOB,
    TIMESTAMP TIMESTAMP,
    PRODUCED_AT TIMESTAMP,
    KAFKA_PARTITION NUMBER,
    KAFKA_OFFSET NUMBER,
    PROCESS_COUNTER NUMBER DEFAULT 1,
    PROCESSED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSUMED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT UK_AUDIT_OFFSET UNIQUE (KAFKA_PARTITION, KAFKA_OFFSET)
);

PROMPT ✓ Table audit_logs created

-- Create indexes for audit_logs
PROMPT Creating indexes for audit_logs...

CREATE INDEX IDX_AUDIT_SESSION ON audit_logs(SESSION_ID, ENTRY_ID);
CREATE INDEX IDX_AUDIT_PROCESSED_AT ON audit_logs(PROCESSED_AT);
CREATE INDEX IDX_AUDIT_DB_USER ON audit_logs(DB_USER);
CREATE INDEX IDX_AUDIT_TARGET ON audit_logs(TARGET);
CREATE INDEX IDX_AUDIT_PROCESS_COUNTER ON audit_logs(PROCESS_COUNTER);

PROMPT ✓ Indexes for audit_logs created
PROMPT

-- ----------------------------------------------------------------------------
-- Table 2: targets
-- ----------------------------------------------------------------------------
-- Purpose: Store target information (databases being audited)
-- ----------------------------------------------------------------------------

PROMPT Creating table: targets...

CREATE TABLE targets (
    ID VARCHAR2(100) PRIMARY KEY,
    NAME VARCHAR2(256) NOT NULL UNIQUE,
    DESCRIPTION VARCHAR2(1000),
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
);

PROMPT ✓ Table targets created

-- Create indexes for targets
PROMPT Creating indexes for targets...

CREATE INDEX IDX_TARGETS_NAME ON targets(NAME);

PROMPT ✓ Indexes for targets created
PROMPT

-- ----------------------------------------------------------------------------
-- Table 3: target_rules
-- ----------------------------------------------------------------------------
-- Purpose: Store extraction rules for each target
-- Note: These rules are applied to audit_logs to extract values
--       If ANY rule successfully extracts a value, a case is created
-- ----------------------------------------------------------------------------

PROMPT Creating table: target_rules...

CREATE TABLE target_rules (
    ID VARCHAR2(100) PRIMARY KEY,
    TARGET_ID VARCHAR2(100) NOT NULL,
    RULE_NAME VARCHAR2(100) NOT NULL,
    SOURCE_FIELD VARCHAR2(100) NOT NULL,  -- Field from audit_logs to extract from (e.g., 'text', 'bindVariables')
    REGEX_PATTERN VARCHAR2(1000) NOT NULL,
    IS_ACTIVE NUMBER(1) DEFAULT 1,
    RULE_ORDER NUMBER NOT NULL,
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT FK_TARGET FOREIGN KEY (TARGET_ID)
        REFERENCES targets(ID) ON DELETE CASCADE,
    CONSTRAINT UK_TARGET_RULE UNIQUE (TARGET_ID, RULE_NAME)
);

PROMPT ✓ Table target_rules created

-- Create indexes for target_rules
PROMPT Creating indexes for target_rules...

CREATE INDEX IDX_RULES_TARGET_ID ON target_rules(TARGET_ID);
CREATE INDEX IDX_RULES_ACTIVE ON target_rules(IS_ACTIVE);
CREATE INDEX IDX_RULES_ORDER ON target_rules(TARGET_ID, RULE_ORDER);

PROMPT ✓ Indexes for target_rules created
PROMPT

-- ----------------------------------------------------------------------------
-- Table 4: cases
-- ----------------------------------------------------------------------------
-- Purpose: Store cases created when extraction rules successfully extract values
-- Note: Cases are created when ANY extraction rule successfully extracts a value
--       IMPORTANT: Only ONE case per audit_log (enforced by unique constraint)
-- ----------------------------------------------------------------------------

PROMPT Creating table: cases...

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

PROMPT ✓ Table cases created

-- Create indexes for cases
PROMPT Creating indexes for cases...

CREATE INDEX IDX_CASES_STATUS ON cases(CASE_STATUS);
CREATE INDEX IDX_CASES_VALID ON cases(VALID);
CREATE INDEX IDX_CASES_CREATED_AT ON cases(CREATED_AT);
-- Note: No separate index on AUDIT_LOG_ID needed - covered by UNIQUE constraint

PROMPT ✓ Indexes for cases created
PROMPT

-- ----------------------------------------------------------------------------
-- Table 5: case_extractions
-- ----------------------------------------------------------------------------
-- Purpose: Store extracted values ONLY for cases
-- Note: Each row represents one extracted value and the rule that extracted it
--       IMPORTANT: Extracted values are ONLY created when a case is created
--       Stores denormalized rule information (RULE_NAME, REGEX_PATTERN, SOURCE_FIELD) for audit trail
-- ----------------------------------------------------------------------------

PROMPT Creating table: case_extractions...

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

PROMPT ✓ Table case_extractions created

-- Create indexes for case_extractions
PROMPT Creating indexes for case_extractions...

CREATE INDEX IDX_EXTRACTIONS_CASE_ID ON case_extractions(CASE_ID);
CREATE INDEX IDX_EXTRACTIONS_AUDIT_LOG ON case_extractions(AUDIT_LOG_ID);
CREATE INDEX IDX_EXTRACTIONS_RULE_ID ON case_extractions(RULE_ID);
CREATE INDEX IDX_EXTRACTIONS_RULE_NAME ON case_extractions(RULE_NAME);
CREATE INDEX IDX_EXTRACTIONS_VALUE ON case_extractions(FIELD_VALUE);
CREATE INDEX IDX_EXTRACTIONS_NAME_VALUE ON case_extractions(RULE_NAME, FIELD_VALUE);

PROMPT ✓ Indexes for case_extractions created
PROMPT

-- ============================================================================
-- SECTION 3: VERIFICATION
-- ============================================================================

PROMPT ========================================
PROMPT Verifying table creation...
PROMPT ========================================

-- Check if all tables exist
SELECT
    'audit_logs' as TABLE_NAME,
    COUNT(*) as ROW_COUNT
FROM audit_logs
UNION ALL
SELECT 'targets', COUNT(*) FROM targets
UNION ALL
SELECT 'target_rules', COUNT(*) FROM target_rules
UNION ALL
SELECT 'cases', COUNT(*) FROM cases
UNION ALL
SELECT 'case_extractions', COUNT(*) FROM case_extractions;

PROMPT
PROMPT Checking indexes...
PROMPT

SELECT
    table_name,
    index_name,
    uniqueness,
    status
FROM user_indexes
WHERE table_name IN ('AUDIT_LOGS', 'TARGETS', 'TARGET_RULES', 'CASES', 'CASE_EXTRACTIONS')
ORDER BY table_name, index_name;

PROMPT
PROMPT Checking constraints...
PROMPT

SELECT
    table_name,
    constraint_name,
    constraint_type,
    CASE constraint_type
        WHEN 'P' THEN 'PRIMARY KEY'
        WHEN 'R' THEN 'FOREIGN KEY'
        WHEN 'U' THEN 'UNIQUE'
        WHEN 'C' THEN 'CHECK'
    END as constraint_description,
    status
FROM user_constraints
WHERE table_name IN ('AUDIT_LOGS', 'TARGETS', 'TARGET_RULES', 'CASES', 'CASE_EXTRACTIONS')
ORDER BY table_name, constraint_type, constraint_name;

-- ============================================================================
-- SECTION 4: SUMMARY
-- ============================================================================

PROMPT
PROMPT ========================================
PROMPT Database Recreation Complete!
PROMPT ========================================
PROMPT
PROMPT Tables Created:
PROMPT   ✓ audit_logs (with 5 indexes)
PROMPT   ✓ targets (with 1 index)
PROMPT   ✓ target_rules (with 3 indexes)
PROMPT   ✓ cases (with 3 indexes)
PROMPT   ✓ case_extractions (with 6 indexes)
PROMPT
PROMPT Total: 5 tables, 18 indexes, 13 constraints
PROMPT
PROMPT Next Steps:
PROMPT   1. Insert sample targets and rules (see data.md for examples)
PROMPT   2. Configure application connection strings
PROMPT   3. Start AuditSync Oracle Consumer application
PROMPT   4. Monitor case creation from Kafka messages
PROMPT
PROMPT For sample data and examples, see:
PROMPT   - docs/data.md
PROMPT   - docs/case_plan.md
PROMPT
PROMPT ========================================

-- Commit all changes
COMMIT;

PROMPT
PROMPT ✓ All changes committed successfully
PROMPT
