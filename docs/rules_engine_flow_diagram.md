# Rules Engine Flow Diagram

Visual representation of how the rules engine processes conditions and executes JavaScript.

---

## Correct Flow: JavaScript Executes Per-Condition

### **Key Point: JavaScript execution happens IMMEDIATELY when a condition passes AND has Extract=true**

```
┌─────────────────────────────────────────────────────────────────┐
│              START: EvaluateRulesAsync()                        │
│              Rules: [Rule 1, Rule 2, Rule 3]                    │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
        ┌────────────────────────────────────────┐
        │  For Each Rule (by ORDER_POSITION)     │
        └────────────────┬───────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    EvaluateRuleAsync(Rule 1)                    │
│                                                                  │
│  Rule 1 has 3 conditions:                                       │
│    Condition 1: dbUser IN ["SYS", "DBA"]                        │
│    Condition 2: action != 3                                     │
│    Condition 3: sqlText REGEX "ALTER" (Extract=true)            │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
        ┌────────────────────────────────────────┐
        │  For Each Condition (by Order)         │
        └────────────────┬───────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│              CONDITION 1: dbUser IN ["SYS", "DBA"]              │
│                                                                  │
│  Step 1: Get field value                                        │
│    actualValue = "DBA_USER"                                     │
│                                                                  │
│  Step 2: Evaluate operator "in"                                 │
│    "DBA_USER" in ["SYS", "DBA"] → FALSE ❌                      │
│                                                                  │
│  Result: CONDITION FAILED                                       │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
            ┌────────────────────────────┐
            │  Condition Failed →        │
            │  STOP evaluating Rule 1    │
            │  Try next rule (Rule 2)    │
            └────────────────────────────┘

─────────────────────────────────────────────────────────────────

Let's restart with a PASSING example:

┌─────────────────────────────────────────────────────────────────┐
│                    EvaluateRuleAsync(Rule 1)                    │
│                                                                  │
│  Rule 1 has 3 conditions:                                       │
│    Condition 1: dbUser IN ["SYS", "SYSTEM", "DBA_USER"]         │
│    Condition 2: action != "3"                                   │
│    Condition 3: sqlText REGEX "ALTER" (Extract=true)            │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│              CONDITION 1: dbUser IN [...]                       │
│                                                                  │
│  actualValue = "DBA_USER"                                       │
│  Operator: "in"                                                 │
│  Expected: ["SYS", "SYSTEM", "DBA_USER"]                        │
│                                                                  │
│  Result: TRUE ✓                                                 │
│  Extract: false (no JavaScript)                                 │
│                                                                  │
│  → Continue to next condition                                   │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│              CONDITION 2: action != "3"                         │
│                                                                  │
│  actualValue = "157"                                            │
│  Operator: "not_equals"                                         │
│  Expected: "3"                                                  │
│                                                                  │
│  Result: TRUE ✓                                                 │
│  Extract: false (no JavaScript)                                 │
│                                                                  │
│  → Continue to next condition                                   │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│         CONDITION 3: sqlText REGEX "ALTER" (Extract=true)       │
│                                                                  │
│  actualValue = "ALTER TABLE subscribers ADD COLUMN email"      │
│  Operator: "regex"                                              │
│  Expected: "(DROP|ALTER|GRANT|REVOKE)"                         │
│                                                                  │
│  Result: TRUE ✓ (matches "ALTER")                               │
│  Extract: true ← EXECUTE JAVASCRIPT NOW!                        │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│          JavaScriptExtractor.ExecuteExtraction()                │
│                                                                  │
│  Input:                                                          │
│    extractionLogic = "const regex = /(ALTER|DROP)\\s+...;"     │
│    fieldValue = "ALTER TABLE subscribers ADD COLUMN email"     │
│    auditLog = { id, dbUser, sqlText, ... }                     │
│                                                                  │
│  JavaScript Context:                                            │
│    value = "ALTER TABLE subscribers ADD COLUMN email"          │
│    auditLog = { id: "123", dbUser: "DBA_USER", ... }           │
│                                                                  │
│  Execute JavaScript:                                            │
│    const regex = /(ALTER|DROP)\s+(\w+)\s+(\w+)/i;              │
│    const match = value.match(regex);                           │
│    return [{                                                    │
│      value: "ALTER TABLE subscribers",                         │
│      type: "DDL_STATEMENT",                                     │
│      tags: ["privileged", "schema-change"]                      │
│    }];                                                          │
│                                                                  │
│  Result: [ExtractionResult { ... }]                            │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│           Collect Extraction from Condition 3                   │
│                                                                  │
│  allExtractions.AddRange([                                      │
│    {                                                             │
│      Value: "ALTER TABLE subscribers",                          │
│      Type: "DDL_STATEMENT",                                     │
│      Tags: ["privileged", "schema-change"],                     │
│      SourceField: "sqlText"  ← Tagged with condition field     │
│    }                                                             │
│  ])                                                              │
│                                                                  │
│  No more conditions → All passed!                               │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│              Return RuleMatchResult                             │
│                                                                  │
│  {                                                               │
│    RuleId: "rule-ddl-001",                                      │
│    RuleName: "Detect Privileged DDL",                           │
│    Matched: true,                                               │
│    Actions: { CreateCase: true },                               │
│    Extractions: [                                               │
│      {                                                           │
│        Value: "ALTER TABLE subscribers",                        │
│        Type: "DDL_STATEMENT",                                   │
│        Tags: ["privileged", "schema-change"],                   │
│        SourceField: "sqlText"                                   │
│      }                                                           │
│    ]                                                             │
│  }                                                               │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
            ┌────────────────────────────┐
            │  Rule 1 MATCHED →          │
            │  SHORT-CIRCUIT!            │
            │  Don't evaluate Rule 2, 3  │
            │  Return to caller          │
            └────────────────────────────┘
```

---

## Example: Multiple Conditions with Multiple Extractions

### **Rule Configuration**

```json
{
    "name": "Extract Subscriber Data",
    "conditions": [
        {
            "field": "sqlText",
            "operator": "contains",
            "value": "subscribers",
            "order": 1,
            "extract": false  // Just check, no extraction
        },
        {
            "field": "sqlText",
            "operator": "contains",
            "value": "msisdn",
            "order": 2,
            "extract": true,  // Extract MSISDNs
            "extractConfig": {
                "extractionLogic": "/* Extract MSISDNs from SQL */"
            }
        },
        {
            "field": "bindVariables",
            "operator": "regex",
            "value": "\\d{13}",
            "order": 3,
            "extract": true,  // Extract from bind variables
            "extractConfig": {
                "extractionLogic": "/* Extract from bind vars */"
            }
        }
    ]
}
```

### **Execution Flow**

```
CONDITION 1: sqlText CONTAINS "subscribers"
  ├─> actualValue: "SELECT * FROM subscribers WHERE msisdn = '123'"
  ├─> Operator: contains
  ├─> Result: TRUE ✓
  ├─> Extract: false → NO JAVASCRIPT
  └─> Continue to Condition 2

CONDITION 2: sqlText CONTAINS "msisdn" (Extract=true)
  ├─> actualValue: "SELECT * FROM subscribers WHERE msisdn = '9647501234567'"
  ├─> Operator: contains
  ├─> Result: TRUE ✓
  ├─> Extract: true → EXECUTE JAVASCRIPT NOW
  │
  ├─> JavaScript Returns:
  │   [
  │     { value: "9647501234567", type: "MSISDN", tags: ["sql"] }
  │   ]
  │
  ├─> Collect extraction (tagged with SourceField: "sqlText")
  └─> Continue to Condition 3

CONDITION 3: bindVariables REGEX "\d{13}" (Extract=true)
  ├─> actualValue: "#1(13):9647509876543"
  ├─> Operator: regex
  ├─> Result: TRUE ✓
  ├─> Extract: true → EXECUTE JAVASCRIPT NOW
  │
  ├─> JavaScript Returns:
  │   [
  │     { value: "9647509876543", type: "MSISDN", tags: ["bind"] }
  │   ]
  │
  ├─> Collect extraction (tagged with SourceField: "bindVariables")
  └─> No more conditions

ALL CONDITIONS PASSED!

Return RuleMatchResult with 2 EXTRACTIONS:
  1. { Value: "9647501234567", Type: "MSISDN", Tags: ["sql"], SourceField: "sqlText" }
  2. { Value: "9647509876543", Type: "MSISDN", Tags: ["bind"], SourceField: "bindVariables" }
```

---

## Important: Extract Happens Per-Condition

### **Wrong Understanding ❌**

```
1. Check all conditions
2. If all pass → Execute JavaScript once
3. Return result
```

### **Correct Understanding ✓**

```
1. Check Condition 1
   └─> If passes AND Extract=true → Execute JavaScript for Condition 1
2. Check Condition 2
   └─> If passes AND Extract=true → Execute JavaScript for Condition 2
3. Check Condition 3
   └─> If passes AND Extract=true → Execute JavaScript for Condition 3
4. Aggregate ALL extractions from all conditions
5. Return combined result
```

---

## Code Reference

From `RulesEngineService.cs` lines 72-106:

```csharp
// Evaluate conditions with short-circuit logic
foreach (var condition in sortedConditions)
{
    var conditionResult = EvaluateCondition(condition, auditLog, out var extractions);

    if (!conditionResult)
    {
        // Condition failed - short circuit, rule doesn't match
        return null;
    }

    // Condition passed, collect extractions if any
    if (extractions != null && extractions.Count > 0)
    {
        // Set source field for each extraction
        foreach (var extraction in extractions)
        {
            extraction.SourceField = condition.Field;
        }

        allExtractions.AddRange(extractions);  // ← Collect per-condition
    }
}

// All conditions passed
return new RuleMatchResult
{
    RuleId = rule.Id,
    RuleName = rule.Name,
    Matched = true,
    Actions = rule.Actions,
    Extractions = allExtractions  // ← All extractions from all conditions
};
```

From `RulesEngineService.cs` lines 154-161:

```csharp
// If condition matches and has extraction config, execute JavaScript
if (condition.Extract && condition.ExtractConfig != null)
{
    extractions = _jsExtractor.ExecuteExtraction(
        condition.ExtractConfig.ExtractionLogic,
        actualValue ?? string.Empty,
        auditLog);
}
```

**This clearly shows: JavaScript executes IMMEDIATELY when a condition passes AND has Extract=true**

---

## Summary

### **Execution Model**

| Step | Action | JavaScript? |
|------|--------|-------------|
| Condition 1 passes, Extract=false | Collect no extractions | ❌ No |
| Condition 2 passes, Extract=true | Execute JS, collect extractions | ✅ Yes |
| Condition 3 passes, Extract=false | Collect no extractions | ❌ No |
| Condition 4 passes, Extract=true | Execute JS, collect extractions | ✅ Yes |
| All conditions passed | Aggregate all extractions | - |

### **Key Points**

1. **JavaScript runs per-condition** (not once at the end)
2. **Each condition can have its own JavaScript** extraction logic
3. **Extractions are aggregated** from all conditions
4. **Each extraction is tagged** with its source field (condition.Field)
5. **Short-circuit still applies** - if ANY condition fails, stop immediately

This design allows **flexible, multi-source extraction** in a single rule!
