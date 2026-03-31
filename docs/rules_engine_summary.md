# Rules Engine - Concise Summary

---

## Core Logic

### **Rules Engine = Decision Tree + Data Extractor**

```
1. Loop through rules (ORDER_POSITION)
2. For each rule:
   a. Check ALL conditions (AND logic)
      - All conditions must be true
      - If one condition fails → the whole rule fails
      - If a condition has javascript → execute the javascript
   b. If the rule fails → Try next rule
   c. If the rule passes → Stop here, skip all upcoming rules
3. First match wins (short-circuit)
```

---

## Condition Evaluation (AND Logic)

### **All Conditions Must Pass**

```
Rule has 3 conditions:
  Condition 1: dbUser IN ["SYS", "DBA_USER"]
  Condition 2: action != "3"
  Condition 3: sqlText REGEX "ALTER"

Evaluation:
  ├─> Condition 1: TRUE ✓
  ├─> Condition 2: TRUE ✓
  ├─> Condition 3: TRUE ✓
  └─> All TRUE → Rule PASSES
```

### **One Failed Condition = Rule Fails**

```
Rule has 3 conditions:
  Condition 1: dbUser IN ["SYS", "DBA_USER"]
  Condition 2: action != "3"
  Condition 3: sqlText REGEX "ALTER"

Evaluation:
  ├─> Condition 1: TRUE ✓
  ├─> Condition 2: FALSE ❌ → STOP! Rule FAILS
  └─> Condition 3: NOT EVALUATED

Result: Try next rule
```

---

## JavaScript Execution

### **When JavaScript Executes**

JavaScript executes when:
1. The condition passes (operator returns true)
2. The condition has `Extract=true`
3. The condition has `ExtractConfig` with `extractionLogic`

### **JavaScript Return Format**

**Required:**
```javascript
return [
    {
        value: '9647501234567',           // String: The extracted data
        type: 'MSISDN',                   // String: Classification
        tags: ['query', 'subscriber']     // Array: Tags for filtering
    },
    // ... can return multiple objects
];
```

**Empty result:**
```javascript
return [];  // No extractions found
```

### **JavaScript Context (Available Variables)**

```javascript
// Variable 1: The field value being processed
const value = "SELECT * FROM subscribers WHERE msisdn = '9647501234567'";

// Variable 2: Complete audit log object
const auditLog = {
    id: "12345_67890_1",
    target: "DWH",
    dbUser: "TELECOM_USER",
    sqlText: "SELECT * FROM subscribers WHERE msisdn = '9647501234567'",
    bindVariables: "#1(13):9647509876543",
    action: 3,
    returnCode: 0,
    owner: "TELECOM",
    name: "SUBSCRIBERS",
    timestamp: "2026-02-15T10:30:00Z"
    // ... all fields available
};

// Variable 3: Webhook function (future use - currently just logs)
const webhook = function(name, payload) {
    // Placeholder for HTTP callbacks
};
```

---

## Tags Storage

### **How Tags Are Stored in Database**

**JavaScript returns:**
```javascript
{
    value: "9647501234567",
    type: "MSISDN",
    tags: ["suspicious", "vip", "priority"]  // Array
}
```

**Stored in database:**
```sql
INSERT INTO case_extractions (
    EXTRACTION_VALUE,
    EXTRACTION_TYPE,
    TAGS
)
VALUES (
    '9647501234567',
    'MSISDN',
    '["suspicious","vip","priority"]'  -- JSON string (stringified)
);
```

**Note:** The `tags` array is **stringified** (converted to JSON string) before storing in the `TAGS` VARCHAR2(4000) column.

---

## Complete Example

### **Rule Configuration**

```json
{
    "id": "rule-msisdn-001",
    "name": "Detect MSISDN Access",
    "orderPosition": 1,
    "conditions": [
        {
            "field": "name",
            "operator": "equals",
            "value": "SUBSCRIBERS",
            "order": 1,
            "extract": false
        },
        {
            "field": "sqlText",
            "operator": "contains",
            "value": "msisdn",
            "order": 2,
            "extract": true,
            "extractConfig": {
                "extractionLogic": "const regex = /msisdn\\s*=\\s*'(\\d{13})'/gi;\nconst matches = [];\nlet match;\nwhile ((match = regex.exec(value)) !== null) {\n  matches.push({\n    value: match[1],\n    type: 'MSISDN',\n    tags: ['query', 'subscriber']\n  });\n}\nreturn matches;"
            }
        }
    ],
    "actions": {
        "createCase": true
    }
}
```

### **Audit Log**

```json
{
    "id": "12345_67890_1",
    "dbUser": "TELECOM_USER",
    "target": "DWH",
    "owner": "TELECOM",
    "name": "SUBSCRIBERS",
    "sqlText": "SELECT * FROM subscribers WHERE msisdn = '9647501234567'",
    "action": 3
}
```

### **Evaluation Flow**

```
Step 1: Evaluate Rule 1 (ORDER_POSITION: 1)

  Condition 1: name EQUALS "SUBSCRIBERS"
    ├─> Actual: "SUBSCRIBERS"
    ├─> Expected: "SUBSCRIBERS"
    ├─> Result: TRUE ✓
    └─> Extract: false → No JavaScript

  Condition 2: sqlText CONTAINS "msisdn"
    ├─> Actual: "SELECT * FROM subscribers WHERE msisdn = '9647501234567'"
    ├─> Expected: "msisdn"
    ├─> Result: TRUE ✓
    └─> Extract: true → Execute JavaScript

    JavaScript Execution:
      Input: value = "SELECT * FROM subscribers WHERE msisdn = '9647501234567'"
      Logic: Extract MSISDN using regex
      Output: [
        {
          value: "9647501234567",
          type: "MSISDN",
          tags: ["query", "subscriber"]
        }
      ]

  All Conditions Passed: TRUE ✓

  Rule Result: MATCHED
  Action: Skip Rule 2, Rule 3, ... (short-circuit)
```

### **Database Storage**

```sql
-- 1. Store audit log
INSERT INTO audit_logs (ID, TARGET, DB_USER, SQL_TEXT, ...)
VALUES ('12345_67890_1', 'DWH', 'TELECOM_USER', 'SELECT * FROM ...', ...);

-- 2. Create case
INSERT INTO cases (ID, AUDIT_LOG_ID, CASE_STATUS, VALID)
VALUES ('case-abc123', '12345_67890_1', 'OPEN', NULL);

-- 3. Store extraction
INSERT INTO case_extractions (
    ID,
    CASE_ID,
    AUDIT_LOG_ID,
    RULE_ID,
    RULE_NAME,
    EXTRACTION_TYPE,
    SOURCE_FIELD,
    EXTRACTION_VALUE,
    TAGS
)
VALUES (
    'ext-xyz789',
    'case-abc123',
    '12345_67890_1',
    'rule-msisdn-001',
    'Detect MSISDN Access',
    'MSISDN',                     -- From JavaScript {type}
    'sqlText',                    -- From condition.field
    '9647501234567',              -- From JavaScript {value}
    '["query","subscriber"]'      -- From JavaScript {tags} - STRINGIFIED
);
```

---

## Multiple Conditions with Multiple Extractions

### **Rule with 3 Conditions (2 have JavaScript)**

```json
{
    "conditions": [
        {
            "field": "name",
            "operator": "equals",
            "value": "SUBSCRIBERS",
            "extract": false  // No JavaScript
        },
        {
            "field": "sqlText",
            "operator": "contains",
            "value": "msisdn",
            "extract": true,  // JavaScript #1
            "extractConfig": {
                "extractionLogic": "/* Extract MSISDNs from SQL */"
            }
        },
        {
            "field": "bindVariables",
            "operator": "regex",
            "value": "\\d{13}",
            "extract": true,  // JavaScript #2
            "extractConfig": {
                "extractionLogic": "/* Extract MSISDNs from bind vars */"
            }
        }
    ]
}
```

### **Evaluation**

```
Condition 1: name EQUALS "SUBSCRIBERS"
  → TRUE ✓ (no JavaScript)

Condition 2: sqlText CONTAINS "msisdn"
  → TRUE ✓
  → Execute JavaScript #1
  → Returns: [{ value: "9647501234567", type: "MSISDN", tags: ["sql"] }]

Condition 3: bindVariables REGEX "\d{13}"
  → TRUE ✓
  → Execute JavaScript #2
  → Returns: [{ value: "9647509876543", type: "MSISDN", tags: ["bind"] }]

All Conditions Passed → Rule MATCHED

Total Extractions: 2
  1. value: "9647501234567", tags: ["sql"], sourceField: "sqlText"
  2. value: "9647509876543", tags: ["bind"], sourceField: "bindVariables"
```

### **Database Result**

```sql
-- One case created
INSERT INTO cases VALUES ('case-multi-123', ...);

-- Two extraction records
INSERT INTO case_extractions VALUES
  ('ext-1', 'case-multi-123', ..., '9647501234567', 'MSISDN', 'sqlText', '["sql"]'),
  ('ext-2', 'case-multi-123', ..., '9647509876543', 'MSISDN', 'bindVariables', '["bind"]');
```

---

## Short-Circuit Behavior

### **Scenario: Multiple Rules with Different Priorities**

```
Rules Configured (ORDER_POSITION):
  1. Emergency Number Detection (ORDER: 1)
  2. VIP Subscriber Detection (ORDER: 2)
  3. Generic MSISDN Detection (ORDER: 3)

Audit Log:
  sqlText: "SELECT * FROM subscribers WHERE msisdn = '9647500000911'"
```

### **Evaluation**

```
Rule 1: Emergency Number Detection
  Condition: sqlText REGEX "96475.*911"
  Result: TRUE ✓ (matches "9647500000911")
  Rule MATCHED → STOP EVALUATION

Rules 2 and 3: NEVER EVALUATED (short-circuit)
```

**Result:** Case created with "emergency" tag, not generic "MSISDN access"

---

## Key Points Summary

### **Rule Evaluation Logic**

| Aspect | Behavior |
|--------|----------|
| **Condition Logic** | ALL conditions must be TRUE (AND logic) |
| **One Failure** | If ANY condition fails → entire rule fails |
| **Rule Priority** | Rules evaluated by ORDER_POSITION (1, 2, 3, ...) |
| **Short-Circuit** | First matching rule wins, rest skipped |
| **JavaScript** | Executes per-condition when Extract=true |
| **Extractions** | Aggregated from all conditions in the rule |

### **JavaScript Execution**

| Aspect | Details |
|--------|---------|
| **Trigger** | Condition passes AND Extract=true |
| **Context** | `value` (field) + `auditLog` (complete) |
| **Return** | `[{value, type, tags}]` |
| **Timeout** | 5 seconds max |
| **Memory** | 10MB max |
| **Recursion** | 10 levels max |

### **Tags Storage**

| Step | Format | Example |
|------|--------|---------|
| **JavaScript returns** | Array | `["suspicious", "vip"]` |
| **Stringified** | JSON string | `'["suspicious","vip"]'` |
| **Database column** | VARCHAR2(4000) | `TAGS = '["suspicious","vip"]'` |

---

## Common Patterns

### **Pattern 1: Detection Only (No Extraction)**

```json
{
    "field": "name",
    "operator": "in",
    "value": ["CREDIT_CARDS", "PASSWORDS"],
    "extract": false  // Just detect, don't extract
}
```

### **Pattern 2: Conditional Extraction**

```javascript
// Only extract if limit > 10000
const limitMatch = value.match(/LIMIT\s+(\d+)/i);
if (limitMatch) {
    const limit = parseInt(limitMatch[1]);
    if (limit > 10000) {
        return [{
            value: limit.toString(),
            type: 'BULK_EXPORT',
            tags: ['suspicious', 'high-volume']
        }];
    }
}
return [];  // Don't extract if limit <= 10000
```

### **Pattern 3: Multi-Field Extraction**

```javascript
// Extract from both sqlText and bindVariables
const results = [];

// From sqlText (value variable)
const sqlMatches = value.match(/msisdn\s*=\s*'(\d{13})'/gi);
if (sqlMatches) {
    sqlMatches.forEach(m => {
        results.push({ value: m, type: 'MSISDN', tags: ['sql'] });
    });
}

// From bindVariables (auditLog.bindVariables)
if (auditLog.bindVariables) {
    const bindMatches = auditLog.bindVariables.match(/(\d{13})/g);
    if (bindMatches) {
        bindMatches.forEach(m => {
            results.push({ value: m, type: 'MSISDN', tags: ['bind'] });
        });
    }
}

return results;
```

---

## Error Handling

### **Graceful Degradation**

| Error Type | Behavior |
|------------|----------|
| **JavaScript error** | Return empty list `[]` (not crash) |
| **JavaScript timeout** | Return empty list `[]` (logged) |
| **Invalid extraction object** | Skip invalid item, continue with rest |
| **Missing required fields** | Skip item (logged warning) |
| **Regex timeout** | Condition fails (returns false) |

### **Example: Partial Results**

```javascript
// JavaScript returns mixed valid/invalid items
return [
    { value: "123", type: "MSISDN", tags: [] },  // ✓ Valid
    { value: "456" },                             // ✗ Missing type/tags (skipped)
    { value: "789", type: "IMEI", tags: [] }      // ✓ Valid
];

// Result: 2 extractions (invalid one skipped, logged as warning)
```

---

## Performance

| Operation | Typical Time |
|-----------|-------------|
| Condition evaluation | < 1ms |
| Regex matching | 1-10ms |
| JavaScript execution | 5-50ms |
| Total per rule | 10-100ms (~20ms avg) |

**Optimizations:**
- Short-circuit stops on first match
- Cache eliminates DB queries (after first load)
- Fast conditions ordered first, expensive ones last

---

This is the complete, accurate specification of how the rules engine works!
