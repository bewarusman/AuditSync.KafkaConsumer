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
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  SYS_GUID(),
  TARGET_ID,
  'TABLE_NAME',
  'name',
  '^(\w+)$',
  1,  -- IS_REQUIRED
  1,  -- IS_ACTIVE
  1,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 2: Extract schema from 'owner' field
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-2',
  'target-dwh',
  'SCHEMA',
  'owner',
  '^(\w+)$',
  1,  -- IS_REQUIRED
  1,  -- IS_ACTIVE
  2,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 3: Extract Korek MSISDN (96475XXXXXXXX pattern) from sqlText
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  SYS_GUID(),
  '469FE3B158BCB85CE0630B4CA8C08B2E',
  'KOREK_MSISDN',
  'text',
  '(96475\d{8})',
  1,  -- IS_ACTIVE
  3,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 4: Extract Korek MSISDN from bind variables
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-4',
  'target-dwh',
  'KOREK_MSISDN_FROM_BIND',
  'bindVariables',
  '#\d+\(\d+\):(96475\d{8})',
  0,  -- IS_REQUIRED (optional)
  1,  -- IS_ACTIVE
  4,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 5: Extract DB_USER
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-5',
  'target-dwh',
  'DB_USER',
  'dbUser',
  '^(.+)$',
  1,  -- IS_REQUIRED
  1,  -- IS_ACTIVE
  5,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 6: Extract OS_USER
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-6',
  'target-dwh',
  'OS_USER',
  'osUser',
  '^(.+)$',
  0,  -- IS_REQUIRED (optional)
  1,  -- IS_ACTIVE
  6,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 7: Extract any MSISDN pattern (generic fallback) from sqlText
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-7',
  'target-dwh',
  'MSISDN_GENERIC',
  'sqlText',
  'MSISDN\s*=\s*[''"]?(\d{10,15})[''"]?',
  0,  -- IS_REQUIRED (optional)
  1,  -- IS_ACTIVE
  7,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 8: Extract STATUS_ID from sqlText
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT)
VALUES (
  'rule-dwh-8',
  'target-dwh',
  'STATUS_ID',
  'sqlText',
  'STATUS_ID\s*=\s*(\d+)',
  0,  -- IS_REQUIRED (optional)
  1,  -- IS_ACTIVE
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
