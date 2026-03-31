# Rules Engine Flow - Complete Execution Path

This document shows the complete flow of how the rules engine evaluates rules and conditions against audit messages.

---

## Quick Summary

The engine works in these key phases:

1. **Entry Point**: `AuditConsumerBackgroundService.ProcessWithRulesEngineAsync()` (line 171)
2. **Load Rules**: `RulesCache.GetRulesAsync()` - cached, thread-safe, 24h TTL
3. **Evaluate Rules**: `RulesEngineService.EvaluateRulesAsync()` (line 30)
   - Loops through rules by `ORDER_POSITION`
   - **First match wins** - stops on first matching rule
4. **Evaluate Each Rule**: `RulesEngineService.EvaluateRuleAsync()` (line 62)
   - Loops through conditions by `Order`
   - **ALL conditions must pass** (AND logic)
   - If any condition fails → rule fails, try next rule
5. **Evaluate Each Condition**: `RulesEngineService.EvaluateCondition()` (line 123)
   - Gets field value from audit message (line 131)
   - Evaluates operator (equals/contains/regex/in/gt/lt/etc.) (line 143)
   - If condition passes AND `Extract=true` → Execute JavaScript (line 164)
6. **JavaScript Execution**: `JavaScriptExtractor.ExecuteExtraction()`
   - Jint engine with 5s timeout, 10MB memory limit
   - Variables available: `value`, `auditLog`, `webhook`
   - Returns: `[{value, type, tags}]`
7. **Store Results**:
   - `audit_logs` table (always, MERGE)
   - `cases` table (if rule matched)
   - `case_extractions` table (if extractions exist)
8. **Commit Kafka offset** (only after successful DB writes)

---

## High-Level Overview

```
Kafka Message → Rules Engine → Database Storage
     ↓              ↓                ↓
AuditMessage   Evaluation     audit_logs (always)
               (1st match)    cases (if matched)
                              case_extractions (if extracted)
```

---

## Complete Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│ 1. KAFKA MESSAGE RECEIVED                                           │
│    AuditConsumerBackgroundService.ExecuteAsync()                    │
│    Location: src/AuditSync.OracleConsumer.App/Services/            │
│              AuditConsumerBackgroundService.cs:84                   │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 2. DESERIALIZE TO AuditMessage                                      │
│    JsonSerializer.Deserialize<AuditMessage>(message)                │
│    Line: 95-97                                                      │
│                                                                      │
│    AuditMessage contains:                                           │
│    - id, target, sessionId, entryId, statement                      │
│    - dbUser, userHost, terminal, action, returnCode                 │
│    - owner, name, sqlText, bindVariables                            │
│    - timestamp, osUser, authPrivileges, etc.                        │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 3. VALIDATE TARGET EXISTS                                           │
│    TargetRepository.ExistsAsync(auditMessage.Target)                │
│    Line: 122                                                        │
│                                                                      │
│    If target doesn't exist → Skip message, commit offset            │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 4. ENTER RULES ENGINE PATH                                          │
│    ProcessWithRulesEngineAsync()                                    │
│    Line: 134                                                        │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 5. LOAD CACHED RULES                                                │
│    RulesCache.GetRulesAsync()                                       │
│    Location: src/AuditSync.OracleConsumer.Application/Services/    │
│              RulesCache.cs:40                                       │
│    Line: 177                                                        │
│                                                                      │
│    Cache behavior:                                                  │
│    - If cache is fresh (< 24h) → return cached rules                │
│    - If cache is stale → refresh from DB (thread-safe)              │
│    - If DB fails → use stale cache (fault tolerance)                │
│                                                                      │
│    Rules are sorted by ORDER_POSITION ascending                     │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 6. EVALUATE RULES (SHORT-CIRCUIT)                                   │
│    RulesEngineService.EvaluateRulesAsync(rules, auditMessage)       │
│    Location: src/AuditSync.OracleConsumer.Application/Services/    │
│              RulesEngineService.cs:30                               │
│    Line: 180                                                        │
│                                                                      │
│    Loop through rules by ORDER_POSITION:                            │
│    foreach (var rule in rules) // Already sorted                    │
│    {                                                                 │
│        var matchResult = EvaluateRuleAsync(rule, auditLog);         │
│        if (matchResult.Matched)                                     │
│            return matchResult; // ← STOP! First match wins          │
│    }                                                                 │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 7. EVALUATE SINGLE RULE                                             │
│    RulesEngineService.EvaluateRuleAsync(rule, auditLog)             │
│    Line: 62                                                         │
│                                                                      │
│    var sortedConditions = rule.Conditions.OrderBy(c => c.Order);    │
│    var allExtractions = new List<ExtractionResult>();               │
│                                                                      │
│    foreach (var condition in sortedConditions)                      │
│    {                                                                 │
│        // Evaluate condition (see step 8)                           │
│    }                                                                 │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 8. EVALUATE SINGLE CONDITION (AND LOGIC)                            │
│    RulesEngineService.EvaluateCondition(condition, auditLog)        │
│    Line: 123                                                        │
│                                                                      │
│    Step A: Get field value from audit message                       │
│    ────────────────────────────────────────────                     │
│    var actualValue = GetFieldValue(condition.Field, auditLog);      │
│    Line: 131                                                        │
│                                                                      │
│    GetFieldValue() maps field names to audit message properties:    │
│    - "dbUser" → auditLog.DbUser                                     │
│    - "sqlText" → auditLog.SqlText                                   │
│    - "name" → auditLog.Name                                         │
│    - "owner" → auditLog.Owner                                       │
│    - etc. (Line: 178-194)                                           │
│                                                                      │
│    Step B: Validate operator is not empty                           │
│    ──────────────────────────────────────────                       │
│    if (string.IsNullOrEmpty(condition.Operator))                    │
│        return false; // Condition fails                             │
│    Line: 134-141                                                    │
│                                                                      │
│    Step C: Evaluate operator against value                          │
│    ───────────────────────────────────────────                      │
│    bool matches = condition.Operator.ToLower() switch               │
│    {                                                                 │
│        "equals"     → EvaluateEquals(actual, expected)              │
│        "not_equals" → !EvaluateEquals(actual, expected)             │
│        "contains"   → EvaluateContains(actual, expected)            │
│        "regex"      → EvaluateRegex(actual, expected)               │
│        "in"         → EvaluateIn(actual, expected)                  │
│        "not_in"     → !EvaluateIn(actual, expected)                 │
│        "gt"         → EvaluateGreaterThan(actual, expected)         │
│        "lt"         → EvaluateLessThan(actual, expected)            │
│        "gte"        → EvaluateGreaterThanOrEqual(actual, expected)  │
│        "lte"        → EvaluateLessThanOrEqual(actual, expected)     │
│        _            → throw NotSupportedException                   │
│    };                                                                │
│    Line: 143-156                                                    │
│                                                                      │
│    if (!matches)                                                    │
│        return false; // ← Condition fails, rule fails               │
│    Line: 158-161                                                    │
└────────────────────────────┬────────────────────────────────────────┘
                             │ (condition passed)
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 9. EXECUTE JAVASCRIPT EXTRACTION (IF CONFIGURED)                    │
│    if (condition.Extract && condition.ExtractConfig != null)        │
│    Line: 164                                                        │
│                                                                      │
│    JavaScriptExtractor.ExecuteExtraction(                           │
│        extractionLogic,   // JavaScript code from ExtractConfig     │
│        actualValue,       // The field value (e.g., sqlText)        │
│        auditLog           // Full audit message                     │
│    )                                                                 │
│    Location: src/AuditSync.OracleConsumer.Application/Services/    │
│              JavaScriptExtractor.cs:24                              │
│    Line: 166-169                                                    │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 10. JAVASCRIPT EXECUTION (Jint Engine)                              │
│     JavaScriptExtractor.ExecuteExtraction()                         │
│                                                                      │
│     Setup Jint engine with:                                         │
│     - Timeout: 5 seconds (configurable)                             │
│     - Memory limit: 10MB                                            │
│     - Recursion limit: 10 levels                                    │
│     Line: 31-36                                                     │
│                                                                      │
│     Make variables available to JavaScript:                         │
│     ─────────────────────────────────────────                       │
│     engine.SetValue("value", fieldValue);                           │
│     engine.SetValue("auditLog", auditLog);                          │
│     engine.SetValue("webhook", webhookFunc);                        │
│     Line: 39-49                                                     │
│                                                                      │
│     Execute JavaScript code:                                        │
│     ─────────────────────────────────────                           │
│     var result = engine.Evaluate(extractionLogic);                  │
│     Line: 52                                                        │
│                                                                      │
│     Expected JavaScript return format:                              │
│     ────────────────────────────────────                            │
│     [                                                                │
│         {                                                            │
│             value: "9647508282748",  // Extracted string            │
│             type: "MSISDN",          // Type classification         │
│             tags: ["query", "vip"]   // Tags array                  │
│         },                                                           │
│         // ... more extractions                                     │
│     ]                                                                │
│                                                                      │
│     Parse result and validate:                                      │
│     ───────────────────────────────                                 │
│     var extractions = ParseJintResult(result);                      │
│     Line: 54                                                        │
│                                                                      │
│     ParseJintResult() validates each extraction has:                │
│     - value (string)                                                 │
│     - type (string)                                                  │
│     - tags (array)                                                   │
│     Invalid items are skipped with warning log                      │
│     Line: 73-117                                                    │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 11. COLLECT EXTRACTIONS                                             │
│     Back in RulesEngineService.EvaluateRuleAsync()                  │
│                                                                      │
│     if (extractions != null && extractions.Count > 0)               │
│     {                                                                │
│         foreach (var extraction in extractions)                     │
│         {                                                            │
│             extraction.SourceField = condition.Field;               │
│             // Type already set by JavaScript                       │
│         }                                                            │
│         allExtractions.AddRange(extractions);                       │
│     }                                                                │
│     Line: 90-105                                                    │
│                                                                      │
│     Continue to next condition...                                   │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 12. ALL CONDITIONS PASSED                                           │
│     RulesEngineService.EvaluateRuleAsync()                          │
│                                                                      │
│     After all conditions evaluated:                                 │
│                                                                      │
│     return new RuleMatchResult                                      │
│     {                                                                │
│         RuleId = rule.Id,                                           │
│         RuleName = rule.Name,                                       │
│         Matched = true,                                             │
│         Actions = rule.Actions,                                     │
│         Extractions = allExtractions  // Aggregated from all conds  │
│     };                                                               │
│     Line: 109-116                                                   │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 13. RETURN TO CALLER (SHORT-CIRCUIT)                                │
│     RulesEngineService.EvaluateRulesAsync()                         │
│                                                                      │
│     if (matchResult.Matched)                                        │
│     {                                                                │
│         Log: "Rule '{RuleName}' matched"                            │
│         return matchResult; // ← STOP! Don't evaluate more rules    │
│     }                                                                │
│     Line: 42-50                                                     │
│                                                                      │
│     If no rules matched:                                            │
│     return null;                                                    │
│     Line: 55                                                        │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 14. STORE AUDIT LOG (ALWAYS)                                        │
│     AuditMessageRepository.SaveAsync()                              │
│     Location: src/AuditSync.OracleConsumer.Infrastructure/         │
│               Repositories/AuditMessageRepository.cs                │
│     Line: 183-186                                                   │
│                                                                      │
│     MERGE INTO audit_logs                                           │
│     ON (ID = :Id)  -- Composite: SessionId_EntryId_Statement        │
│     WHEN MATCHED THEN UPDATE SET ...                                │
│     WHEN NOT MATCHED THEN INSERT ...                                │
│                                                                      │
│     All audit logs stored, regardless of rule match                 │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 15. CREATE CASE (IF RULE MATCHED)                                   │
│     if (matchedRule != null && matchedRule.Matched &&               │
│         matchedRule.Actions.CreateCase)                             │
│     Line: 189                                                       │
│                                                                      │
│     var caseEntity = new Case                                       │
│     {                                                                │
│         Id = $"case-{Guid.NewGuid()}",                              │
│         AuditLogId = auditMessage.Id,                               │
│         CaseStatus = "OPEN",                                        │
│         Valid = null,  // Never set by consumer                     │
│         CreatedAt = DateTime.UtcNow,                                │
│         UpdatedAt = DateTime.UtcNow                                 │
│     };                                                               │
│     Line: 192-200                                                   │
│                                                                      │
│     CaseRepository.CreateAsync(caseEntity)                          │
│     Location: src/AuditSync.OracleConsumer.Infrastructure/         │
│               Repositories/CaseRepository.cs                        │
│     Line: 202                                                       │
│                                                                      │
│     INSERT INTO cases (ID, AUDIT_LOG_ID, CASE_STATUS, VALID, ...)  │
│     VALUES (:Id, :AuditLogId, 'OPEN', NULL, ...)                    │
│                                                                      │
│     UNIQUE constraint on AUDIT_LOG_ID prevents duplicates           │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 16. STORE EXTRACTIONS (IF ANY)                                      │
│     if (matchedRule.Extractions != null &&                          │
│         matchedRule.Extractions.Count > 0)                          │
│     Line: 205                                                       │
│                                                                      │
│     Populate metadata:                                              │
│     ──────────────────                                              │
│     foreach (var extraction in matchedRule.Extractions)             │
│     {                                                                │
│         extraction.AuditLogId = auditMessage.Id;                    │
│         extraction.RuleId = matchedRule.RuleId;                     │
│         extraction.RuleName = matchedRule.RuleName;                 │
│         // SourceField already set in step 11                       │
│         // Type, Value, Tags set by JavaScript in step 10           │
│     }                                                                │
│     Line: 208-213                                                   │
│                                                                      │
│     RuleExtractionRepository.InsertExtractionsAsync()               │
│     Location: src/AuditSync.OracleConsumer.Infrastructure/         │
│               Repositories/RuleExtractionRepository.cs              │
│     Line: 215-218                                                   │
│                                                                      │
│     For each extraction:                                            │
│     ────────────────────                                            │
│     var tagsJson = JsonSerializer.Serialize(extraction.Tags);       │
│                                                                      │
│     INSERT INTO case_extractions (                                  │
│         ID, CASE_ID, AUDIT_LOG_ID, RULE_ID, RULE_NAME,             │
│         EXTRACTION_TYPE, SOURCE_FIELD, EXTRACTION_VALUE, TAGS       │
│     ) VALUES (                                                       │
│         :Id, :CaseId, :AuditLogId, :RuleId, :RuleName,             │
│         :Type, :SourceField, :Value, :TagsJson                      │
│     )                                                                │
│                                                                      │
│     Tags stored as JSON string: '["tag1","tag2"]'                   │
└────────────────────────────┬────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│ 17. COMMIT KAFKA OFFSET                                             │
│     KafkaConsumer.Commit(consumeResult)                             │
│     Line: 143                                                       │
│                                                                      │
│     Offset committed ONLY after successful database writes          │
│     If any step failed → exception thrown → offset NOT committed    │
│     → Kafka will redeliver message                                  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Key Code Locations

| Component | File Path | Key Methods |
|-----------|-----------|-------------|
| **Main Loop** | `src/AuditSync.OracleConsumer.App/Services/AuditConsumerBackgroundService.cs` | `ExecuteAsync()` (line 70)<br>`ProcessWithRulesEngineAsync()` (line 171) |
| **Rules Engine** | `src/AuditSync.OracleConsumer.Application/Services/RulesEngineService.cs` | `EvaluateRulesAsync()` (line 30)<br>`EvaluateRuleAsync()` (line 62)<br>`EvaluateCondition()` (line 123) |
| **JavaScript Execution** | `src/AuditSync.OracleConsumer.Application/Services/JavaScriptExtractor.cs` | `ExecuteExtraction()` (line 24)<br>`ParseJintResult()` (line 73) |
| **Rules Cache** | `src/AuditSync.OracleConsumer.Application/Services/RulesCache.cs` | `GetRulesAsync()` (line 40)<br>`RefreshRulesAsync()` (line 78) |
| **Case Storage** | `src/AuditSync.OracleConsumer.Infrastructure/Repositories/CaseRepository.cs` | `CreateAsync()` (line 27) |
| **Extraction Storage** | `src/AuditSync.OracleConsumer.Infrastructure/Repositories/RuleExtractionRepository.cs` | `InsertExtractionsAsync()` (line 29) |

---

## Operator Implementations

All operators are case-insensitive and located in `RulesEngineService.cs` lines 196-321:

| Operator | Method | Line | Logic |
|----------|--------|------|-------|
| `equals` | `EvaluateEquals()` | 198 | Case-insensitive string comparison |
| `not_equals` | `!EvaluateEquals()` | 146 | Negation of equals |
| `contains` | `EvaluateContains()` | 206 | Case-insensitive substring search |
| `regex` | `EvaluateRegex()` | 213 | Regex match with 1s timeout |
| `in` | `EvaluateIn()` | 234 | Value exists in array/CSV/JSON array |
| `not_in` | `!EvaluateIn()` | 150 | Negation of in |
| `gt` | `EvaluateGreaterThan()` | 271 | Numeric greater than |
| `lt` | `EvaluateLessThan()` | 284 | Numeric less than |
| `gte` | `EvaluateGreaterThanOrEqual()` | 297 | Numeric greater than or equal |
| `lte` | `EvaluateLessThanOrEqual()` | 310 | Numeric less than or equal |

---

## Example: Complete Execution Trace

**Scenario**: Detect MSISDN access in SQL queries

**Rule Configuration**:
```json
{
    "id": "rule-msisdn-001",
    "name": "Detect MSISDN Query",
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
                "extractionLogic": "
                    const regex = /msisdn\\s*=\\s*'(\\d{13})'/gi;
                    const matches = [];
                    let match;
                    while ((match = regex.exec(value)) !== null) {
                        matches.push({
                            value: match[1],
                            type: 'MSISDN',
                            tags: ['query', 'subscriber']
                        });
                    }
                    return matches;
                "
            }
        }
    ],
    "actions": {
        "createCase": true
    }
}
```

**Audit Message**:
```json
{
    "id": "12345_67890_1",
    "target": "DWH",
    "dbUser": "APP_USER",
    "name": "SUBSCRIBERS",
    "sqlText": "SELECT * FROM subscribers WHERE msisdn = '9647508282748'"
}
```

**Execution Trace**:

1. **Load Rules**: `RulesCache.GetRulesAsync()` returns 2 rules sorted by ORDER_POSITION
2. **Evaluate Rule 1** (`rule-msisdn-001`):
   - **Condition 1** (order: 1):
     - Field: `name`
     - Operator: `equals`
     - Actual: `"SUBSCRIBERS"` (from `auditLog.Name`)
     - Expected: `"SUBSCRIBERS"`
     - Result: **TRUE** ✓
     - Extract: `false` → No JavaScript
   - **Condition 2** (order: 2):
     - Field: `sqlText`
     - Operator: `contains`
     - Actual: `"SELECT * FROM subscribers WHERE msisdn = '9647508282748'"`
     - Expected: `"msisdn"`
     - Result: **TRUE** ✓ (case-insensitive)
     - Extract: `true` → **Execute JavaScript**
       - Input `value`: The SQL text
       - Input `auditLog`: Full audit message
       - JavaScript executes regex extraction
       - Returns: `[{ value: "9647508282748", type: "MSISDN", tags: ["query", "subscriber"] }]`
       - Extraction collected with `SourceField = "sqlText"`
   - **All conditions passed** → Rule matched!
3. **Return Result**: `RuleMatchResult` with 1 extraction
4. **Short-circuit**: Rule 2 is never evaluated
5. **Store audit log**: `audit_logs` table (MERGE)
6. **Create case**: `cases` table with `CASE_STATUS='OPEN'`, `VALID=NULL`
7. **Store extraction**: `case_extractions` table:
   - `EXTRACTION_VALUE = "9647508282748"`
   - `EXTRACTION_TYPE = "MSISDN"`
   - `SOURCE_FIELD = "sqlText"`
   - `TAGS = '["query","subscriber"]'` (JSON string)
   - `RULE_NAME = "Detect MSISDN Query"` (denormalized)
8. **Commit offset**: Kafka offset committed

**Result**: Case created with 1 MSISDN extraction, offset committed, ready for next message.

---

## Error Handling & Graceful Degradation

| Error Type | Location | Behavior |
|------------|----------|----------|
| **Empty operator** | `RulesEngineService.cs:135` | Return `false`, log warning, condition fails |
| **Regex timeout** | `RulesEngineService.cs:222` | Return `false`, log warning, condition fails |
| **JavaScript timeout** | `JavaScriptExtractor.cs:61` | Return empty list `[]`, log warning |
| **JavaScript error** | `JavaScriptExtractor.cs:65` | Return empty list `[]`, log error |
| **Invalid extraction object** | `JavaScriptExtractor.cs:92` | Skip item, log warning, continue |
| **DB failure (cache refresh)** | `RulesCache.cs:106` | Use stale cache, log warning |
| **DB failure (no cache)** | `RulesCache.cs:111` | Throw exception, message redelivered |
| **Case insert duplicate** | `CaseRepository.cs` | Unique constraint violation caught upstream |
