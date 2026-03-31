# Rules Examples and Processing Flow

This document shows real-world examples of how rules are structured in the database and how they're processed.

---

## Example 1: Detect MSISDN Access (Simple Contains)

### Database Storage

**RULES Table Row:**
```
ID: rule-msisdn-001
TARGET_ID: target-dwh-123
NAME: Detect MSISDN Access
DESCRIPTION: Triggers when SQL queries access MSISDN columns
ENABLED: 1
ORDER_POSITION: 1
```

**CONDITIONS Column (CLOB - JSON):**
```json
[
  {
    "field": "sqlText",
    "operator": "contains",
    "value": "msisdn",
    "order": 1,
    "extract": true,
    "extractConfig": {
      "extractionLogic": "const regex = /msisdn\\s*=\\s*'(\\d{13})'/gi;\nconst matches = [];\nlet match;\nconst pattern = new RegExp(regex);\nwhile ((match = pattern.exec(value)) !== null) {\n  matches.push({\n    value: match[1],\n    type: 'MSISDN',\n    tags: ['query', 'subscriber']\n  });\n}\nreturn matches;"
    }
  }
]
```

**ACTIONS Column (CLOB - JSON):**
```json
{
  "createCase": true,
  "notifyChannels": ["security-team"]
}
```

### Sample Audit Log

```json
{
  "id": "12345_67890_1",
  "sessionId": "12345",
  "entryId": "67890",
  "statement": "1",
  "dbUser": "TELECOM_USER",
  "target": "DWH",
  "sqlText": "SELECT * FROM subscribers WHERE msisdn = '9647501234567'",
  "bindVariables": null,
  "action": 3,
  "owner": "TELECOM",
  "name": "SUBSCRIBERS",
  "timestamp": "2026-02-15T10:30:00Z"
}
```

### Processing Steps

**Step 1: Load Rule**
```
RulesCache.GetRulesAsync() returns:
[
  Rule {
    Id: "rule-msisdn-001",
    Name: "Detect MSISDN Access",
    OrderPosition: 1,
    Conditions: [
      {
        Field: "sqlText",
        Operator: "contains",
        Value: "msisdn",
        Extract: true,
        ExtractConfig: { ... }
      }
    ],
    Actions: { CreateCase: true }
  }
]
```

**Step 2: Evaluate Condition**
```
Field: "sqlText"
Actual Value: "SELECT * FROM subscribers WHERE msisdn = '9647501234567'"

Operator: "contains"
Expected Value: "msisdn"

Evaluation:
  actualValue.Contains("msisdn", OrdinalIgnoreCase)
  → "SELECT ... WHERE msisdn = ..." contains "msisdn"
  → TRUE ✓
```

**Step 3: Execute JavaScript Extraction**
```javascript
// JavaScript context:
const value = "SELECT * FROM subscribers WHERE msisdn = '9647501234567'";
const auditLog = { /* full audit message */ };

// Extraction logic:
const regex = /msisdn\s*=\s*'(\d{13})'/gi;
const matches = [];
let match;
const pattern = new RegExp(regex);
while ((match = pattern.exec(value)) !== null) {
  matches.push({
    value: match[1],        // "9647501234567"
    type: 'MSISDN',
    tags: ['query', 'subscriber']
  });
}
return matches;

// Returns:
[
  {
    value: "9647501234567",
    type: "MSISDN",
    tags: ["query", "subscriber"]
  }
]
```

**Step 4: Store Results**

**audit_logs table:**
```sql
INSERT INTO audit_logs (ID, TARGET, DB_USER, SQL_TEXT, ...)
VALUES ('12345_67890_1', 'DWH', 'TELECOM_USER', 'SELECT * FROM ...', ...);
```

**cases table:**
```sql
INSERT INTO cases (ID, AUDIT_LOG_ID, CASE_STATUS, VALID, CREATED_AT)
VALUES ('case-abc123', '12345_67890_1', 'OPEN', NULL, SYSDATE);
```

**case_extractions table:**
```sql
INSERT INTO case_extractions (
  ID, CASE_ID, AUDIT_LOG_ID, RULE_ID, RULE_NAME,
  EXTRACTION_VALUE, EXTRACTION_TYPE, SOURCE_FIELD, TAGS
)
VALUES (
  'ext-xyz789',
  'case-abc123',
  '12345_67890_1',
  'rule-msisdn-001',
  'Detect MSISDN Access',
  '9647501234567',
  'MSISDN',
  'sqlText',
  '["query","subscriber"]'
);
```

**Step 5: Log Output**
```
[INFO] Rule 'Detect MSISDN Access' matched - Created case case-abc123 with 1 extraction(s) for message 12345_67890_1
```

---

## Example 2: Detect Privileged User Actions (Multiple Conditions)

### Database Storage

**CONDITIONS Column:**
```json
[
  {
    "field": "dbUser",
    "operator": "in",
    "value": ["SYS", "SYSTEM", "DBA_USER", "ADMIN"],
    "order": 1,
    "extract": false
  },
  {
    "field": "action",
    "operator": "not_equals",
    "value": "3",
    "order": 2,
    "extract": false
  },
  {
    "field": "sqlText",
    "operator": "regex",
    "value": "(DROP|ALTER|GRANT|REVOKE)",
    "order": 3,
    "extract": true,
    "extractConfig": {
      "extractionLogic": "const regex = /(DROP|ALTER|GRANT|REVOKE)\\s+(\\w+)\\s+(\\w+)/i;\nconst match = value.match(regex);\nif (match) {\n  return [{\n    value: match[0],\n    type: 'DDL_STATEMENT',\n    tags: ['privileged', 'schema-change']\n  }];\n}\nreturn [];"
    }
  }
]
```

**ACTIONS Column:**
```json
{
  "createCase": true,
  "notifyChannels": ["dba-team", "security-team"]
}
```

### Sample Audit Log

```json
{
  "id": "54321_11111_2",
  "sessionId": "54321",
  "dbUser": "DBA_USER",
  "target": "DWH",
  "action": 157,
  "sqlText": "ALTER TABLE subscribers ADD COLUMN email VARCHAR2(256)",
  "owner": "TELECOM",
  "name": "SUBSCRIBERS"
}
```

### Processing Steps

**Condition 1: Check dbUser IN list**
```
Field: "dbUser"
Actual: "DBA_USER"
Operator: "in"
Expected: ["SYS", "SYSTEM", "DBA_USER", "ADMIN"]

Evaluation:
  "DBA_USER" in ["SYS", "SYSTEM", "DBA_USER", "ADMIN"]
  → TRUE ✓
```

**Condition 2: Check action NOT equals SELECT**
```
Field: "action"
Actual: "157"
Operator: "not_equals"
Expected: "3"

Evaluation:
  "157" != "3"
  → TRUE ✓
```

**Condition 3: Check SQL contains DDL keywords (with regex)**
```
Field: "sqlText"
Actual: "ALTER TABLE subscribers ADD COLUMN email VARCHAR2(256)"
Operator: "regex"
Expected: "(DROP|ALTER|GRANT|REVOKE)"

Evaluation:
  Regex.IsMatch("ALTER TABLE ...", "(DROP|ALTER|GRANT|REVOKE)")
  → TRUE ✓ (matches "ALTER")

Execute JavaScript:
  Returns: [{
    value: "ALTER TABLE subscribers",
    type: "DDL_STATEMENT",
    tags: ["privileged", "schema-change"]
  }]
```

**All 3 conditions passed → Rule MATCHED**

**Result:**
```sql
-- Case created
INSERT INTO cases (ID, AUDIT_LOG_ID, CASE_STATUS)
VALUES ('case-ddl-999', '54321_11111_2', 'OPEN');

-- Extraction stored
INSERT INTO case_extractions (ID, CASE_ID, EXTRACTION_VALUE, EXTRACTION_TYPE, TAGS)
VALUES ('ext-ddl-888', 'case-ddl-999', 'ALTER TABLE subscribers', 'DDL_STATEMENT', '["privileged","schema-change"]');
```

---

## Example 3: Detect Bulk Data Exfiltration (Numeric Comparison)

### Database Storage

**CONDITIONS Column:**
```json
[
  {
    "field": "sqlText",
    "operator": "contains",
    "value": "SELECT",
    "order": 1,
    "extract": false
  },
  {
    "field": "returnCode",
    "operator": "equals",
    "value": "0",
    "order": 2,
    "extract": false
  },
  {
    "field": "sqlText",
    "operator": "regex",
    "value": "LIMIT\\s+(\\d+)",
    "order": 3,
    "extract": true,
    "extractConfig": {
      "extractionLogic": "const limitMatch = value.match(/LIMIT\\s+(\\d+)/i);\nconst offsetMatch = value.match(/OFFSET\\s+(\\d+)/i);\n\nif (limitMatch) {\n  const limit = parseInt(limitMatch[1]);\n  const offset = offsetMatch ? parseInt(offsetMatch[1]) : 0;\n  \n  if (limit > 10000) {\n    return [{\n      value: limit.toString(),\n      type: 'BULK_EXPORT',\n      tags: ['high-volume', 'potential-exfiltration']\n    }];\n  }\n}\nreturn [];"
    }
  }
]
```

### Sample Audit Log

```json
{
  "id": "99999_22222_3",
  "dbUser": "ANALYST_USER",
  "target": "DWH",
  "sqlText": "SELECT msisdn, imei, imsi FROM subscribers LIMIT 50000 OFFSET 0",
  "returnCode": 0,
  "action": 3
}
```

### Processing Steps

**Condition 1:**
```
sqlText CONTAINS "SELECT"
→ TRUE ✓
```

**Condition 2:**
```
returnCode EQUALS "0"
→ TRUE ✓ (query succeeded)
```

**Condition 3:**
```
sqlText REGEX "LIMIT\\s+(\\d+)"
→ TRUE ✓ (matches "LIMIT 50000")

JavaScript execution:
  limit = 50000
  offset = 0
  limit > 10000 → TRUE

  Returns: [{
    value: "50000",
    type: "BULK_EXPORT",
    tags: ["high-volume", "potential-exfiltration"]
  }]
```

**Result:**
```sql
INSERT INTO case_extractions (EXTRACTION_VALUE, EXTRACTION_TYPE, TAGS)
VALUES ('50000', 'BULK_EXPORT', '["high-volume","potential-exfiltration"]');
```

---

## Example 4: Detect Sensitive Table Access (No Extraction)

### Database Storage

**CONDITIONS Column:**
```json
[
  {
    "field": "name",
    "operator": "in",
    "value": ["CREDIT_CARDS", "PASSWORDS", "SSN_DATA", "BANK_ACCOUNTS"],
    "order": 1,
    "extract": false
  },
  {
    "field": "dbUser",
    "operator": "not_in",
    "value": ["AUTHORIZED_APP", "ETL_SERVICE"],
    "order": 2,
    "extract": false
  }
]
```

**ACTIONS Column:**
```json
{
  "createCase": true,
  "notifyChannels": ["compliance-team"]
}
```

### Sample Audit Log

```json
{
  "id": "77777_33333_4",
  "dbUser": "JOHN_DOE",
  "target": "DWH",
  "sqlText": "SELECT * FROM credit_cards WHERE card_number LIKE '4532%'",
  "owner": "FINANCE",
  "name": "CREDIT_CARDS",
  "action": 3
}
```

### Processing Steps

**Condition 1:**
```
name IN ["CREDIT_CARDS", "PASSWORDS", "SSN_DATA", "BANK_ACCOUNTS"]
→ "CREDIT_CARDS" is in the list
→ TRUE ✓
```

**Condition 2:**
```
dbUser NOT_IN ["AUTHORIZED_APP", "ETL_SERVICE"]
→ "JOHN_DOE" is NOT in the list
→ TRUE ✓
```

**Extract: false** → No JavaScript execution

**Result:**
```sql
-- Case created but NO extractions (Extract was false)
INSERT INTO cases (ID, AUDIT_LOG_ID, CASE_STATUS)
VALUES ('case-sensitive-555', '77777_33333_4', 'OPEN');

-- No rows in case_extractions table
```

---

## Example 5: Complex Multi-Field Extraction

### Database Storage

**CONDITIONS Column:**
```json
[
  {
    "field": "sqlText",
    "operator": "regex",
    "value": "WHERE\\s+msisdn.*AND\\s+imei",
    "order": 1,
    "extract": true,
    "extractConfig": {
      "extractionLogic": "const results = [];\n\n// Extract MSISDNs\nconst msisdnRegex = /msisdn\\s*=\\s*'(\\d{13})'/gi;\nlet match;\nwhile ((match = msisdnRegex.exec(value)) !== null) {\n  results.push({\n    value: match[1],\n    type: 'MSISDN',\n    tags: ['correlated-query']\n  });\n}\n\n// Extract IMEIs\nconst imeiRegex = /imei\\s*=\\s*'(\\d{15})'/gi;\nwhile ((match = imeiRegex.exec(value)) !== null) {\n  results.push({\n    value: match[1],\n    type: 'IMEI',\n    tags: ['correlated-query']\n  });\n}\n\n// Extract from bind variables if available\nif (auditLog.bindVariables) {\n  const bindRegex = /#\\d+\\(\\d+\\):(\\d{13,15})/g;\n  while ((match = bindRegex.exec(auditLog.bindVariables)) !== null) {\n    const val = match[1];\n    if (val.length === 13) {\n      results.push({ value: val, type: 'MSISDN', tags: ['bind-variable'] });\n    } else if (val.length === 15) {\n      results.push({ value: val, type: 'IMEI', tags: ['bind-variable'] });\n    }\n  }\n}\n\nreturn results;"
    }
  }
]
```

### Sample Audit Log

```json
{
  "id": "88888_44444_5",
  "dbUser": "APP_USER",
  "target": "DWH",
  "sqlText": "SELECT * FROM device_usage WHERE msisdn = '9647501234567' AND imei = '123456789012345'",
  "bindVariables": "#1(13):9647509876543 #2(15):987654321098765",
  "action": 3
}
```

### Processing Steps

**Condition 1:**
```
Regex: "WHERE\\s+msisdn.*AND\\s+imei"
Actual: "SELECT * FROM device_usage WHERE msisdn = '9647501234567' AND imei = '123456789012345'"
→ TRUE ✓

JavaScript execution:
  1. Extract MSISDNs from sqlText: "9647501234567"
  2. Extract IMEIs from sqlText: "123456789012345"
  3. Extract from bindVariables:
     - "9647509876543" (13 digits → MSISDN)
     - "987654321098765" (15 digits → IMEI)

  Returns: [
    { value: "9647501234567", type: "MSISDN", tags: ["correlated-query"] },
    { value: "123456789012345", type: "IMEI", tags: ["correlated-query"] },
    { value: "9647509876543", type: "MSISDN", tags: ["bind-variable"] },
    { value: "987654321098765", type: "IMEI", tags: ["bind-variable"] }
  ]
```

**Result:**
```sql
-- One case with FOUR extractions
INSERT INTO cases (ID, AUDIT_LOG_ID)
VALUES ('case-multi-777', '88888_44444_5');

INSERT INTO case_extractions (CASE_ID, EXTRACTION_VALUE, EXTRACTION_TYPE, TAGS)
VALUES
  ('case-multi-777', '9647501234567', 'MSISDN', '["correlated-query"]'),
  ('case-multi-777', '123456789012345', 'IMEI', '["correlated-query"]'),
  ('case-multi-777', '9647509876543', 'MSISDN', '["bind-variable"]'),
  ('case-multi-777', '987654321098765', 'IMEI', '["bind-variable"]');
```

---

## Processing Flow Summary

### Single Rule Evaluation

```
1. Load cached rules (sorted by ORDER_POSITION)
2. For each rule (stop on first match):
   a. For each condition (AND logic):
      i.   Get field value from audit log
      ii.  Evaluate operator
      iii. If matched AND Extract=true:
           - Execute JavaScript
           - Collect extractions
      iv.  If condition fails:
           - STOP, rule doesn't match
   b. If all conditions pass:
      - STOP evaluating rules (short-circuit)
      - Store audit log
      - Create case (if Actions.CreateCase=true)
      - Store extractions
      - Commit Kafka offset
3. If no rules matched:
   - Store audit log anyway
   - No case created
   - Commit Kafka offset
```

### Operator Behavior

| Operator | Logic | Example |
|----------|-------|---------|
| `equals` | Case-insensitive exact match | `"DWH" equals "dwh"` → true |
| `not_equals` | Negation of equals | `"DWH" not_equals "CRM"` → true |
| `contains` | Case-insensitive substring | `"SELECT * FROM" contains "SELECT"` → true |
| `regex` | Pattern match (1s timeout) | `"MSISDN=123" regex "MSISDN=\\d+"` → true |
| `in` | Value in array/CSV/JSON | `"SYS" in ["SYS","SYSTEM"]` → true |
| `not_in` | Negation of in | `"USER" not_in ["SYS","SYSTEM"]` → true |
| `gt` | Numeric greater than | `"100" gt "50"` → true |
| `lt` | Numeric less than | `"10" lt "50"` → true |
| `gte` | Greater than or equal | `"100" gte "100"` → true |
| `lte` | Less than or equal | `"50" lte "100"` → true |

### JavaScript Extraction Context

Every extraction JavaScript has access to:

```javascript
// Variables available in JavaScript:
const value;      // String: The field value being processed (e.g., sqlText)
const auditLog;   // Object: Complete audit message with all fields
                  //   {
                  //     id, sessionId, dbUser, sqlText, bindVariables,
                  //     action, target, owner, name, timestamp, ...
                  //   }
const webhook;    // Function: (Currently stub) For future HTTP callbacks

// Expected return format:
return [
  {
    value: "extracted-data",    // String: The extracted value
    type: "CATEGORY",            // String: Classification (MSISDN, IMEI, etc.)
    tags: ["tag1", "tag2"]       // Array: Tags for filtering/alerting
  },
  // ... more extractions
];

// Return empty array if nothing extracted:
return [];
```

### Performance Characteristics

| Scenario | Rules Checked | Conditions Evaluated | DB Queries | Latency |
|----------|---------------|---------------------|------------|---------|
| **Rule 1 matches** | 1 | 1-3 | 3 (audit_logs, cases, case_extractions) | ~50ms |
| **Rule 3 matches** | 3 | 5-8 | 3 | ~60ms |
| **No rules match** | All (e.g., 10) | 10-30 | 1 (audit_logs only) | ~30ms |
| **Rules cached** | N/A | N/A | 0 (cache hit) | ~1µs |

---

## Real-World Rule Priority Example

### Rules Loaded (ORDER_POSITION)

```
1. Detect Emergency Number Access (ORDER: 1)
2. Detect VIP Subscriber Access (ORDER: 2)
3. Detect MSISDN Access (ORDER: 3)
4. Detect Generic Sensitive Data (ORDER: 4)
```

### Audit Log
```sql
SELECT * FROM subscribers WHERE msisdn = '9647500000911'
```

### Evaluation Flow

```
Rule 1: Detect Emergency Number Access
  Condition: sqlText REGEX "96475.*911"
  → Match: "9647500000911" contains "911"
  → MATCHED! Stop evaluation
  → Tag: ["emergency", "critical"]

Rules 2, 3, 4: NEVER EVALUATED (short-circuit)
```

**Result:** Case created with "emergency" classification, not generic "MSISDN access"

**Key insight:** Rule order matters! Most specific rules should have lower ORDER_POSITION.

---

## Conclusion

The rules engine provides:
- ✅ Flexible condition logic (10+ operators)
- ✅ Powerful JavaScript extraction
- ✅ Short-circuit evaluation (performance)
- ✅ Multi-field extraction (comprehensive data capture)
- ✅ Priority-based matching (control specificity)
- ✅ Tag-based classification (flexible alerting)

All while maintaining high throughput and low latency!
