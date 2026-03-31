# Rules Engine Logic - Complete Explanation

This document explains the complete logic flow of the JavaScript-based rules engine.

---

## Overview: What is the Rules Engine?

The Rules Engine is a **decision-making and data extraction system** that:
1. **Evaluates conditions** against incoming audit logs
2. **Executes JavaScript code** to extract sensitive data
3. **Returns structured results** for case creation

**Key Features:**
- ✅ **Short-circuit evaluation**: Stops on first matching rule
- ✅ **AND logic**: All conditions within a rule must pass
- ✅ **Priority-based**: Rules execute in ORDER_POSITION sequence
- ✅ **Sandboxed JavaScript**: Safe execution with timeouts and memory limits
- ✅ **10+ operators**: equals, contains, regex, in, gt, lt, etc.

---

## Architecture: Three-Layer Design

```
┌─────────────────────────────────────────────────────────────┐
│                    RulesEngineService                       │
│  Orchestrates rule evaluation and condition matching       │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ├─> Evaluates each rule in sequence
                     ├─> Checks all conditions (AND logic)
                     └─> Collects extractions
                     │
┌────────────────────▼────────────────────────────────────────┐
│                  JavaScriptExtractor                        │
│  Executes JavaScript code in sandboxed Jint engine         │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ├─> Configures Jint with safety limits
                     ├─> Provides context (value, auditLog)
                     └─> Returns [{value, type, tags}]
                     │
┌────────────────────▼────────────────────────────────────────┐
│                   RuleMatchResult                           │
│  Contains matched rule info + all extractions              │
└─────────────────────────────────────────────────────────────┘
```

---

## Complete Flow: Step-by-Step

### **Entry Point: EvaluateRulesAsync()**

```csharp
public async Task<RuleMatchResult?> EvaluateRulesAsync(
    List<Rule> rules,           // All active rules (cached)
    AuditMessage auditLog,      // Incoming audit log
    CancellationToken cancellationToken)
{
    // Rules already sorted by ORDER_POSITION (1, 2, 3, ...)
    foreach (var rule in rules)
    {
        var matchResult = await EvaluateRuleAsync(rule, auditLog, cancellationToken);

        if (matchResult != null && matchResult.Matched)
        {
            return matchResult; // ✋ SHORT-CIRCUIT: Stop on first match
        }
    }

    return null; // No rules matched
}
```

**Logic:**
1. Loop through rules in **ORDER_POSITION** sequence
2. Try to match each rule against the audit log
3. **Stop immediately** when first rule matches (short-circuit)
4. Return `null` if no rules match

**Example:**
```
Rules loaded:
  1. Emergency Number Detection (ORDER: 1)
  2. VIP Subscriber Detection (ORDER: 2)
  3. Generic MSISDN Detection (ORDER: 3)

Audit Log: SELECT * FROM subscribers WHERE msisdn = '9647500000911'

Evaluation:
  Rule 1: Emergency Number → MATCH! (contains "911")
  Rule 2: NOT EVALUATED (short-circuit stopped)
  Rule 3: NOT EVALUATED (short-circuit stopped)

Result: Emergency case created
```

---

## Step 1: Single Rule Evaluation

### **EvaluateRuleAsync() - Evaluates One Rule**

```csharp
private async Task<RuleMatchResult?> EvaluateRuleAsync(
    Rule rule,
    AuditMessage auditLog,
    CancellationToken cancellationToken)
{
    // Sort conditions by Order (1, 2, 3, ...)
    var sortedConditions = rule.Conditions.OrderBy(c => c.Order).ToList();

    var allExtractions = new List<ExtractionResult>();

    // Evaluate conditions with AND logic
    foreach (var condition in sortedConditions)
    {
        var conditionResult = EvaluateCondition(condition, auditLog, out var extractions);

        if (!conditionResult)
        {
            return null; // ❌ Condition failed → Rule doesn't match
        }

        // ✅ Condition passed, collect extractions
        if (extractions != null && extractions.Count > 0)
        {
            foreach (var extraction in extractions)
            {
                extraction.SourceField = condition.Field; // Tag with source
            }
            allExtractions.AddRange(extractions);
        }
    }

    // All conditions passed!
    return new RuleMatchResult
    {
        RuleId = rule.Id,
        RuleName = rule.Name,
        Matched = true,
        Actions = rule.Actions,
        Extractions = allExtractions
    };
}
```

**Logic:**
1. Sort conditions within the rule by `Order` field
2. Evaluate each condition sequentially (AND logic)
3. **If ANY condition fails** → Rule doesn't match (return null)
4. **If ALL conditions pass** → Collect extractions and return match result

**Example: Multi-Condition Rule**

```json
Rule: "Detect Privileged DDL"
Conditions:
  1. dbUser IN ["SYS", "SYSTEM", "DBA_USER"]
  2. action NOT_EQUALS "3" (not SELECT)
  3. sqlText REGEX "(DROP|ALTER|GRANT)"
```

```
Audit Log:
  dbUser: "DBA_USER"
  action: 157
  sqlText: "ALTER TABLE subscribers ADD COLUMN email"

Evaluation:
  Condition 1: "DBA_USER" IN ["SYS", "SYSTEM", "DBA_USER"] → TRUE ✓
  Condition 2: "157" != "3" → TRUE ✓
  Condition 3: "ALTER TABLE..." matches regex → TRUE ✓

Result: ALL conditions passed → Rule MATCHED
```

---

## Step 2: Condition Evaluation

### **EvaluateCondition() - The Core Matching Logic**

```csharp
private bool EvaluateCondition(
    RuleCondition condition,
    AuditMessage auditLog,
    out List<ExtractionResult>? extractions)
{
    extractions = null;

    // Step 1: Get field value from audit log
    var actualValue = GetFieldValue(condition.Field, auditLog);

    // Step 2: Evaluate operator
    bool matches = condition.Operator.ToLower() switch
    {
        "equals"     => EvaluateEquals(actualValue, condition.Value),
        "not_equals" => !EvaluateEquals(actualValue, condition.Value),
        "contains"   => EvaluateContains(actualValue, condition.Value),
        "regex"      => EvaluateRegex(actualValue, condition.Value),
        "in"         => EvaluateIn(actualValue, condition.Value),
        "not_in"     => !EvaluateIn(actualValue, condition.Value),
        "gt"         => EvaluateGreaterThan(actualValue, condition.Value),
        "lt"         => EvaluateLessThan(actualValue, condition.Value),
        "gte"        => EvaluateGreaterThanOrEqual(actualValue, condition.Value),
        "lte"        => EvaluateLessThanOrEqual(actualValue, condition.Value),
        _ => throw new NotSupportedException($"Operator '{condition.Operator}' not supported")
    };

    if (!matches)
    {
        return false; // Condition failed
    }

    // Step 3: Execute JavaScript extraction (if configured)
    if (condition.Extract && condition.ExtractConfig != null)
    {
        extractions = _jsExtractor.ExecuteExtraction(
            condition.ExtractConfig.ExtractionLogic,
            actualValue ?? string.Empty,
            auditLog
        );
    }

    return true; // Condition passed
}
```

**Three-Step Process:**

### **Step 2.1: Get Field Value**

```csharp
private string? GetFieldValue(string fieldName, AuditMessage auditLog)
{
    return fieldName.ToLower() switch
    {
        "dbuser"       => auditLog.DbUser,
        "action"       => auditLog.Action.ToString(),
        "owner"        => auditLog.Owner,
        "name"         => auditLog.Name,
        "sqltext"      => auditLog.SqlText,
        "bindvariables"=> auditLog.BindVariables,
        "osuser"       => auditLog.OsUser,
        "userhost"     => auditLog.UserHost,
        "terminal"     => auditLog.Terminal,
        "returncode"   => auditLog.ReturnCode.ToString(),
        _ => null
    };
}
```

**Example:**
```
Condition.Field: "sqlText"
Audit Log:
  sqlText: "SELECT * FROM subscribers WHERE msisdn = '9647501234567'"

Result: actualValue = "SELECT * FROM subscribers WHERE msisdn = '9647501234567'"
```

### **Step 2.2: Evaluate Operator**

Each operator has its own implementation:

#### **"equals" - Case-Insensitive Exact Match**
```csharp
private bool EvaluateEquals(string? actual, object? expected)
{
    if (actual == null && expected == null) return true;
    if (actual == null || expected == null) return false;

    return string.Equals(actual, expected.ToString(), StringComparison.OrdinalIgnoreCase);
}
```

**Example:**
```
actual: "DWH"
expected: "dwh"
Result: TRUE (case-insensitive)
```

#### **"contains" - Substring Search**
```csharp
private bool EvaluateContains(string? actual, object? expected)
{
    if (actual == null || expected == null) return false;

    return actual.Contains(expected.ToString()!, StringComparison.OrdinalIgnoreCase);
}
```

**Example:**
```
actual: "SELECT * FROM subscribers WHERE msisdn = '123'"
expected: "msisdn"
Result: TRUE
```

#### **"regex" - Pattern Matching (1-second timeout)**
```csharp
private bool EvaluateRegex(string? actual, object? expected)
{
    if (actual == null || expected == null) return false;

    try
    {
        var regex = new Regex(expected.ToString()!, RegexOptions.None, TimeSpan.FromSeconds(1));
        return regex.IsMatch(actual);
    }
    catch (RegexMatchTimeoutException ex)
    {
        _logger.LogWarning(ex, "Regex timeout for pattern: {Pattern}", expected);
        return false; // Timeout = fail
    }
}
```

**Example:**
```
actual: "ALTER TABLE subscribers ADD COLUMN email"
expected: "(DROP|ALTER|GRANT|REVOKE)"
Result: TRUE (matches "ALTER")
```

#### **"in" - Value in List (Smart Parsing)**
```csharp
private bool EvaluateIn(string? actual, object? expected)
{
    if (actual == null || expected == null) return false;

    List<string> expectedValues;

    // Handle different formats:
    if (expected is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
    {
        // JSON array: ["SYS", "SYSTEM", "DBA_USER"]
        expectedValues = jsonElement.EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .ToList();
    }
    else if (expected.ToString()!.Contains(','))
    {
        // CSV: "SYS,SYSTEM,DBA_USER"
        expectedValues = expected.ToString()!
            .Split(',')
            .Select(v => v.Trim())
            .ToList();
    }
    else
    {
        // Single value: "SYS"
        expectedValues = new List<string> { expected.ToString()! };
    }

    return expectedValues.Any(v => string.Equals(v, actual, StringComparison.OrdinalIgnoreCase));
}
```

**Examples:**
```
// JSON array
actual: "DBA_USER"
expected: ["SYS", "SYSTEM", "DBA_USER"]
Result: TRUE

// CSV string
actual: "DBA_USER"
expected: "SYS,SYSTEM,DBA_USER"
Result: TRUE

// Single value
actual: "DBA_USER"
expected: "DBA_USER"
Result: TRUE
```

#### **"gt", "lt", "gte", "lte" - Numeric Comparison**
```csharp
private bool EvaluateGreaterThan(string? actual, object? expected)
{
    if (actual == null || expected == null) return false;

    if (double.TryParse(actual, out var actualNum) &&
        double.TryParse(expected.ToString(), out var expectedNum))
    {
        return actualNum > expectedNum;
    }

    return false; // Not numbers = fail
}
```

**Example:**
```
actual: "50000"
expected: "10000"
Result: TRUE (50000 > 10000)
```

### **Step 2.3: Execute JavaScript Extraction**

**Only runs if:**
1. Condition matched (operator returned true)
2. `Extract = true` in condition
3. `ExtractConfig` is not null

**Calls:** `JavaScriptExtractor.ExecuteExtraction()`

---

## Step 3: JavaScript Execution

### **JavaScriptExtractor - Sandboxed Execution**

```csharp
public List<ExtractionResult> ExecuteExtraction(
    string extractionLogic,  // JavaScript code from database
    string fieldValue,       // The field being processed (e.g., sqlText)
    AuditMessage auditLog)   // Complete audit log for context
{
    try
    {
        // Step 1: Configure Jint engine with SAFETY LIMITS
        var engine = new Engine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromSeconds(5));  // Max 5 seconds
            options.LimitRecursion(10);                        // Max 10 recursion levels
            options.LimitMemory(10_000_000);                   // Max 10MB memory
        });

        // Step 2: Set JavaScript variables
        engine.SetValue("value", fieldValue ?? string.Empty);

        // Step 3: Provide full audit log as object
        var auditLogObj = new
        {
            id = auditLog.Id,
            target = auditLog.Target,
            dbUser = auditLog.DbUser,
            sqlText = auditLog.SqlText,
            bindVariables = auditLog.BindVariables,
            action = auditLog.Action,
            // ... all fields
        };
        engine.SetValue("auditLog", auditLogObj);

        // Step 4: Provide webhook function (placeholder)
        engine.SetValue("webhook", new Action<string, object>((name, payload) =>
        {
            _logger.LogDebug("Webhook called: {Name}", name);
            // Future: HTTP POST to external endpoint
        }));

        // Step 5: Execute JavaScript code
        var result = engine.Evaluate(extractionLogic);

        // Step 6: Parse result into C# objects
        return ParseJintResult(result);
    }
    catch (Jint.Runtime.JavaScriptException ex)
    {
        _logger.LogError(ex, "JavaScript execution error: {Message}", ex.Message);
        return new List<ExtractionResult>(); // Empty list on error
    }
    catch (TimeoutException ex)
    {
        _logger.LogError(ex, "JavaScript execution timeout");
        return new List<ExtractionResult>();
    }
}
```

### **JavaScript Context - Available Variables**

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
    // ... all fields
};

// Variable 3: Webhook function (for future use)
const webhook = function(name, payload) {
    // Currently logs only
    // Future: HTTP POST to external system
};
```

### **JavaScript Return Format**

**Expected:**
```javascript
return [
    {
        value: "extracted-data",     // String: The extracted value
        type: "CATEGORY",            // String: Classification (MSISDN, IMEI, etc.)
        tags: ["tag1", "tag2"]       // Array: Tags for filtering/alerting
    },
    // ... more extractions
];
```

**Example:**
```javascript
// JavaScript code in database:
const regex = /msisdn\s*=\s*'(\d{13})'/gi;
const matches = [];
let match;
while ((match = regex.exec(value)) !== null) {
    matches.push({
        value: match[1],
        type: "MSISDN",
        tags: ["query", "subscriber"]
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

### **Result Parsing - ParseJintResult()**

```csharp
private List<ExtractionResult> ParseJintResult(JsValue result)
{
    // Validate result is array
    if (!result.IsArray())
    {
        _logger.LogWarning("Extraction must return array, got: {Type}", result.Type);
        return new List<ExtractionResult>();
    }

    var array = result.AsArray();
    var extractions = new List<ExtractionResult>();

    // Parse each item
    for (int i = 0; i < array.Length; i++)
    {
        try
        {
            var item = array.Get(i.ToString());

            if (!item.IsObject())
            {
                _logger.LogWarning("Item at index {Index} is not an object", i);
                continue; // Skip invalid item
            }

            var obj = item.AsObject();

            // Validate required fields
            if (!obj.HasProperty("value") ||
                !obj.HasProperty("type") ||
                !obj.HasProperty("tags"))
            {
                _logger.LogWarning("Missing required fields at index {Index}", i);
                continue; // Skip invalid item
            }

            // Extract values
            var value = obj.Get("value");
            var type = obj.Get("type");
            var tags = obj.Get("tags");

            // Convert to C# object
            extractions.Add(new ExtractionResult
            {
                Value = value.AsString(),     // "9647501234567"
                Type = type.AsString(),       // "MSISDN"
                Tags = ParseTags(tags)        // ["query", "subscriber"]
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing extraction at index {Index}", i);
            continue; // Skip problematic item, continue with rest
        }
    }

    return extractions;
}
```

**Error Handling:**
- Invalid items are **skipped** (not fatal)
- Missing fields logged as warnings
- Continues parsing remaining items
- Returns partial results instead of failing completely

---

## Complete Example: End-to-End Flow

### **Scenario: Detect MSISDN Access**

**1. Rule Configuration (Database)**
```json
{
    "id": "rule-msisdn-001",
    "name": "Detect MSISDN Access",
    "orderPosition": 1,
    "conditions": [
        {
            "field": "sqlText",
            "operator": "contains",
            "value": "msisdn",
            "order": 1,
            "extract": true,
            "extractConfig": {
                "extractionLogic": "const regex = /msisdn\\s*=\\s*'(\\d{13})'/gi;\nconst matches = [];\nlet match;\nwhile ((match = regex.exec(value)) !== null) {\n  matches.push({\n    value: match[1],\n    type: 'MSISDN',\n    tags: ['query']\n  });\n}\nreturn matches;"
            }
        }
    ],
    "actions": {
        "createCase": true
    }
}
```

**2. Audit Log Arrives**
```json
{
    "id": "12345_67890_1",
    "dbUser": "TELECOM_USER",
    "target": "DWH",
    "sqlText": "SELECT * FROM subscribers WHERE msisdn = '9647501234567'",
    "action": 3
}
```

**3. Execution Flow**

```
┌─────────────────────────────────────────────────────────────┐
│ EvaluateRulesAsync([Rule-001], auditLog)                   │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ EvaluateRuleAsync(Rule-001, auditLog)                      │
│   Sort conditions: [Condition-1]                            │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ EvaluateCondition(Condition-1, auditLog)                   │
│                                                              │
│   Step 1: GetFieldValue("sqlText")                         │
│      → "SELECT * FROM subscribers WHERE msisdn = '...'"    │
│                                                              │
│   Step 2: EvaluateContains(sqlText, "msisdn")              │
│      → TRUE ✓                                               │
│                                                              │
│   Step 3: Execute JavaScript (Extract=true)                │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ JavaScriptExtractor.ExecuteExtraction()                    │
│                                                              │
│   Configure Jint:                                           │
│     - Timeout: 5 seconds                                    │
│     - Recursion: 10 levels                                  │
│     - Memory: 10MB                                          │
│                                                              │
│   Set variables:                                            │
│     value = "SELECT * FROM subscribers..."                 │
│     auditLog = { id, dbUser, sqlText, ... }                │
│                                                              │
│   Execute JavaScript:                                       │
│     const regex = /msisdn\s*=\s*'(\d{13})'/gi;            │
│     // ... extraction logic ...                             │
│     return [{                                               │
│       value: "9647501234567",                              │
│       type: "MSISDN",                                       │
│       tags: ["query"]                                       │
│     }];                                                     │
│                                                              │
│   Parse result:                                             │
│     [ExtractionResult {                                     │
│       Value: "9647501234567",                              │
│       Type: "MSISDN",                                       │
│       Tags: ["query"],                                      │
│       SourceField: "sqlText"                                │
│     }]                                                      │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ Condition PASSED → Collect extractions                     │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ All conditions passed → Return RuleMatchResult             │
│                                                              │
│ {                                                            │
│   RuleId: "rule-msisdn-001",                                │
│   RuleName: "Detect MSISDN Access",                         │
│   Matched: true,                                            │
│   Actions: { CreateCase: true },                            │
│   Extractions: [                                            │
│     {                                                        │
│       Value: "9647501234567",                               │
│       Type: "MSISDN",                                       │
│       Tags: ["query"],                                      │
│       SourceField: "sqlText"                                │
│     }                                                        │
│   ]                                                          │
│ }                                                            │
└─────────────────────────────────────────────────────────────┘
```

**4. Result Returned to Caller**

The `RuleMatchResult` object is returned to the background service, which then:
1. Stores audit log in database
2. Creates case (because Actions.CreateCase = true)
3. Inserts extraction into case_extractions table
4. Commits Kafka offset

---

## Safety Mechanisms

### **1. Timeout Protection**
```csharp
options.TimeoutInterval(TimeSpan.FromSeconds(5));
```
- JavaScript execution limited to 5 seconds
- Prevents infinite loops
- Throws `TimeoutException` if exceeded
- Returns empty list instead of crashing

### **2. Recursion Limit**
```csharp
options.LimitRecursion(10);
```
- Maximum 10 recursion levels
- Prevents stack overflow
- Typical extraction: 0-2 levels (no issue)

### **3. Memory Limit**
```csharp
options.LimitMemory(10_000_000); // 10MB
```
- Prevents memory exhaustion
- Typical extraction uses < 100KB
- Large arrays/strings would hit limit

### **4. Regex Timeout**
```csharp
var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
```
- Regex evaluation limited to 1 second
- Prevents catastrophic backtracking
- Returns `false` on timeout (condition fails)

### **5. Graceful Error Handling**
- JavaScript errors return empty list (not crash)
- Invalid extraction items skipped (partial results)
- All errors logged for debugging
- System continues processing next messages

---

## Performance Characteristics

| Operation | Typical Time | Notes |
|-----------|--------------|-------|
| **Condition evaluation** | < 1ms | String operations are fast |
| **Regex matching** | 1-10ms | Depends on complexity |
| **JavaScript execution** | 5-50ms | Depends on extraction logic |
| **Parse result** | < 1ms | C# object creation |
| **Total per rule** | 10-100ms | Usually ~20ms |

**Optimization:**
- Short-circuit stops after first match
- Cache reduces DB queries to zero (after first load)
- Conditions ordered: fast checks first, expensive last

---

## Common Patterns

### **Pattern 1: Simple Detection (No Extraction)**
```json
{
    "field": "name",
    "operator": "in",
    "value": ["CREDIT_CARDS", "PASSWORDS"],
    "extract": false  // Just detect, don't extract
}
```

### **Pattern 2: Extract from Multiple Fields**
```javascript
// JavaScript can access bindVariables via auditLog
const results = [];

// Extract from sqlText (value variable)
const sqlMatches = value.match(/msisdn\s*=\s*'(\d{13})'/gi);
if (sqlMatches) {
    sqlMatches.forEach(m => results.push({ value: m, type: 'MSISDN', tags: ['sql'] }));
}

// Extract from bindVariables (auditLog.bindVariables)
if (auditLog.bindVariables) {
    const bindMatches = auditLog.bindVariables.match(/(\d{13})/g);
    if (bindMatches) {
        bindMatches.forEach(m => results.push({ value: m, type: 'MSISDN', tags: ['bind'] }));
    }
}

return results;
```

### **Pattern 3: Conditional Extraction**
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
return []; // Don't extract if limit <= 10000
```

---

## Summary

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

**JavaScript Extraction:**
- Conditions with `Extract=true` execute JavaScript when the condition passes
- JavaScript must return: `[{value: '', type: '', tags: ['']}]`
- Tags are stored as JSON string in `case_extractions.TAGS` column
- Example: `tags: ["suspicious", "vip"]` → stored as `'["suspicious","vip"]'`

### **Key Decisions**

| Design Choice | Reasoning |
|---------------|-----------|
| **Short-circuit** | Performance + predictable behavior |
| **AND logic** | All conditions must pass (strict matching) |
| **JavaScript** | Flexibility for complex extraction patterns |
| **Sandboxing** | Security (timeout, memory, recursion limits) |
| **ORDER_POSITION** | Control specificity (specific before generic) |
| **Graceful errors** | Partial results better than total failure |

The rules engine provides **powerful, flexible, and safe** extraction capabilities while maintaining high performance for real-time audit log processing!
