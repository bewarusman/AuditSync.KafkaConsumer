# Cases and Case Extractions Database Schema

Complete documentation of how cases and their extractions are stored in the database.

---

## Table 1: CASES

### Purpose
Stores investigation cases created when rules match and extract sensitive data from audit logs.

### Schema

```sql
CREATE TABLE cases (
    -- Primary Key
    ID                  VARCHAR2(100) PRIMARY KEY,

    -- Foreign Key (1:1 relationship with audit_logs)
    AUDIT_LOG_ID        VARCHAR2(100) NOT NULL,

    -- Case Workflow Fields
    CASE_STATUS         VARCHAR2(50) DEFAULT 'OPEN',
    VALID               VARCHAR2(3) DEFAULT NULL,

    -- Timestamps
    CREATED_AT          TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT          TIMESTAMP DEFAULT SYSTIMESTAMP,

    -- Resolution Information
    RESOLVED_AT         TIMESTAMP,
    RESOLVED_BY         VARCHAR2(128),
    RESOLUTION_NOTES    VARCHAR2(4000),

    -- Constraints
    CONSTRAINT FK_CASE_AUDIT_LOG
        FOREIGN KEY (AUDIT_LOG_ID) REFERENCES audit_logs(ID) ON DELETE CASCADE,
    CONSTRAINT CHK_CASE_STATUS
        CHECK (CASE_STATUS IN ('OPEN', 'RESOLVED', 'ASSIGNED')),
    CONSTRAINT CHK_VALID
        CHECK (VALID IN ('YES', 'NO') OR VALID IS NULL),
    CONSTRAINT UK_CASES_AUDIT_LOG
        UNIQUE (AUDIT_LOG_ID)  -- One case per audit_log
);
```

### Field Descriptions

| Field | Type | Description | Populated By | Example |
|-------|------|-------------|--------------|---------|
| **ID** | VARCHAR2(100) | Unique case identifier | Consumer (auto) | `case-a1b2c3d4-5678` |
| **AUDIT_LOG_ID** | VARCHAR2(100) | Links to audit_logs table | Consumer | `12345_67890_1` |
| **CASE_STATUS** | VARCHAR2(50) | Current case state | Consumer + Manual | `OPEN`, `RESOLVED`, `ASSIGNED` |
| **VALID** | VARCHAR2(3) | Investigation result | Manual only | `YES`, `NO`, `NULL` |
| **CREATED_AT** | TIMESTAMP | When case was created | Consumer (auto) | `2026-02-15 10:30:00` |
| **UPDATED_AT** | TIMESTAMP | Last modification time | Consumer + Manual | `2026-02-15 14:20:00` |
| **RESOLVED_AT** | TIMESTAMP | When case was closed | Manual only | `2026-02-16 09:15:00` |
| **RESOLVED_BY** | VARCHAR2(128) | Who closed the case | Manual only | `security_analyst@korek.com` |
| **RESOLUTION_NOTES** | VARCHAR2(4000) | Investigation findings | Manual only | `False positive - authorized access` |

### Indexes

```sql
CREATE INDEX IDX_CASES_STATUS ON cases(CASE_STATUS);       -- Filter by status
CREATE INDEX IDX_CASES_VALID ON cases(VALID);              -- Filter by validity
CREATE INDEX IDX_CASES_CREATED_AT ON cases(CREATED_AT);    -- Time-based queries
```

### Important Constraints

**1. One Case Per Audit Log**
```sql
CONSTRAINT UK_CASES_AUDIT_LOG UNIQUE (AUDIT_LOG_ID)
```
- Each audit log can have maximum ONE case
- Prevents duplicate case creation on message reprocessing
- If consumer tries to create duplicate: DB throws unique constraint violation

**2. Case Status Validation**
```sql
CONSTRAINT CHK_CASE_STATUS CHECK (CASE_STATUS IN ('OPEN', 'RESOLVED', 'ASSIGNED'))
```
- **OPEN**: New case, not yet assigned
- **ASSIGNED**: Someone is investigating
- **RESOLVED**: Investigation complete

**3. Valid Flag Validation**
```sql
CONSTRAINT CHK_VALID CHECK (VALID IN ('YES', 'NO') OR VALID IS NULL)
```
- **NULL**: Consumer always sets this (never judges validity)
- **YES**: Manual - confirmed security incident
- **NO**: Manual - false positive or authorized activity

---

## Table 2: CASE_EXTRACTIONS

### Purpose
Stores individual extracted values from JavaScript rules engine. Each extraction object `{value, type, tags}` returned by JavaScript becomes one record.

### Schema

```sql
CREATE TABLE case_extractions (
    -- Primary Key
    ID                  VARCHAR2(100) PRIMARY KEY,

    -- Foreign Keys
    CASE_ID             VARCHAR2(100) NOT NULL,
    AUDIT_LOG_ID        VARCHAR2(100) NOT NULL,
    RULE_ID             VARCHAR2(100) NOT NULL,

    -- Denormalized Rule Information (Audit Trail)
    RULE_NAME           VARCHAR2(256) NOT NULL,

    -- Extraction Details
    EXTRACTION_TYPE     VARCHAR2(100) NOT NULL,   -- From JavaScript: {type}
    SOURCE_FIELD        VARCHAR2(100) NOT NULL,   -- Where it was extracted from
    EXTRACTION_VALUE    VARCHAR2(1000) NOT NULL,  -- From JavaScript: {value}
    TAGS                VARCHAR2(4000),           -- From JavaScript: {tags} as JSON

    -- Timestamp
    EXTRACTED_AT        TIMESTAMP(6) DEFAULT SYSTIMESTAMP NOT NULL,

    -- Constraints
    CONSTRAINT fk_case_extractions_case
        FOREIGN KEY (CASE_ID) REFERENCES CASES(ID) ON DELETE CASCADE,
    CONSTRAINT fk_case_extractions_audit_log
        FOREIGN KEY (AUDIT_LOG_ID) REFERENCES AUDIT_LOGS(ID) ON DELETE CASCADE
);
```

### Field Descriptions

| Field | Type | Description | Source | Example |
|-------|------|-------------|--------|---------|
| **ID** | VARCHAR2(100) | Unique extraction identifier | Auto-generated | `extraction-x1y2z3w4` |
| **CASE_ID** | VARCHAR2(100) | Parent case | From case creation | `case-a1b2c3d4` |
| **AUDIT_LOG_ID** | VARCHAR2(100) | Source audit log | From audit message | `12345_67890_1` |
| **RULE_ID** | VARCHAR2(100) | Which rule matched | From rule engine | `rule-msisdn-001` |
| **RULE_NAME** | VARCHAR2(256) | Rule name (denormalized) | From rule | `Detect MSISDN Access` |
| **EXTRACTION_TYPE** | VARCHAR2(100) | Category of extracted data | JavaScript `{type}` | `MSISDN`, `IMEI`, `IMSI` |
| **SOURCE_FIELD** | VARCHAR2(100) | Audit log field | Condition field | `sqlText`, `bindVariables` |
| **EXTRACTION_VALUE** | VARCHAR2(1000) | The actual extracted data | JavaScript `{value}` | `9647501234567` |
| **TAGS** | VARCHAR2(4000) | Classification tags as JSON | JavaScript `{tags}` | `["suspicious","vip"]` |
| **EXTRACTED_AT** | TIMESTAMP(6) | When extraction occurred | Auto (SYSTIMESTAMP) | `2026-02-15 10:30:00.123456` |

### Indexes (8 Total)

```sql
-- Foreign key indexes
CREATE INDEX idx_case_extractions_case_id ON case_extractions(CASE_ID);
CREATE INDEX idx_case_extractions_audit_log ON case_extractions(AUDIT_LOG_ID);
CREATE INDEX idx_case_extractions_rule_id ON case_extractions(RULE_ID);

-- Search indexes
CREATE INDEX idx_case_extractions_type ON case_extractions(EXTRACTION_TYPE);
CREATE INDEX idx_case_extractions_value ON case_extractions(EXTRACTION_VALUE);
CREATE INDEX idx_case_extractions_source ON case_extractions(SOURCE_FIELD);
CREATE INDEX idx_case_extractions_extracted ON case_extractions(EXTRACTED_AT);

-- Composite index (most common query pattern)
CREATE INDEX idx_case_extractions_type_value
    ON case_extractions(EXTRACTION_TYPE, EXTRACTION_VALUE);
```

### Why Denormalization?

**RULE_NAME is stored instead of just RULE_ID**

✅ **Pros:**
- Faster queries (no join with RULES table needed)
- Audit trail preserved even if rule is deleted/modified
- Simpler analytics queries
- Better performance for reporting

❌ **Cons:**
- Redundant data (disk space)
- If rule renamed, old extractions keep old name

**Decision:** Performance and audit trail > normalization

---

## Data Flow: From Audit Log to Database

### Example: MSISDN Detection

**Step 1: Audit Log Arrives**
```json
{
  "id": "12345_67890_1",
  "dbUser": "TELECOM_USER",
  "target": "DWH",
  "sqlText": "SELECT * FROM subscribers WHERE msisdn = '9647501234567'",
  "timestamp": "2026-02-15T10:30:00Z"
}
```

**Step 2: Rule Matches**
```
Rule: "Detect MSISDN Access"
Condition: sqlText CONTAINS "msisdn" → TRUE
JavaScript Extraction → Returns:
[
  {
    value: "9647501234567",
    type: "MSISDN",
    tags: ["query", "subscriber"]
  }
]
```

**Step 3: Store in audit_logs**
```sql
INSERT INTO audit_logs (
    ID, TARGET, DB_USER, SQL_TEXT, TIMESTAMP,
    KAFKA_PARTITION, KAFKA_OFFSET, PROCESS_COUNTER
)
VALUES (
    '12345_67890_1',
    'DWH',
    'TELECOM_USER',
    'SELECT * FROM subscribers WHERE msisdn = ''9647501234567''',
    TO_TIMESTAMP('2026-02-15T10:30:00Z', 'YYYY-MM-DD"T"HH24:MI:SS"Z"'),
    0,
    12345,
    1
);
```

**Step 4: Create Case**
```sql
INSERT INTO cases (
    ID,
    AUDIT_LOG_ID,
    CASE_STATUS,
    VALID,
    CREATED_AT,
    UPDATED_AT
)
VALUES (
    'case-a1b2c3d4-5678-90ab-cdef',
    '12345_67890_1',
    'OPEN',
    NULL,  -- Consumer NEVER sets VALID
    SYSTIMESTAMP,
    SYSTIMESTAMP
);
```

**Step 5: Store Extraction**
```sql
INSERT INTO case_extractions (
    ID,
    CASE_ID,
    AUDIT_LOG_ID,
    RULE_ID,
    RULE_NAME,
    EXTRACTION_TYPE,
    SOURCE_FIELD,
    EXTRACTION_VALUE,
    TAGS,
    EXTRACTED_AT
)
VALUES (
    'extraction-x1y2z3w4-1111-2222-3333',
    'case-a1b2c3d4-5678-90ab-cdef',
    '12345_67890_1',
    'rule-msisdn-001',
    'Detect MSISDN Access',
    'MSISDN',
    'sqlText',
    '9647501234567',
    '["query","subscriber"]',  -- JSON array as string
    SYSTIMESTAMP
);
```

---

## Multiple Extractions Example

### Scenario: Query with MSISDN AND IMEI

**JavaScript Returns:**
```javascript
[
  { value: "9647501234567", type: "MSISDN", tags: ["correlated"] },
  { value: "123456789012345", type: "IMEI", tags: ["correlated"] },
  { value: "9647509876543", type: "MSISDN", tags: ["bind-var"] },
  { value: "987654321098765", type: "IMEI", tags: ["bind-var"] }
]
```

### Database Storage

**One Case:**
```sql
INSERT INTO cases (ID, AUDIT_LOG_ID, CASE_STATUS)
VALUES ('case-multi-777', '88888_44444_5', 'OPEN');
```

**Four Extraction Records:**
```sql
INSERT INTO case_extractions (ID, CASE_ID, EXTRACTION_VALUE, EXTRACTION_TYPE, TAGS)
VALUES
  ('ext-1', 'case-multi-777', '9647501234567', 'MSISDN', '["correlated"]'),
  ('ext-2', 'case-multi-777', '123456789012345', 'IMEI', '["correlated"]'),
  ('ext-3', 'case-multi-777', '9647509876543', 'MSISDN', '["bind-var"]'),
  ('ext-4', 'case-multi-777', '987654321098765', 'IMEI', '["bind-var"]');
```

**Result:**
- 1 case record
- 4 extraction records
- All linked by CASE_ID

---

## Query Examples

### 1. Get All Open Cases with Extraction Count

```sql
SELECT
    c.ID AS CASE_ID,
    c.CASE_STATUS,
    c.CREATED_AT,
    COUNT(ce.ID) AS EXTRACTION_COUNT
FROM cases c
LEFT JOIN case_extractions ce ON c.ID = ce.CASE_ID
WHERE c.CASE_STATUS = 'OPEN'
GROUP BY c.ID, c.CASE_STATUS, c.CREATED_AT
ORDER BY c.CREATED_AT DESC;
```

**Output:**
```
CASE_ID                   | CASE_STATUS | CREATED_AT          | EXTRACTION_COUNT
--------------------------|-------------|---------------------|------------------
case-a1b2c3d4             | OPEN        | 2026-02-15 10:30:00 | 1
case-multi-777            | OPEN        | 2026-02-15 11:45:00 | 4
case-ddl-999              | OPEN        | 2026-02-15 12:00:00 | 1
```

### 2. Find All MSISDNs Extracted Today

```sql
SELECT
    ce.EXTRACTION_VALUE AS MSISDN,
    ce.CASE_ID,
    ce.TAGS,
    ce.EXTRACTED_AT,
    c.CASE_STATUS
FROM case_extractions ce
JOIN cases c ON ce.CASE_ID = c.ID
WHERE ce.EXTRACTION_TYPE = 'MSISDN'
  AND ce.EXTRACTED_AT >= TRUNC(SYSDATE)
ORDER BY ce.EXTRACTED_AT DESC;
```

**Output:**
```
MSISDN        | CASE_ID        | TAGS                | EXTRACTED_AT        | CASE_STATUS
--------------|----------------|---------------------|---------------------|-------------
9647501234567 | case-a1b2c3d4  | ["query","subscriber"] | 2026-02-15 10:30:00 | OPEN
9647509876543 | case-multi-777 | ["bind-var"]        | 2026-02-15 11:45:00 | OPEN
9647508282748 | case-xyz-123   | ["suspicious"]      | 2026-02-15 09:15:00 | RESOLVED
```

### 3. Get Case Details with All Extractions

```sql
SELECT
    c.ID AS CASE_ID,
    c.CASE_STATUS,
    c.VALID,
    c.CREATED_AT,
    ce.EXTRACTION_TYPE,
    ce.EXTRACTION_VALUE,
    ce.SOURCE_FIELD,
    ce.TAGS,
    al.DB_USER,
    al.SQL_TEXT
FROM cases c
LEFT JOIN case_extractions ce ON c.ID = ce.CASE_ID
JOIN audit_logs al ON c.AUDIT_LOG_ID = al.ID
WHERE c.ID = 'case-multi-777'
ORDER BY ce.EXTRACTED_AT;
```

**Output:**
```
CASE_ID        | CASE_STATUS | EXTRACTION_TYPE | EXTRACTION_VALUE    | SOURCE_FIELD | DB_USER      | SQL_TEXT
---------------|-------------|-----------------|---------------------|--------------|--------------|----------
case-multi-777 | OPEN        | MSISDN          | 9647501234567       | sqlText      | APP_USER     | SELECT...
case-multi-777 | OPEN        | IMEI            | 123456789012345     | sqlText      | APP_USER     | SELECT...
case-multi-777 | OPEN        | MSISDN          | 9647509876543       | bindVariables| APP_USER     | SELECT...
case-multi-777 | OPEN        | IMEI            | 987654321098765     | bindVariables| APP_USER     | SELECT...
```

### 4. Find Suspicious Cases (Using Tags)

```sql
SELECT
    c.ID AS CASE_ID,
    ce.EXTRACTION_VALUE,
    ce.EXTRACTION_TYPE,
    ce.TAGS,
    c.CREATED_AT
FROM case_extractions ce
JOIN cases c ON ce.CASE_ID = c.ID
WHERE ce.TAGS LIKE '%suspicious%'
  AND c.CASE_STATUS = 'OPEN'
ORDER BY c.CREATED_AT DESC;
```

**Output:**
```
CASE_ID       | EXTRACTION_VALUE | EXTRACTION_TYPE | TAGS                      | CREATED_AT
--------------|------------------|-----------------|---------------------------|-------------------
case-abc-123  | 9647501112233    | MSISDN          | ["suspicious","vip"]      | 2026-02-15 14:00:00
case-xyz-789  | 50000            | BULK_EXPORT     | ["suspicious","exfiltration"] | 2026-02-15 13:30:00
```

### 5. Monthly Case Statistics by Extraction Type

```sql
SELECT
    TO_CHAR(c.CREATED_AT, 'YYYY-MM') AS MONTH,
    ce.EXTRACTION_TYPE,
    COUNT(DISTINCT c.ID) AS CASE_COUNT,
    COUNT(ce.ID) AS EXTRACTION_COUNT
FROM cases c
JOIN case_extractions ce ON c.ID = ce.CASE_ID
GROUP BY TO_CHAR(c.CREATED_AT, 'YYYY-MM'), ce.EXTRACTION_TYPE
ORDER BY MONTH DESC, EXTRACTION_TYPE;
```

**Output:**
```
MONTH   | EXTRACTION_TYPE | CASE_COUNT | EXTRACTION_COUNT
--------|-----------------|------------|------------------
2026-02 | MSISDN          | 145        | 203
2026-02 | IMEI            | 67         | 89
2026-02 | BULK_EXPORT     | 12         | 12
2026-01 | MSISDN          | 98         | 134
2026-01 | IMEI            | 45         | 56
```

---

## Helper View: v_extractions_summary

### Definition

```sql
CREATE OR REPLACE VIEW v_extractions_summary AS
SELECT
    ce.ID,
    ce.CASE_ID,
    ce.AUDIT_LOG_ID,
    ce.RULE_ID,
    ce.RULE_NAME,
    ce.EXTRACTION_TYPE,
    ce.SOURCE_FIELD,
    ce.EXTRACTION_VALUE,
    ce.TAGS,
    ce.EXTRACTED_AT,
    -- Case information
    c.CASE_STATUS,
    c.VALID AS CASE_VALID,
    c.CREATED_AT AS CASE_CREATED_AT,
    -- Audit log information
    al.DB_USER,
    al.ACTION,
    al.OWNER,
    al.NAME AS OBJECT_NAME,
    al.RETURN_CODE,
    al.TIMESTAMP AS AUDIT_TIMESTAMP
FROM case_extractions ce
JOIN cases c ON ce.CASE_ID = c.ID
JOIN audit_logs al ON ce.AUDIT_LOG_ID = al.ID;
```

### Usage

```sql
-- Simple query without manual joins
SELECT *
FROM v_extractions_summary
WHERE EXTRACTION_TYPE = 'MSISDN'
  AND CASE_STATUS = 'OPEN'
  AND TRUNC(CASE_CREATED_AT) = TRUNC(SYSDATE);
```

---

## Important Business Rules

### Consumer Behavior

1. **Always NULL for VALID field**
   - Consumer never judges if a case is valid
   - Only humans can set VALID to 'YES' or 'NO'

2. **Always OPEN status**
   - Consumer always creates cases with CASE_STATUS = 'OPEN'
   - Manual processes change to 'ASSIGNED' or 'RESOLVED'

3. **Idempotent Case Creation**
   - UNIQUE constraint on AUDIT_LOG_ID prevents duplicates
   - If message reprocessed: case creation fails, but that's OK
   - Extractions might be re-inserted (depends on FK cascade behavior)

### Manual Workflow

1. **Case Assignment**
   ```sql
   UPDATE cases
   SET CASE_STATUS = 'ASSIGNED', UPDATED_AT = SYSTIMESTAMP
   WHERE ID = 'case-xyz-123';
   ```

2. **Case Resolution**
   ```sql
   UPDATE cases
   SET CASE_STATUS = 'RESOLVED',
       VALID = 'NO',  -- False positive
       RESOLVED_AT = SYSTIMESTAMP,
       RESOLVED_BY = 'analyst@korek.com',
       RESOLUTION_NOTES = 'Authorized maintenance activity',
       UPDATED_AT = SYSTIMESTAMP
   WHERE ID = 'case-xyz-123';
   ```

3. **Confirmed Security Incident**
   ```sql
   UPDATE cases
   SET CASE_STATUS = 'RESOLVED',
       VALID = 'YES',  -- Confirmed incident
       RESOLVED_AT = SYSTIMESTAMP,
       RESOLVED_BY = 'security@korek.com',
       RESOLUTION_NOTES = 'Unauthorized access by former employee. Account disabled.',
       UPDATED_AT = SYSTIMESTAMP
   WHERE ID = 'case-abc-999';
   ```

---

## Relationship Diagram

```
audit_logs (1) ←──────── (1) cases (1) ←──────── (N) case_extractions
    ↑                                                      ↑
    │                                                      │
    └──────────────────────────────────────────────────────┘
                    (Direct FK for audit trail)


One audit_log can have:
  - 0 cases (no rules matched)
  - 1 case (rules matched)

One case can have:
  - 1+ extractions (JavaScript returned values)

One extraction belongs to:
  - Exactly 1 case
  - Exactly 1 audit_log
  - Exactly 1 rule
```

---

## Performance Considerations

### Index Strategy

**Fast queries for:**
- ✅ Find all cases with status = 'OPEN'
- ✅ Find all MSISDNs extracted
- ✅ Find specific MSISDN value
- ✅ Find all extractions for a case
- ✅ Time-based queries (today, this month)

### Composite Index Usage

```sql
-- This query uses idx_case_extractions_type_value (composite index)
SELECT * FROM case_extractions
WHERE EXTRACTION_TYPE = 'MSISDN'
  AND EXTRACTION_VALUE = '9647501234567';

-- Execution plan: INDEX RANGE SCAN on idx_case_extractions_type_value
```

### Cascade Delete Behavior

```sql
-- Delete audit log → Deletes case → Deletes all extractions
DELETE FROM audit_logs WHERE ID = '12345_67890_1';

-- Result:
-- - 1 row deleted from audit_logs
-- - 1 row deleted from cases (CASCADE)
-- - N rows deleted from case_extractions (CASCADE)
```

---

## Summary

### Cases Table
- **Purpose**: Track security investigation workflows
- **Key Fields**: ID, AUDIT_LOG_ID, CASE_STATUS, VALID
- **Consumer Sets**: ID, AUDIT_LOG_ID, CASE_STATUS='OPEN', VALID=NULL
- **Manual Sets**: CASE_STATUS changes, VALID, resolution info

### Case_Extractions Table
- **Purpose**: Store individual extracted sensitive values
- **Key Fields**: EXTRACTION_VALUE, EXTRACTION_TYPE, TAGS
- **One JavaScript object** = **One database record**
- **Denormalized**: Includes RULE_NAME for performance

### Cardinality
- 1 audit_log : 0-1 case
- 1 case : 1-N extractions
- Each extraction links back to audit_log (audit trail)

### Performance
- 8 indexes on case_extractions
- 3 indexes on cases
- Optimized for common query patterns
- View available for simplified queries
