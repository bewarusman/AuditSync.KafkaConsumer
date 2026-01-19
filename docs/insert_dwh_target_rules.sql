-- ============================================================================
-- SOURCE_FIELD Enum Mapping (SourceFieldType)
-- ============================================================================
-- The SOURCE_FIELD column stores numeric values that map to AuditMessage properties:
--   0 = SqlText
--   1 = BindVariables
--   2 = Owner
--   3 = Name
--   4 = DbUser
--   5 = UserHost
--   6 = Terminal
--   7 = OsUser
--   8 = Target
--   9 = AuthPrivileges
--  10 = AuthGrantee
--  11 = NewOwner
--  12 = NewName
--  13 = PrivilegeUsed
-- ============================================================================

-- Insert DWH Target
INSERT INTO targets (ID, NAME, DESCRIPTION, CREATED_AT, UPDATED_AT)
VALUES (
  SYS_GUID(),
  'DWH',
  'Data Warehouse target for audit log processing',
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 1: Extract table name from 'name' field
-- SOURCE_FIELD: 3 = Name (see SourceFieldType enum)
INSERT INTO target_rules (ID, TARGET_ID, NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  SYS_GUID(),
  TARGET_ID,
  'TABLE_NAME',
  3,  -- Name
  '^(\w+)$',
  1,  -- IS_REQUIRED
  1,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 2: Extract schema from 'owner' field
-- SOURCE_FIELD: 2 = Owner (see SourceFieldType enum)
INSERT INTO target_rules (ID, TARGET_ID, NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-2',
  'target-dwh',
  'SCHEMA',
  2,  -- Owner
  '^(\w+)$',
  1,  -- IS_REQUIRED
  2,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 3: Extract Korek MSISDN (96475XXXXXXXX pattern) from sqlText
-- SOURCE_FIELD: 0 = SqlText (see SourceFieldType enum)
INSERT INTO target_rules (ID, TARGET_ID, NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  SYS_GUID(),
  '469FE3B158BCB85CE0630B4CA8C08B2E',
  'KOREK_MSISDN',
  0,  -- SqlText
  '(96475\d{8})',
  0,  -- IS_REQUIRED (optional)
  3,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 4: Extract Korek MSISDN from bind variables
-- SOURCE_FIELD: 1 = BindVariables (see SourceFieldType enum)
INSERT INTO target_rules (ID, TARGET_ID, NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-4',
  'target-dwh',
  'KOREK_MSISDN_FROM_BIND',
  1,  -- BindVariables
  '#\d+\(\d+\):(96475\d{8})',
  0,  -- IS_REQUIRED (optional)
  4,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 5: Extract DB_USER
-- SOURCE_FIELD: 4 = DbUser (see SourceFieldType enum)
INSERT INTO target_rules (ID, TARGET_ID, NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-5',
  'target-dwh',
  'DB_USER',
  4,  -- DbUser
  '^(.+)$',
  1,  -- IS_REQUIRED
  5,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 6: Extract OS_USER
-- SOURCE_FIELD: 7 = OsUser (see SourceFieldType enum)
INSERT INTO target_rules (ID, TARGET_ID, NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-6',
  'target-dwh',
  'OS_USER',
  7,  -- OsUser
  '^(.+)$',
  0,  -- IS_REQUIRED (optional)
  6,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 7: Extract any MSISDN pattern (generic fallback) from sqlText
-- SOURCE_FIELD: 0 = SqlText (see SourceFieldType enum)
INSERT INTO target_rules (ID, TARGET_ID, NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-7',
  'target-dwh',
  'MSISDN_GENERIC',
  0,  -- SqlText
  'MSISDN\s*=\s*[''"]?(\d{10,15})[''"]?',
  0,  -- IS_REQUIRED (optional)
  7,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 8: Extract STATUS_ID from sqlText
-- SOURCE_FIELD: 0 = SqlText (see SourceFieldType enum)
INSERT INTO target_rules (ID, TARGET_ID, NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-8',
  'target-dwh',
  'STATUS_ID',
  0,  -- SqlText
  'STATUS_ID\s*=\s*(\d+)',
  0,  -- IS_REQUIRED (optional)
  8,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

COMMIT;

-- Verification queries
SELECT 'Target Created:' AS INFO, t.* FROM targets t WHERE t.NAME = 'DWH';
SELECT 'Rules Created:' AS INFO, COUNT(*) AS RULE_COUNT FROM target_rules WHERE TARGET_ID = 'target-dwh';
SELECT 'Rule Details:' AS INFO, r.RULE_NAME, r.SOURCE_FIELD, r.REGEX_PATTERN, r.RULE_ORDER
FROM target_rules r
WHERE r.TARGET_ID = 'target-dwh'
ORDER BY r.RULE_ORDER;
