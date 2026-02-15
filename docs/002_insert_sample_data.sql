-- =============================================================================
-- AuditSync Oracle Consumer - Sample Data Script
-- =============================================================================
-- Description: Inserts sample targets and extraction rules for testing
-- =============================================================================

-- Insert Sample Targets
INSERT INTO targets (ID, NAME, DESCRIPTION, CREATED_AT, UPDATED_AT) VALUES (
  'target-1',
  'Production Oracle Database',
  'Production Oracle database for main application',
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

INSERT INTO targets (ID, NAME, DESCRIPTION, CREATED_AT, UPDATED_AT) VALUES (
  'target-2',
  'Development Oracle Database',
  'Development Oracle database for testing',
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

COMMIT;

-- Rules for "Production Oracle Database" target

-- Rule 1: Extract table name
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT) VALUES (
  'rule-1',
  'target-1',
  'TABLE_NAME',
  'name',
  '^(\w+)$',
  1,
  1,
  1,
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 2: Extract schema
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT) VALUES (
  'rule-2',
  'target-1',
  'SCHEMA',
  'owner',
  '^(\w+)$',
  1,
  1,
  2,
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 3: Extract MSISDN from sqlText
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT) VALUES (
  'rule-3',
  'target-1',
  'MSISDN',
  'sqlText',
  'MSISDN=:(\w+)',
  0,
  1,
  3,
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 4: Extract MSISDN from bind variables
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT) VALUES (
  'rule-4',
  'target-1',
  'MSISDN_FROM_BIND',
  'bindVariables',
  '#1\(\d+\):(\d+)',
  0,
  1,
  4,
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 5: Extract STATUS_ID
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT) VALUES (
  'rule-5',
  'target-1',
  'STATUS_ID',
  'sqlText',
  'STATUS_ID=(\d+)',
  0,
  1,
  5,
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rules for "Development Oracle Database" target
INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER, CREATED_AT, UPDATED_AT) VALUES (
  'rule-6',
  'target-2',
  'TABLE_NAME',
  'name',
  '^(\w+)$',
  1,
  1,
  1,
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

COMMIT;

-- Success message
SELECT 'Sample data inserted successfully!' AS STATUS FROM DUAL;
SELECT COUNT(*) AS TARGET_COUNT FROM targets;
SELECT COUNT(*) AS RULE_COUNT FROM target_rules;
