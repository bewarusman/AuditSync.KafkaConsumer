-- ============================================================================
-- Rules Engine Troubleshooting Queries
-- ============================================================================
-- Use these queries to diagnose why rules aren't matching or creating cases

-- ----------------------------------------------------------------------------
-- 1. Check if rules exist and are enabled
-- ----------------------------------------------------------------------------
SELECT
    r.ID,
    r.NAME,
    r.DESCRIPTION,
    r.ENABLED,
    r.ORDER_POSITION,
    t.NAME as TARGET_NAME,
    r.CREATED_AT,
    r.UPDATED_AT
FROM RULES r
JOIN TARGETS t ON t.ID = r.TARGET_ID
WHERE t.NAME = 'DWH'
ORDER BY r.ORDER_POSITION;

-- Expected: At least 1 row with ENABLED = 1
-- If 0 rows: No rules exist for target 'DWH'
-- If ENABLED = 0: Rules exist but are disabled

-- ----------------------------------------------------------------------------
-- 2. Check rule conditions
-- ----------------------------------------------------------------------------
SELECT
    r.ID as RULE_ID,
    r.NAME as RULE_NAME,
    r.ORDER_POSITION,
    r.CONDITIONS  -- CLOB containing JSON array
FROM RULES r
JOIN TARGETS t ON t.ID = r.TARGET_ID
WHERE t.NAME = 'DWH'
  AND r.ENABLED = 1
ORDER BY r.ORDER_POSITION;

-- Expected: CONDITIONS column should contain JSON like:
-- [
--   {
--     "field": "name",
--     "operator": "equals",
--     "value": "SUBSCRIBERS",
--     "order": 1,
--     "extract": false
--   },
--   ...
-- ]
-- Check:
-- - Are operators valid? (equals, contains, regex, in, gt, lt, gte, lte)
-- - Are operators not empty/null?
-- - Are field names correct? (dbUser, sqlText, name, owner, action, etc.)

-- ----------------------------------------------------------------------------
-- 3. Check rule actions
-- ----------------------------------------------------------------------------
SELECT
    r.ID as RULE_ID,
    r.NAME as RULE_NAME,
    r.ACTIONS  -- CLOB containing JSON object
FROM RULES r
JOIN TARGETS t ON t.ID = r.TARGET_ID
WHERE t.NAME = 'DWH'
  AND r.ENABLED = 1
ORDER BY r.ORDER_POSITION;

-- Expected: ACTIONS column should contain JSON like:
-- {
--   "createCase": true
-- }
-- If createCase is false, no case will be created even if rule matches

-- ----------------------------------------------------------------------------
-- 4. Check recent audit logs
-- ----------------------------------------------------------------------------
SELECT
    ID,
    TARGET,
    DB_USER,
    OWNER,
    NAME,
    ACTION,
    TIMESTAMP,
    SUBSTR(SQL_TEXT, 1, 100) as SQL_TEXT_PREVIEW
FROM audit_logs
WHERE TARGET = 'DWH'
ORDER BY TIMESTAMP DESC
FETCH FIRST 10 ROWS ONLY;

-- These are the audit messages that rules are evaluated against
-- Check if the field values match your rule conditions

-- ----------------------------------------------------------------------------
-- 5. Check if cases are being created (even if you think they're not)
-- ----------------------------------------------------------------------------
SELECT
    c.ID as CASE_ID,
    c.AUDIT_LOG_ID,
    c.CASE_STATUS,
    c.VALID,
    c.CREATED_AT,
    a.DB_USER,
    a.NAME as TABLE_NAME,
    SUBSTR(a.SQL_TEXT, 1, 50) as SQL_PREVIEW
FROM cases c
JOIN audit_logs a ON a.ID = c.AUDIT_LOG_ID
WHERE c.CREATED_AT > SYSDATE - 1  -- Last 24 hours
ORDER BY c.CREATED_AT DESC;

-- Expected: Rows if rules matched
-- If 0 rows: Either no rules matched or rules matched but createCase=false

-- ----------------------------------------------------------------------------
-- 6. Check if extractions are being stored
-- ----------------------------------------------------------------------------
SELECT
    ce.ID,
    ce.CASE_ID,
    ce.RULE_NAME,
    ce.EXTRACTION_TYPE,
    ce.SOURCE_FIELD,
    ce.EXTRACTION_VALUE,
    ce.TAGS,
    ce.EXTRACTED_AT
FROM case_extractions ce
WHERE ce.EXTRACTED_AT > SYSDATE - 1  -- Last 24 hours
ORDER BY ce.EXTRACTED_AT DESC;

-- Expected: Rows if JavaScript extraction executed
-- If 0 rows: Either no extraction configured or JavaScript returned []

-- ----------------------------------------------------------------------------
-- 7. Check target exists
-- ----------------------------------------------------------------------------
SELECT ID, NAME, DESCRIPTION, CREATED_AT
FROM targets
WHERE NAME = 'DWH';

-- Expected: 1 row
-- If 0 rows: Target 'DWH' doesn't exist - messages will be skipped

-- ----------------------------------------------------------------------------
-- 8. Example: Check if a specific rule's conditions would match recent data
-- ----------------------------------------------------------------------------
-- Replace 'YOUR_RULE_NAME' with actual rule name from query #1
SELECT
    a.ID,
    a.DB_USER,
    a.NAME,
    a.OWNER,
    SUBSTR(a.SQL_TEXT, 1, 100) as SQL_TEXT_PREVIEW,
    -- Add more fields as needed based on your rule conditions
    a.TIMESTAMP
FROM audit_logs a
WHERE a.TARGET = 'DWH'
  AND a.TIMESTAMP > SYSDATE - 1
  -- Add WHERE conditions that match your rule's conditions
  -- Example: AND a.NAME = 'SUBSCRIBERS'  (if rule has: field='name', operator='equals', value='SUBSCRIBERS')
  -- Example: AND UPPER(a.SQL_TEXT) LIKE '%MSISDN%'  (if rule has: field='sqlText', operator='contains', value='msisdn')
ORDER BY a.TIMESTAMP DESC
FETCH FIRST 10 ROWS ONLY;

-- This shows if audit logs exist that SHOULD match your rule conditions

-- ----------------------------------------------------------------------------
-- 9. Check for errors in conditions (empty operators)
-- ----------------------------------------------------------------------------
-- This requires parsing the JSON CONDITIONS column
-- If you see warnings like "Condition has empty operator" in logs,
-- run query #2 and manually inspect the CONDITIONS JSON to find
-- which condition has operator = "" or operator = null

-- ----------------------------------------------------------------------------
-- 10. Verify rules cache is loading correctly
-- ----------------------------------------------------------------------------
-- The consumer logs should show at startup:
-- "Cached N rules for target DWH. Next refresh at ..."
--
-- If it shows "Cached 0 rules", check:
-- - Does target 'DWH' exist? (query #7)
-- - Are rules enabled? (query #1)
-- - Are rules assigned to target 'DWH'? (query #1)
