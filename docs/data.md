# Sample Extracted Data

> **📋 Note**: For the complete architectural plan, design rationale, and implementation phases, see [case_plan.md](docs/case_plan.md)

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

-- Table 2: Store cases created when extraction rules successfully extract values
-- Cases are created when ANY extraction rule successfully extracts a value from an audit_log
-- IMPORTANT: Only ONE case per audit_log (enforced by unique constraint)
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
-- Note: No separate index on AUDIT_LOG_ID needed - covered by UNIQUE constraint

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
-- These rules are applied to audit_logs to extract values
-- If ANY rule successfully extracts a value, a case is created
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

-- Indexes for target_rules
CREATE INDEX IDX_RULES_TARGET_ID ON target_rules(TARGET_ID);
CREATE INDEX IDX_RULES_ACTIVE ON target_rules(IS_ACTIVE);
CREATE INDEX IDX_RULES_ORDER ON target_rules(TARGET_ID, RULE_ORDER);

-- Table 5: Store extracted values ONLY for cases
-- Each row represents one extracted value and the rule that extracted it
-- IMPORTANT: Extracted values are ONLY created when a case is created
-- Stores denormalized rule information (RULE_NAME, REGEX_PATTERN, SOURCE_FIELD) for audit trail
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

-- Target 2: CDR Database
INSERT INTO targets VALUES (
  'target-2',
  'CDR Database',
  'Call Detail Records database',
  SYSTIMESTAMP,
  SYSTIMESTAMP
);
```

### Extraction Rules for "Production Oracle Database" target

```sql
-- Rule 1: Extract MSISDN from sqlText
INSERT INTO target_rules VALUES (
  'rule-1',
  'target-1',
  'msisdn',
  'text',
  'msisdn\s*=\s*''(\d+)''',
  1,  -- IS_ACTIVE
  1,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 2: Extract MSISDN from bind variables
INSERT INTO target_rules VALUES (
  'rule-2',
  'target-1',
  'msisdn',
  'bindVariables',
  '#1\(\d+\):(\d+)',
  1,  -- IS_ACTIVE
  2,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 3: Extract IMSI
INSERT INTO target_rules VALUES (
  'rule-3',
  'target-1',
  'imsi',
  'text',
  'imsi\s*=\s*''(\d+)''',
  1,  -- IS_ACTIVE
  3,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 4: Extract STATUS_ID
INSERT INTO target_rules VALUES (
  'rule-4',
  'target-1',
  'status_id',
  'text',
  'STATUS_ID\s*=\s*(\d+)',
  1,  -- IS_ACTIVE
  4,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);
```

### Extraction Rules for "CDR Database" target

```sql
-- Rule 1: Extract Iraqi MSISDN (Korek pattern)
INSERT INTO target_rules VALUES (
  'rule-5',
  'target-2',
  'msisdn',
  'text',
  '\b964(7[0-9]|73|74|75|76|77|78|79)\d{8}\b',
  1,  -- IS_ACTIVE
  1,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

-- Rule 2: Extract IMSI (Iraqi pattern)
INSERT INTO target_rules VALUES (
  'rule-6',
  'target-2',
  'imsi',
  'text',
  '\b418\d{9}\b',
  1,  -- IS_ACTIVE
  2,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);
```

---

## Example Scenario

### Input (Kafka Message)

```json
{
  "id": "157094731_1045_56",
  "target": "CDR Database",
  "sessionId": 157094731,
  "entryId": 1045,
  "statement": 56,
  "dbUser": "CDR_USER",
  "userHost": "KOREKTEL\\HQVAPPT01",
  "terminal": "HQVAPPT01",
  "action": 3,
  "returnCode": 0,
  "owner": "CDR",
  "name": "MSC",
  "authPrivileges": "",
  "authGrantee": "",
  "newOwner": "",
  "newName": "",
  "osUser": "AppUser",
  "privilegeUsed": null,
  "timestamp": "2025-12-23T10:15:30.123456",
  "bindVariables": "",
  "sqlText": "SELECT * FROM cdr.msc WHERE msisdn = '9647507703030' AND imsi = '418576891839'",
  "producedAt": "2025-12-23T10:15:35.654321"
}
```

## Output (Database Records)

### Table 1: audit_logs

```sql
INSERT INTO audit_logs VALUES (
  '157094731_1045_56',              -- ID
  'CDR Database',                   -- TARGET
  157094731,                        -- SESSION_ID
  1045,                             -- ENTRY_ID
  56,                               -- STATEMENT
  'CDR_USER',                       -- DB_USER
  'KOREKTEL\HQVAPPT01',            -- USER_HOST
  'HQVAPPT01',                      -- TERMINAL
  'AppUser',                        -- OS_USER
  3,                                -- ACTION (3 = SELECT)
  0,                                -- RETURN_CODE
  'CDR',                            -- OWNER
  'MSC',                            -- NAME
  '',                               -- AUTH_PRIVILEGES
  '',                               -- AUTH_GRANTEE
  '',                               -- NEW_OWNER
  '',                               -- NEW_NAME
  NULL,                             -- PRIVILEGE_USED
  'SELECT * FROM cdr.msc WHERE msisdn = ''9647507703030'' AND imsi = ''418576891839''', -- TEXT (CLOB)
  '',                               -- BIND_VARIABLES
  TO_TIMESTAMP('2025-12-23 10:15:30.123456', 'YYYY-MM-DD HH24:MI:SS.FF'), -- TIMESTAMP
  TO_TIMESTAMP('2025-12-23 10:15:35.654321', 'YYYY-MM-DD HH24:MI:SS.FF'), -- PRODUCED_AT
  0,                                -- KAFKA_PARTITION
  12345,                            -- KAFKA_OFFSET
  1,                                -- PROCESS_COUNTER
  SYSTIMESTAMP,                     -- PROCESSED_AT
  SYSTIMESTAMP                      -- CONSUMED_AT
);
```

### Table 2: cases

```sql
-- Case created because extraction rules successfully extracted values
INSERT INTO cases VALUES (
  'case-uuid-1',
  '157094731_1045_56',  -- AUDIT_LOG_ID (FK)
  'OPEN',               -- CASE_STATUS (OPEN, RESOLVED, ASSIGNED)
  NULL,                 -- VALID (defaults to NULL when case is created)
  SYSTIMESTAMP,         -- CREATED_AT
  SYSTIMESTAMP,         -- UPDATED_AT
  NULL,                 -- RESOLVED_AT
  NULL,                 -- RESOLVED_BY
  NULL                  -- RESOLUTION_NOTES
);
```

### Table 3: case_extractions

```sql
-- Row 1: MSISDN extracted by rule-5
INSERT INTO case_extractions VALUES (
  'extraction-uuid-1',
  'case-uuid-1',                    -- CASE_ID (FK)
  '157094731_1045_56',              -- AUDIT_LOG_ID (FK)
  'rule-5',                         -- RULE_ID (FK)
  'msisdn',                         -- RULE_NAME (denormalized)
  '\b964(7[0-9]|73|74|75|76|77|78|79)\d{8}\b',  -- REGEX_PATTERN (denormalized)
  'text',                           -- SOURCE_FIELD (denormalized)
  '9647507703030',                  -- FIELD_VALUE (extracted value)
  SYSTIMESTAMP                      -- EXTRACTED_AT
);

-- Row 2: IMSI extracted by rule-6
INSERT INTO case_extractions VALUES (
  'extraction-uuid-2',
  'case-uuid-1',                    -- CASE_ID (FK)
  '157094731_1045_56',              -- AUDIT_LOG_ID (FK)
  'rule-6',                         -- RULE_ID (FK)
  'imsi',                           -- RULE_NAME (denormalized)
  '\b418\d{9}\b',                   -- REGEX_PATTERN (denormalized)
  'text',                           -- SOURCE_FIELD (denormalized)
  '418576891839',                   -- FIELD_VALUE (extracted value)
  SYSTIMESTAMP                      -- EXTRACTED_AT
);
```

---

## Data Flow Summary

### Processing Flow

**Step 1: Find Target**
- Extract target name from Kafka message
- Lookup target in `targets` table

**Step 2: Load Extraction Rules**
- Load all active extraction rules for this target from `target_rules` table
- Order by RULE_ORDER ASC

**Step 3: Store Audit Log**
- Insert/Update audit_log in `audit_logs` table using MERGE (UPSERT)
- Deduplication: If same ID arrives again, updates all fields + increments PROCESS_COUNTER

**Step 4: Apply Extraction Rules**
- For each active rule (in RULE_ORDER):
  - Apply regex pattern to the specified SOURCE_FIELD
  - If extraction succeeds, store the extracted value temporarily
- If NO values were extracted → Skip to next message (no case created)
- If ANY values were extracted → Continue to Step 5

**Step 5: Create Case**
- Check if case already exists for this AUDIT_LOG_ID
- If case exists → Skip (due to reprocessing, no error thrown)
- If case doesn't exist → Create new case in `cases` table with status 'OPEN' and VALID = NULL

**Step 6: Store Extracted Values**
- For each extracted value:
  - Insert into `case_extractions` table
  - Include CASE_ID, AUDIT_LOG_ID, RULE_ID
  - Denormalize RULE_NAME, REGEX_PATTERN, SOURCE_FIELD from the rule
  - Store FIELD_VALUE (the extracted value)

### Important Notes

- **Case creation is based on extraction success**: If ANY rule extracts a value, create case
- **No extraction = No case**: If no rules match, skip the audit_log
- **Only ONE case per audit_log**: Enforced by UNIQUE constraint on AUDIT_LOG_ID
- **Denormalized rule info**: Each extraction stores rule name and regex for audit trail
- **Reprocessing safe**: If case already exists, skip gracefully (no error)
- **VALID field**: Always NULL when case is created (for manual review later)

### Example: Processing Logic

```
Given audit_log with sqlText: "SELECT * FROM cdr.msc WHERE msisdn = '9647507703030' AND imsi = '418576891839'"

Target: "CDR Database"
Rules for this target (ordered by RULE_ORDER):
  - Rule 1 (ORDER=1): Extract MSISDN - Pattern: \b964(7[0-9]|73|74|75|76|77|78|79)\d{8}\b - Field: text
  - Rule 2 (ORDER=2): Extract IMSI - Pattern: \b418\d{9}\b - Field: text

Processing:
1. Find target "CDR Database" → target_id = 'target-2'
2. Load rules for 'target-2' → [Rule 1, Rule 2]
3. Store audit_log → audit_logs table
4. Apply Rule 1 to 'text' field:
   - Match found: '9647507703030'
   - Store temporarily: {rule: 'rule-5', name: 'msisdn', value: '9647507703030'}
5. Apply Rule 2 to 'text' field:
   - Match found: '418576891839'
   - Store temporarily: {rule: 'rule-6', name: 'imsi', value: '418576891839'}
6. Check: extracted_values.count > 0 → TRUE
7. Create case → cases table (case-uuid-1)
8. Store extractions:
   - Insert case_extractions (extraction-uuid-1): msisdn = 9647507703030, rule = rule-5
   - Insert case_extractions (extraction-uuid-2): imsi = 418576891839, rule = rule-6

Result:
- 1 audit_log
- 1 case
- 2 extractions (msisdn, imsi)
```

### Benefits

1. **Simplified Architecture**
   - No separate case rules vs extraction rules
   - Extraction success determines case creation
   - Single table for all extracted values

2. **Complete Audit Trail**
   - Each extraction stores which rule extracted it
   - Rule name and regex pattern stored for historical reference
   - Easy to see what pattern matched what value

3. **Traceability**
   - One-to-one relationship: audit_log → case
   - Many-to-one relationship: extractions → case
   - Clear lineage from Kafka message to extracted values

4. **Query Flexibility**
   - Find all MSISDNs: `SELECT FIELD_VALUE FROM case_extractions WHERE RULE_NAME = 'msisdn'`
   - Find which rule extracted a value: `SELECT RULE_NAME, REGEX_PATTERN FROM case_extractions WHERE FIELD_VALUE = '9647507703030'`
   - Find all cases for a target: `SELECT c.* FROM cases c JOIN audit_logs a ON c.AUDIT_LOG_ID = a.ID WHERE a.TARGET = 'CDR Database'`

### Common Queries

```sql
-- Get case with all extracted values and the rules that extracted them
SELECT
  c.ID as CASE_ID,
  c.CASE_STATUS,
  ce.RULE_NAME,
  ce.REGEX_PATTERN,
  ce.SOURCE_FIELD,
  ce.FIELD_VALUE,
  ce.EXTRACTED_AT
FROM cases c
JOIN case_extractions ce ON c.ID = ce.CASE_ID
WHERE c.ID = 'case-uuid-1'
ORDER BY ce.EXTRACTED_AT;

-- Find all MSISDNs extracted across all cases
SELECT DISTINCT FIELD_VALUE
FROM case_extractions
WHERE RULE_NAME = 'msisdn'
ORDER BY FIELD_VALUE;

-- Find which rules are extracting the most values
SELECT
  RULE_NAME,
  REGEX_PATTERN,
  COUNT(*) as extraction_count
FROM case_extractions
GROUP BY RULE_NAME, REGEX_PATTERN
ORDER BY extraction_count DESC;

-- Get complete case information (audit_log + case + extractions)
SELECT
  a.*,
  c.CASE_STATUS,
  c.VALID,
  ce.RULE_NAME,
  ce.FIELD_VALUE
FROM audit_logs a
JOIN cases c ON a.ID = c.AUDIT_LOG_ID
LEFT JOIN case_extractions ce ON c.ID = ce.CASE_ID
WHERE a.ID = '157094731_1045_56';

-- Find cases that extracted both MSISDN and IMSI
SELECT c.ID, c.AUDIT_LOG_ID
FROM cases c
WHERE EXISTS (
  SELECT 1 FROM case_extractions ce1
  WHERE ce1.CASE_ID = c.ID AND ce1.RULE_NAME = 'msisdn'
)
AND EXISTS (
  SELECT 1 FROM case_extractions ce2
  WHERE ce2.CASE_ID = c.ID AND ce2.RULE_NAME = 'imsi'
);
```
