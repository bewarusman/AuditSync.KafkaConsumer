# Sample Extracted Data

## Database Schema

### Table Creation Queries

```sql
-- Table 1: Store complete audit messages from Kafka
-- Note: Uses MERGE (upsert) logic to prevent duplicates
-- If same ID arrives again, updates ALL fields + increments PROCESS_COUNTER
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

-- Indexes for audit_logs
CREATE INDEX IDX_AUDIT_SESSION ON audit_logs(SESSION_ID, ENTRY_ID);
CREATE INDEX IDX_AUDIT_PROCESSED_AT ON audit_logs(PROCESSED_AT);
CREATE INDEX IDX_AUDIT_DB_USER ON audit_logs(DB_USER);
CREATE INDEX IDX_AUDIT_TARGET ON audit_logs(TARGET);
CREATE INDEX IDX_AUDIT_PROCESS_COUNTER ON audit_logs(PROCESS_COUNTER);

-- Table 2: Store extracted (name, value) pairs
-- Note: On duplicate message, DELETE all old extracted values and re-INSERT new ones
-- This ensures extracted values always reflect the latest message data
CREATE TABLE audit_log_extracted_values (
    ID VARCHAR2(100) PRIMARY KEY,
    AUDIT_MESSAGE_ID VARCHAR2(100) NOT NULL,
    FIELD_NAME VARCHAR2(100) NOT NULL,
    FIELD_VALUE VARCHAR2(4000),
    EXTRACTED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT FK_AUDIT_MESSAGE FOREIGN KEY (AUDIT_MESSAGE_ID)
        REFERENCES audit_logs(ID) ON DELETE CASCADE
);

-- Indexes for audit_log_extracted_values
CREATE INDEX IDX_EXTRACTED_MESSAGE_ID ON audit_log_extracted_values(AUDIT_MESSAGE_ID);
CREATE INDEX IDX_EXTRACTED_FIELD_NAME ON audit_log_extracted_values(FIELD_NAME);
CREATE INDEX IDX_EXTRACTED_NAME_VALUE ON audit_log_extracted_values(FIELD_NAME, FIELD_VALUE);

-- Table 3: Store target information
CREATE TABLE targets (
    ID VARCHAR2(100) PRIMARY KEY,
    NAME VARCHAR2(256) NOT NULL UNIQUE,
    DESCRIPTION VARCHAR2(1000),
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
);

-- Indexes for targets
CREATE INDEX IDX_TARGETS_NAME ON targets(NAME);

-- Table 4: Store extraction rules for each target
-- Different targets can have different extraction rules
CREATE TABLE target_rules (
    ID VARCHAR2(100) PRIMARY KEY,
    TARGET_ID VARCHAR2(100) NOT NULL,
    RULE_NAME VARCHAR2(100) NOT NULL,
    SOURCE_FIELD VARCHAR2(100) NOT NULL,
    REGEX_PATTERN VARCHAR2(1000) NOT NULL,
    IS_REQUIRED NUMBER(1) DEFAULT 0,
    IS_ACTIVE NUMBER(1) DEFAULT 1,
    RULE_ORDER NUMBER NOT NULL,
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT FK_TARGET FOREIGN KEY (TARGET_ID)
        REFERENCES targets(ID) ON DELETE CASCADE,
    CONSTRAINT UK_TARGET_RULE UNIQUE (TARGET_ID, RULE_NAME)
);

-- Indexes for target_rules
CREATE INDEX IDX_RULES_TARGET_ID ON target_rules(TARGET_ID);
CREATE INDEX IDX_RULES_ACTIVE ON target_rules(IS_ACTIVE);
CREATE INDEX IDX_RULES_ORDER ON target_rules(TARGET_ID, RULE_ORDER);
```

---

## Sample Targets and Extraction Rules

### Insert Sample Targets

```sql
-- Target 1: Production Oracle Database
INSERT INTO targets VALUES (
  'target-1',
  'Production Oracle Database',
  'Production Oracle database for main application',
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Target 2: Development Oracle Database
INSERT INTO targets VALUES (
  'target-2',
  'Development Oracle Database',
  'Development Oracle database for testing',
  SYSTIMESTAMP,
  SYSTIMESTAMP
);
```

### Rules for "Production Oracle Database" target

```sql
-- Rule 1: Extract table name
INSERT INTO target_rules VALUES (
  'rule-1',
  'target-1',  -- TARGET_ID (FK to targets table)
  'TABLE_NAME',
  'name',
  '^(\w+)$',
  1,  -- IS_REQUIRED
  1,  -- IS_ACTIVE
  1,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 2: Extract schema
INSERT INTO target_rules VALUES (
  'rule-2',
  'target-1',  -- TARGET_ID (FK to targets table)
  'SCHEMA',
  'owner',
  '^(\w+)$',
  1,  -- IS_REQUIRED
  1,  -- IS_ACTIVE
  2,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 3: Extract MSISDN from sqlText
INSERT INTO target_rules VALUES (
  'rule-3',
  'target-1',  -- TARGET_ID (FK to targets table)
  'MSISDN',
  'sqlText',
  'MSISDN=:(\w+)',
  0,  -- IS_REQUIRED (optional)
  1,  -- IS_ACTIVE
  3,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 4: Extract MSISDN from bind variables
INSERT INTO target_rules VALUES (
  'rule-4',
  'target-1',  -- TARGET_ID (FK to targets table)
  'MSISDN_FROM_BIND',
  'bindVariables',
  '#1\(\d+\):(\d+)',
  0,  -- IS_REQUIRED (optional)
  1,  -- IS_ACTIVE
  4,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 5: Extract STATUS_ID
INSERT INTO target_rules VALUES (
  'rule-5',
  'target-1',  -- TARGET_ID (FK to targets table)
  'STATUS_ID',
  'sqlText',
  'STATUS_ID=(\d+)',
  0,  -- IS_REQUIRED (optional)
  1,  -- IS_ACTIVE
  5,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);
```

### Rules for "Development Oracle Database" target

```sql
-- Different rules can be defined for different targets
INSERT INTO target_rules VALUES (
  'rule-6',
  'target-2',  -- TARGET_ID (FK to targets table)
  'TABLE_NAME',
  'name',
  '^(\w+)$',
  1,
  1,
  1,
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Additional rules for development target...
```

---

## Input (Kafka Message)

```json
{
  "id": "157094731_1045_56",
  "target": "Production Oracle Database",
  "sessionId": 157094731,
  "entryId": 1045,
  "statement": 56,
  "dbUser": "DEMODB",
  "userHost": "KOREKTEL\\HQVAPPT01",
  "terminal": "HQVAPPT01",
  "action": 3,
  "returnCode": 0,
  "owner": "DEMODB",
  "name": "DEMO_CONTRACTS_TBL",
  "authPrivileges": "",
  "authGrantee": "",
  "newOwner": "",
  "newName": "",
  "osUser": "DefaultAppPool",
  "privilegeUsed": null,
  "timestamp": "2025-12-15T13:02:35.638673",
  "bindVariables": " #1(13):9647515364803",
  "sqlText": "SELECT COUNT (*) FROM DEMO_CONTRACTS_TBL WHERE MSISDN=:B1 AND STATUS_ID=1",
  "producedAt": "2025-12-18T08:41:03.850702"
}
```

## Output (Database Records)

### Table 1: audit_logs

```sql
-- First insert (process_counter = 1)
INSERT INTO audit_logs VALUES (
  '157094731_1045_56',              -- ID
  'Production Oracle Database',     -- TARGET
  157094731,                        -- SESSION_ID
  1045,                             -- ENTRY_ID
  56,                               -- STATEMENT
  'DEMODB',                         -- DB_USER
  'KOREKTEL\HQVAPPT01',            -- USER_HOST
  'HQVAPPT01',                      -- TERMINAL
  'DefaultAppPool',                 -- OS_USER
  3,                                -- ACTION (3 = SELECT)
  0,                                -- RETURN_CODE
  'DEMODB',                         -- OWNER
  'DEMO_CONTRACTS_TBL',            -- NAME
  '',                               -- AUTH_PRIVILEGES
  '',                               -- AUTH_GRANTEE
  '',                               -- NEW_OWNER
  '',                               -- NEW_NAME
  NULL,                             -- PRIVILEGE_USED
  'SELECT COUNT (*) FROM DEMO_CONTRACTS_TBL WHERE MSISDN=:B1 AND STATUS_ID=1', -- TEXT (CLOB)
  ' #1(13):9647515364803',         -- BIND_VARIABLES
  TO_TIMESTAMP('2025-12-15 13:02:35.638673', 'YYYY-MM-DD HH24:MI:SS.FF'), -- TIMESTAMP
  TO_TIMESTAMP('2025-12-18 08:41:03.850702', 'YYYY-MM-DD HH24:MI:SS.FF'), -- PRODUCED_AT
  0,                                -- KAFKA_PARTITION
  12345,                            -- KAFKA_OFFSET
  1,                                -- PROCESS_COUNTER
  SYSTIMESTAMP,                     -- PROCESSED_AT
  SYSTIMESTAMP                      -- CONSUMED_AT
);

-- If the same record arrives again (duplicate), update all fields instead of insert:
-- MERGE INTO audit_logs USING dual ON (ID = '157094731_1045_56')
-- WHEN MATCHED THEN
--   UPDATE SET
--     TARGET = 'Production Oracle Database',
--     SESSION_ID = 157094731,
--     ENTRY_ID = 1045,
--     STATEMENT = 56,
--     DB_USER = 'DEMODB',
--     USER_HOST = 'KOREKTEL\HQVAPPT01',
--     TERMINAL = 'HQVAPPT01',
--     OS_USER = 'DefaultAppPool',
--     ACTION = 3,
--     RETURN_CODE = 0,
--     OWNER = 'DEMODB',
--     NAME = 'DEMO_CONTRACTS_TBL',
--     AUTH_PRIVILEGES = '',
--     AUTH_GRANTEE = '',
--     NEW_OWNER = '',
--     NEW_NAME = '',
--     PRIVILEGE_USED = NULL,
--     TEXT = 'SELECT COUNT (*) FROM DEMO_CONTRACTS_TBL WHERE MSISDN=:B1 AND STATUS_ID=1',
--     BIND_VARIABLES = ' #1(13):9647515364803',
--     TIMESTAMP = TO_TIMESTAMP('2025-12-15 13:02:35.638673', 'YYYY-MM-DD HH24:MI:SS.FF'),
--     PRODUCED_AT = TO_TIMESTAMP('2025-12-18 08:41:03.850702', 'YYYY-MM-DD HH24:MI:SS.FF'),
--     KAFKA_PARTITION = 0,
--     KAFKA_OFFSET = 12345,
--     PROCESS_COUNTER = PROCESS_COUNTER + 1,
--     CONSUMED_AT = SYSTIMESTAMP;
```

### Table 2: audit_log_extracted_values (one row per extracted field)

```sql
-- First time processing - INSERT new extracted values
-- Row 1: MSISDN extracted value
INSERT INTO audit_log_extracted_values VALUES (
  'guid-1',
  '157094731_1045_56',  -- AUDIT_MESSAGE_ID (FK)
  'MSISDN',             -- FIELD_NAME
  '9647515364803',      -- FIELD_VALUE (extracted from bindVariables or text)
  SYSTIMESTAMP          -- EXTRACTED_AT
);

-- Row 2: STATUS_ID extracted value
INSERT INTO audit_log_extracted_values VALUES (
  'guid-2',
  '157094731_1045_56',  -- AUDIT_MESSAGE_ID (FK)
  'STATUS_ID',          -- FIELD_NAME
  '1',                  -- FIELD_VALUE
  SYSTIMESTAMP          -- EXTRACTED_AT
);

-- If the same record arrives again (duplicate):
-- 1. Delete all existing extracted values for this audit message
-- DELETE FROM audit_log_extracted_values WHERE AUDIT_MESSAGE_ID = '157094731_1045_56';
-- 2. Re-insert new extracted values (values may have changed in the new message)
-- INSERT INTO audit_log_extracted_values VALUES (...);
```

---

**Note:** The above examples show how Kafka messages are transformed and persisted into Oracle database tables. The `audit_logs` table stores the complete audit record with deduplication (updates all fields of existing records if they arrive again, incrementing the process_counter), while `audit_log_extracted_values` stores field/value pairs extracted using regex rules. When a duplicate message is processed, all extracted values are deleted and re-extracted from the new message to ensure they reflect the latest data.
