-- ============================================================================
-- Migration Script: Add Tagging Support to Extraction System
-- ============================================================================
-- Description: Adds ability to tag extracted values based on their content
-- Example: Tag MSISDNs starting with 964750445 as "VIP"
-- Date: 2026-01-20
-- Version: 1.0
-- ============================================================================

SET ECHO ON
SET FEEDBACK ON
SET SERVEROUTPUT ON

PROMPT ========================================
PROMPT Adding Tagging Support...
PROMPT ========================================

-- ============================================================================
-- Table 1: rule_tags
-- Purpose: Store tag configurations for extraction rules
-- Each rule can have multiple tag conditions that evaluate extracted values
-- ============================================================================

PROMPT Creating table: rule_tags...

CREATE TABLE rule_tags (
    ID VARCHAR2(100) PRIMARY KEY,
    RULE_ID VARCHAR2(100) NOT NULL,
    TAG_NAME VARCHAR2(100) NOT NULL,           -- Tag to apply (e.g., "VIP", "Priority", "High-Risk")
    CONDITION_TYPE VARCHAR2(50) NOT NULL,      -- Type of condition: StartsWith, EndsWith, Equals, Contains, Regex, Range
    CONDITION_VALUE VARCHAR2(1000) NOT NULL,   -- Value to match (e.g., "964750445" for StartsWith)
    TAG_PRIORITY NUMBER DEFAULT 0,             -- Priority for ordering (higher = more important)
    IS_ACTIVE NUMBER(1) DEFAULT 1,             -- Whether this tag rule is active
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT FK_RULE_TAG_RULE FOREIGN KEY (RULE_ID)
        REFERENCES target_rules(ID) ON DELETE CASCADE,
    CONSTRAINT CHK_CONDITION_TYPE CHECK (CONDITION_TYPE IN ('StartsWith', 'EndsWith', 'Equals', 'Contains', 'Regex', 'Range'))
);

PROMPT ✓ Table rule_tags created

-- Create indexes for rule_tags
PROMPT Creating indexes for rule_tags...

CREATE INDEX IDX_RULE_TAGS_RULE_ID ON rule_tags(RULE_ID);
CREATE INDEX IDX_RULE_TAGS_ACTIVE ON rule_tags(IS_ACTIVE);
CREATE INDEX IDX_RULE_TAGS_PRIORITY ON rule_tags(TAG_PRIORITY DESC);
CREATE INDEX IDX_RULE_TAGS_TAG_NAME ON rule_tags(TAG_NAME);

PROMPT ✓ Indexes for rule_tags created
PROMPT

-- ============================================================================
-- Table 2: Add TAGS column to case_extractions
-- Purpose: Store applied tags for each extracted value
-- ============================================================================

PROMPT Adding TAGS column to case_extractions...

ALTER TABLE case_extractions ADD (
    TAGS VARCHAR2(1000) DEFAULT NULL  -- Comma-separated list of applied tags (e.g., "VIP,Priority")
);

PROMPT ✓ TAGS column added to case_extractions
PROMPT

-- Create index on TAGS column for filtering
CREATE INDEX IDX_EXTRACTIONS_TAGS ON case_extractions(TAGS);

PROMPT ✓ Index created on TAGS column
PROMPT

-- ============================================================================
-- Sample Data: Tag Configuration Examples
-- ============================================================================

PROMPT ========================================
PROMPT Sample Tag Configurations
PROMPT ========================================

PROMPT
PROMPT To add tag rules, use the following pattern:
PROMPT
PROMPT -- Example 1: Tag MSISDNs starting with 964750445 as VIP
PROMPT INSERT INTO rule_tags VALUES (
PROMPT   'tag-1',
PROMPT   'rule-id-for-msisdn',  -- Replace with your actual MSISDN extraction rule ID
PROMPT   'VIP',                  -- Tag name
PROMPT   'StartsWith',           -- Condition type
PROMPT   '964750445',            -- Value to match
PROMPT   10,                     -- Priority
PROMPT   1,                      -- Active
PROMPT   SYSTIMESTAMP,
PROMPT   SYSTIMESTAMP
PROMPT );
PROMPT
PROMPT -- Example 2: Tag specific IMSI as High-Priority
PROMPT INSERT INTO rule_tags VALUES (
PROMPT   'tag-2',
PROMPT   'rule-id-for-imsi',
PROMPT   'High-Priority',
PROMPT   'Equals',
PROMPT   '418123456789',
PROMPT   5,
PROMPT   1,
PROMPT   SYSTIMESTAMP,
PROMPT   SYSTIMESTAMP
PROMPT );
PROMPT
PROMPT -- Example 3: Tag MSISDNs in range 9647504450000-9647504459999 as Premium
PROMPT INSERT INTO rule_tags VALUES (
PROMPT   'tag-3',
PROMPT   'rule-id-for-msisdn',
PROMPT   'Premium',
PROMPT   'Range',
PROMPT   '9647504450000-9647504459999',  -- Format: min-max
PROMPT   8,
PROMPT   1,
PROMPT   SYSTIMESTAMP,
PROMPT   SYSTIMESTAMP
PROMPT );
PROMPT
PROMPT -- Example 4: Tag using regex pattern
PROMPT INSERT INTO rule_tags VALUES (
PROMPT   'tag-4',
PROMPT   'rule-id-for-msisdn',
PROMPT   'Special',
PROMPT   'Regex',
PROMPT   '.*777$',  -- MSISDNs ending with 777
PROMPT   7,
PROMPT   1,
PROMPT   SYSTIMESTAMP,
PROMPT   SYSTIMESTAMP
PROMPT );
PROMPT

-- ============================================================================
-- Verification
-- ============================================================================

PROMPT ========================================
PROMPT Verifying changes...
PROMPT ========================================

-- Check if rule_tags table exists
SELECT 'rule_tags' as table_name, COUNT(*) as row_count FROM rule_tags;

-- Verify TAGS column was added
SELECT column_name, data_type, data_length, nullable
FROM user_tab_columns
WHERE table_name = 'CASE_EXTRACTIONS' AND column_name = 'TAGS';

PROMPT
PROMPT ========================================
PROMPT Migration Complete!
PROMPT ========================================
PROMPT
PROMPT Changes Applied:
PROMPT   ✓ Created rule_tags table with 4 indexes
PROMPT   ✓ Added TAGS column to case_extractions
PROMPT   ✓ Created index on TAGS column
PROMPT
PROMPT Supported Condition Types:
PROMPT   - StartsWith: Value starts with condition_value
PROMPT   - EndsWith: Value ends with condition_value
PROMPT   - Equals: Value exactly matches condition_value
PROMPT   - Contains: Value contains condition_value
PROMPT   - Regex: Value matches regex pattern in condition_value
PROMPT   - Range: Value is between min-max (format: "min-max")
PROMPT
PROMPT Next Steps:
PROMPT   1. Insert tag rules for your extraction rules
PROMPT   2. Update application code to evaluate tags
PROMPT   3. Test tag evaluation with sample data
PROMPT
PROMPT ========================================

-- Commit all changes
COMMIT;

PROMPT
PROMPT ✓ All changes committed successfully
PROMPT
