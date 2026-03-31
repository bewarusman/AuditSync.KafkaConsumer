# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run unit tests only
dotnet test tests/AuditSync.OracleConsumer.Test.Unit

# Run integration tests (requires Oracle via Testcontainers)
dotnet test tests/AuditSync.OracleConsumer.Test.Integration

# Run a single test
dotnet test tests/AuditSync.OracleConsumer.Test.Unit --filter "FullyQualifiedName~ExtractionServiceTests"

# Run application
dotnet run --project src/AuditSync.OracleConsumer.App
```

Configuration is loaded from `src/AuditSync.OracleConsumer.App/.env`. Copy `.env.example` to `.env` and fill in values before running.

## Architecture

Clean Architecture with four layers:

- **Domain** — entities, interfaces, models. No external dependencies.
- **Application** — business logic services (rules evaluation, extraction, case management).
- **Infrastructure** — Oracle repositories (Dapper), Kafka consumer.
- **App** — `Program.cs` DI wiring, `AuditConsumerBackgroundService` main loop, HTTP host.

### Data Flow

```
Kafka → AuditConsumerBackgroundService
  → Deserialize AuditMessage
  → Validate target exists (TargetRepository)
  → Dual processing path (see below)
  → Commit Kafka offset ONLY after successful DB write
```

Offsets are committed manually after successful persistence. On failure, the offset is not committed so Kafka redelivers the message.

### Dual Processing Paths

Controlled by `AUDITSYNC_ENABLE_RULES_ENGINE=true/false` in `.env`.

**Rules Engine path** (new, JavaScript-based — use this):
1. Load rules from cache (`RulesCache`, 24h TTL, thread-safe with `SemaphoreSlim`)
2. `RulesEngineService.EvaluateRulesAsync()` — iterates rules by `ORDER_POSITION`, evaluates all conditions with AND logic, stops on first match (short-circuit)
3. Per-condition: if condition passes AND `Extract=true` → execute JavaScript immediately via `JavaScriptExtractor` (Jint engine, 5s timeout, 10MB memory, 10 recursion levels)
4. Store audit log (MERGE/upsert)
5. If rule matched and `actions.createCase=true` → create case + store extractions

**Legacy path** (regex-based, avoid — requires `rule_tags` table that is not used):
1. Store audit log
2. Load extraction rules by target
3. Apply regex patterns (100ms timeout), extract ALL matches
4. Create case if any match

### Rules Engine Condition Evaluation

Rules are evaluated in `ORDER_POSITION` order. Within a rule, all conditions must pass (AND logic). If any condition fails, the rule fails and the next rule is tried. **First matching rule wins — remaining rules are skipped.**

When a condition passes and has `Extract=true` with `ExtractConfig.extractionLogic`, JavaScript runs immediately for that condition. Extractions from all conditions in the matched rule are aggregated.

JavaScript context available: `value` (the condition's field value), `auditLog` (full audit message object), `webhook` (stub, logs only).

JavaScript must return:
```javascript
[{ value: "extracted_string", type: "MSISDN", tags: ["tag1", "tag2"] }]
// or [] for no match
```

Tags are stored as JSON string in `case_extractions.TAGS` (VARCHAR2 column).

**Execution Flow Summary** (see `docs/rules_engine_flow.md` for details):

1. **Entry Point**: `AuditConsumerBackgroundService.ProcessWithRulesEngineAsync()` (line 171)
2. **Load Rules**: `RulesCache.GetRulesAsync()` - cached, thread-safe, 24h TTL
3. **Evaluate Rules**: `RulesEngineService.EvaluateRulesAsync()` (line 30) - loops through rules by `ORDER_POSITION`, first match wins
4. **Evaluate Each Rule**: `RulesEngineService.EvaluateRuleAsync()` (line 62) - loops through conditions by `Order`, ALL must pass (AND logic)
5. **Evaluate Each Condition**: `RulesEngineService.EvaluateCondition()` (line 123) - gets field value (line 131), evaluates operator (line 143), executes JavaScript if `Extract=true` (line 164)
6. **JavaScript Execution**: `JavaScriptExtractor.ExecuteExtraction()` - Jint engine with 5s timeout, 10MB memory, returns `[{value, type, tags}]`
7. **Store Results**: `audit_logs` (always), `cases` (if matched), `case_extractions` (if extractions)
8. **Commit Kafka offset** (only after successful DB writes)

### Key Database Tables

- `audit_logs` — all audit events (MERGE on composite key `SessionId_EntryId_Statement`)
- `cases` — one case per audit log (`AUDIT_LOG_ID` has UNIQUE constraint); status always `OPEN` on creation, `VALID` is never set by the consumer
- `case_extractions` — one row per extracted value; denormalizes `RULE_NAME`, `SOURCE_FIELD`, `EXTRACTION_TYPE`, `EXTRACTION_VALUE`, `TAGS`
- `RULES` / `CONDITIONS` / `ACTIONS` — JavaScript rules engine configuration (CONDITIONS and ACTIONS stored as CLOBs, deserialized as JSON)
- `targets` — valid target system names; messages for unknown targets are skipped

### Key Design Decisions

- **Denormalization**: `RULE_NAME`, source field, and extraction type are stored directly in `case_extractions` so queries don't require joins and historical records survive rule changes.
- **No case without extraction**: Cases are only created when a rule matches. All audit logs are stored regardless.
- **Stale cache fallback**: If the DB fails during rules cache refresh, the stale cache is used rather than throwing. Only throws if no cache exists at all.
- **CLOB handling**: `RulesEngineRepository` handles Oracle CLOB types explicitly (`OracleClob`, `string`, and `DBNull` cases) for reading JSON conditions and actions.
- **Graceful degradation**: Empty/null operators in conditions return `false` (skip rule) with a warning instead of crashing. JavaScript errors return empty extractions instead of crashing.

## Common Issues

**Build fails with "file is being used by another process"**: The app is still running. Stop it first (`Stop-Process` or Ctrl+C), then rebuild.

**"Operator '' is not supported"**: A rule condition in the database has an empty `OPERATOR` field. Check the log warning to identify which rule/field, then fix the database record.

**SSL certificate errors on startup**: Ensure `ca.pem`, `service.cert`, and `service.key` in `src/AuditSync.OracleConsumer.App/certs/` are complete valid PEM files downloaded from Aiven (not truncated from copy-paste).

**"table or view does not exist" for `rule_tags`**: You're in legacy mode but the legacy tables don't exist. Set `AUDITSYNC_ENABLE_RULES_ENGINE=true` in `.env` to use the new JavaScript rules engine instead.
