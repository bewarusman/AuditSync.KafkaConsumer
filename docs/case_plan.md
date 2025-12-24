# Case-Based Architecture Plan

## Overview

This plan outlines the architectural changes to implement a case-based system for audit logs. Cases are created when extraction rules successfully extract values from audit log data.

> **📋 Note**: For complete database schema with examples, sample data, and concrete implementation details, see [data.md](../data.md)

## Concept

A **case** represents an audit log from which we successfully extracted sensitive or important data. Cases are created when:
- An audit_log is processed
- Extraction rules are applied to the audit_log fields
- **ANY** extraction rule successfully extracts a value

## Current Architecture

1. **audit_logs** - Stores complete audit messages from Kafka
2. **audit_log_extracted_values** - Stores extracted (name, value) pairs based on regex rules
3. **targets** - Stores target information
4. **target_rules** - Stores extraction rules for each target

## New Architecture

### Tables

1. **audit_logs** - Unchanged - Stores complete audit messages from Kafka
2. **cases** - NEW - Stores cases created when extraction succeeds
3. **targets** - Unchanged - Stores target information
4. **target_rules** - Unchanged - Stores extraction rules (regex patterns)
5. **case_extractions** - NEW - Replaces audit_log_extracted_values, stores extracted values with rule info

### Removed Tables

- **audit_log_extracted_values** - Replaced by case_extractions
- **target_case_rules** - Not needed (extraction success determines case creation)
- **target_case_rule_conditions** - Not needed
- **case_triggered_rules** - Not needed (merged into case_extractions)

## Database Schema

### Table: cases

```sql
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
```

### Table: case_extractions

**Purpose**: Stores extracted values ONLY for cases, with denormalized rule information

```sql
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

## Data Flow

### Processing Flow

```
Step 1: Find Target
├─ Extract target name from Kafka message
└─ Lookup target in targets table

Step 2: Load Extraction Rules
├─ Load all active extraction rules for this target from target_rules
└─ Order by RULE_ORDER ASC

Step 3: Store Audit Log
├─ Insert/Update audit_log in audit_logs table using MERGE (UPSERT)
└─ Deduplication: If same ID arrives again, updates all fields + increments PROCESS_COUNTER

Step 4: Apply Extraction Rules
├─ For each active rule (in RULE_ORDER):
│  ├─ Apply regex pattern to the specified SOURCE_FIELD
│  └─ If extraction succeeds, store the extracted value temporarily
├─ If NO values were extracted → Skip to next message (no case created)
└─ If ANY values were extracted → Continue to Step 5

Step 5: Create Case
├─ Check if case already exists for this AUDIT_LOG_ID
├─ If case exists → Skip (due to reprocessing, no error thrown)
└─ If case doesn't exist → Create new case in cases table with status 'OPEN' and VALID = NULL

Step 6: Store Extracted Values
└─ For each extracted value:
   ├─ Insert into case_extractions table
   ├─ Include CASE_ID, AUDIT_LOG_ID, RULE_ID
   ├─ Denormalize RULE_NAME, REGEX_PATTERN, SOURCE_FIELD from the rule
   └─ Store FIELD_VALUE (the extracted value)
```

### Detailed Example

**Input**: Kafka message with SQL text: `"SELECT * FROM cdr.msc WHERE msisdn = '9647507703030' AND imsi = '418576891839'"`

**Target**: "CDR Database"

**Rules for this target**:
- Rule 1 (msisdn): Pattern `\b964(7[0-9]|73|74|75|76|77|78|79)\d{8}\b` on field `text`
- Rule 2 (imsi): Pattern `\b418\d{9}\b` on field `text`

**Processing**:
1. Find target "CDR Database" → target_id = 'target-2'
2. Load rules for 'target-2' → [Rule 1, Rule 2]
3. Store audit_log → audit_logs table
4. Apply Rule 1 to 'text' field:
   - Match found: '9647507703030'
   - Store temporarily: {rule: 'rule-5', name: 'msisdn', pattern: '\b964...', value: '9647507703030'}
5. Apply Rule 2 to 'text' field:
   - Match found: '418576891839'
   - Store temporarily: {rule: 'rule-6', name: 'imsi', pattern: '\b418\d{9}\b', value: '418576891839'}
6. Check: extracted_values.count > 0 → TRUE
7. Create case → cases table (case-uuid-1)
8. Store extractions:
   - Insert case_extractions: msisdn = 9647507703030, rule = rule-5, pattern = '\b964...'
   - Insert case_extractions: imsi = 418576891839, rule = rule-6, pattern = '\b418\d{9}\b'

**Result**:
- 1 audit_log
- 1 case
- 2 extractions (msisdn, imsi) with their respective rule info

## Key Differences from Old Architecture

| Aspect | Old Architecture | New Architecture |
|--------|-----------------|------------------|
| Case creation trigger | Separate case rules (complex expressions) | Extraction success (simpler) |
| Rule types | Two: case rules + extraction rules | One: extraction rules only |
| Rule tracking | Separate table (case_triggered_rules) | Merged into case_extractions |
| Extraction storage | Simple table (field name + value) | Denormalized (includes rule info) |
| Case-extraction link | One case → many extractions | Same (one case → many extractions) |
| Audit trail | Limited (which case rules matched) | Complete (which rule, pattern, source field) |

## Benefits of This Architecture

1. **Simplicity**
   - No complex rule expressions (AND/OR/parentheses)
   - No separate case rules vs extraction rules
   - Extraction success determines case creation
   - Easier to understand and maintain

2. **Complete Audit Trail**
   - Each extraction stores which rule extracted it (RULE_ID)
   - Rule name stored for easy querying (RULE_NAME)
   - Regex pattern stored for historical reference (REGEX_PATTERN)
   - Source field stored to know where value came from (SOURCE_FIELD)
   - Easy to see what pattern matched what value

3. **Traceability**
   - One-to-one relationship: audit_log → case
   - Many-to-one relationship: extractions → case
   - Clear lineage from Kafka message to extracted values
   - Can answer: "Which rule extracted this MSISDN?"

4. **Flexibility**
   - Easy to add new extraction rules
   - No need to define complex case rule expressions
   - Rules are independent (each tries to extract)
   - Query by rule name, pattern, or value

5. **Performance**
   - Single table for extractions (vs separate tracking table)
   - Fewer joins needed for queries
   - Denormalized data for faster reads

## Implementation Phases

### Phase 1: Database Schema Changes ✅
- [x] Create `cases` table
- [x] Create `case_extractions` table (replaces audit_log_extracted_values)
- [x] Remove `target_case_rules` table (not needed)
- [x] Remove `target_case_rule_conditions` table (not needed)
- [x] Remove `case_triggered_rules` table (not needed)
- [x] Keep `target_rules` table (existing extraction rules)
- [x] Create all necessary indexes
- [x] Create migration scripts

### Phase 2: Remove Old Extraction Code ✅
**CRITICAL**: Replaced existing audit_log extraction logic with case-based flow
- [x] Updated AuditConsumerBackgroundService to use new case-based flow
- [x] Kept AuditMessageRepository as-is (still needed for audit_logs table)
- [x] Old extraction logic replaced with new ExtractionService and CaseService
- Note: Old IExtractedValuesRepository and AuditDataService remain for backward compatibility

### Phase 3: Domain Model Updates ✅
- [x] Add `Case` entity
- [x] Add `CaseExtraction` entity (replaces AuditLogExtractedValue)
- [x] No TargetCaseRule entity needed (simpler architecture)
- [x] No TargetCaseRuleCondition entity needed (simpler architecture)
- [x] Keep `TargetRule` entity (existing)
- [x] Created ICaseRepository and CaseRepository
- [x] Created ICaseExtractionRepository and CaseExtractionRepository

### Phase 4: Extraction Service Implementation ✅
- [x] Create `ExtractionService` class
  - [x] Method: `ApplyRulesAsync(AuditMessage auditMessage, List<ExtractionRule> rules)`
  - [x] Returns: `List<ExtractedValue>` (temporary DTOs)
  - [x] Logic: Apply each regex to SOURCE_FIELD with timeout, collect matches
- [x] Create `CaseService` class
  - [x] Method: `CreateCaseWithExtractionsAsync(AuditMessage auditMessage, List<ExtractedValue> values)`
  - [x] Logic: Create case + store extractions with denormalized rule info
  - [x] Handle reprocessing (skip if case exists, returns null)

### Phase 5: Service Layer Integration ✅
- [x] Update `AuditConsumerBackgroundService`
  - [x] After storing audit_log, load extraction rules
  - [x] Call ExtractionService to apply rules
  - [x] If extractions found, call CaseService
  - [x] If no extractions, skip to next message (log debug message)
- [x] Removed old IRuleEngine and IAuditDataService dependencies
- [x] Added new dependencies: IAuditMessageRepository, IRuleRepository, IExtractionService, ICaseService
- [x] Updated dependency injection in Program.cs

### Phase 6: Testing ✅
- [x] Fixed unit tests for AuditConsumerBackgroundService
  - [x] Updated constructor calls with new dependencies
  - [x] Tests now compile and pass
- [x] Unit tests for ExtractionService (13 comprehensive tests)
  - [x] Test extraction with single rule
  - [x] Test extraction with multiple rules
  - [x] Test no matches scenario
  - [x] Test extraction from different source fields
  - [x] Test special characters handling
  - [x] Test rule ordering
  - [x] Test edge cases (empty fields, null values, no capturing groups)
- [x] Unit tests for CaseService (14 comprehensive tests)
  - [x] Test case creation with extractions
  - [x] Test denormalization of rule info
  - [x] Test reprocessing (case already exists)
  - [x] Test multiple extractions
  - [x] Test VALID field defaults to NULL
  - [x] Test case status defaults to OPEN
  - [x] Test timestamp handling
  - [x] Test unique ID generation
- [x] All 71 unit tests passing
- Note: Integration and performance tests can be added in future as needed

## Configuration Considerations

### appsettings.json additions

```json
{
  "CaseManagement": {
    "EnableCaseCreation": true,
    "MaxExtractionsPerCase": 50,
    "RegexTimeoutMs": 100
  }
}
```

## Design Decisions

### 1. Reprocessing Behavior ✅
**Decision**: If an audit_log is reprocessed and already has a case, **do NOT create another case**

**Implementation**:
- The UNIQUE constraint on `AUDIT_LOG_ID` in the `cases` table prevents duplicates
- Before creating case, check if case exists for this audit_log_id
- If exists, skip case creation gracefully (log and continue, no error)
- This ensures idempotency

### 2. VALID Field Usage ✅
**Decision**: The consumer will **NEVER** set the VALID field

**Behavior**:
- VALID always defaults to NULL when case is created
- This field is for manual review/validation by users later
- Business users will update VALID to 'YES' or 'NO' through a separate UI/process
- Consumer only creates cases with OPEN status and VALID = NULL

### 3. Denormalization Strategy ✅
**Decision**: Store rule information (name, pattern, source field) in case_extractions

**Rationale**:
- **Audit trail**: Historical record of which exact rule extracted the value
- **Rule changes**: If rule regex is updated, old extractions show original pattern
- **Performance**: No need to join with target_rules for queries
- **Simplicity**: All extraction context in one table

**Trade-offs**:
- Storage: More space (acceptable for audit system)
- Consistency: Denormalized data (acceptable since it's historical)
- Benefits outweigh costs for this use case

### 4. Case Creation Logic ✅
**Decision**: Create case if **ANY** extraction succeeds (not ALL)

**Rationale**:
- More inclusive (captures partial matches)
- Simpler logic (no need for "required" flags on rules)
- Better for audit trail (even one sensitive value warrants investigation)

**Example**:
```
Rules: [msisdn, imsi, status_id]
Audit_log: "SELECT * WHERE msisdn = '9647507703030'"

Result:
- msisdn extracted ✓
- imsi not found ✗
- status_id not found ✗

Action: Create case (because msisdn was extracted)
```

### 5. Existing Data Cleanup ✅
**Decision**: **Leave existing** `audit_log_extracted_values` records as-is

**Rationale**:
- No migration needed (simpler deployment)
- User will manually delete them later
- New architecture only applies to future data
- Clear cutoff point (before/after implementation)

## Migration Strategy

### For Existing Data
1. Keep existing audit_logs and audit_log_extracted_values as-is
2. New data will follow the case-based approach (extraction → case → case_extractions)
3. Optionally backfill cases for historical data if needed (user decision)

### Database Migration Script Order
```sql
-- 1. Create new tables
CREATE TABLE cases (...);
CREATE TABLE case_extractions (...);

-- 2. Create indexes
CREATE INDEX IDX_CASES_STATUS ON cases(CASE_STATUS);
-- ... (all indexes)

-- 3. (Optional) Leave old tables for reference
-- audit_log_extracted_values remains (user will delete manually)

-- 4. (Future) Drop old tables when ready
-- DROP TABLE audit_log_extracted_values;
```

## Example Queries

### Find all cases with their extractions
```sql
SELECT
  c.ID as CASE_ID,
  c.CASE_STATUS,
  c.VALID,
  ce.RULE_NAME,
  ce.REGEX_PATTERN,
  ce.SOURCE_FIELD,
  ce.FIELD_VALUE
FROM cases c
JOIN case_extractions ce ON c.ID = ce.CASE_ID
WHERE c.CASE_STATUS = 'OPEN'
ORDER BY c.CREATED_AT DESC;
```

### Find which rule extracted a specific value
```sql
SELECT
  ce.RULE_NAME,
  ce.REGEX_PATTERN,
  ce.SOURCE_FIELD,
  ce.FIELD_VALUE,
  ce.EXTRACTED_AT,
  c.CASE_STATUS
FROM case_extractions ce
JOIN cases c ON ce.CASE_ID = c.ID
WHERE ce.FIELD_VALUE = '9647507703030';
```

### Find most frequently triggered rules
```sql
SELECT
  RULE_NAME,
  REGEX_PATTERN,
  COUNT(*) as extraction_count
FROM case_extractions
GROUP BY RULE_NAME, REGEX_PATTERN
ORDER BY extraction_count DESC;
```

### Find cases with multiple extractions
```sql
SELECT
  c.ID,
  c.AUDIT_LOG_ID,
  COUNT(ce.ID) as extraction_count
FROM cases c
JOIN case_extractions ce ON c.ID = ce.CASE_ID
GROUP BY c.ID, c.AUDIT_LOG_ID
HAVING COUNT(ce.ID) > 1
ORDER BY extraction_count DESC;
```

## Implementation Checklist

### Code Components to Create ✅
- [x] `CaseExtraction` entity (Domain/Entities/CaseExtraction.cs)
- [x] `Case` entity (Domain/Entities/Case.cs)
- [x] `ICaseRepository` interface (Domain/Interfaces/ICaseRepository.cs)
- [x] `ICaseExtractionRepository` interface (Domain/Interfaces/ICaseExtractionRepository.cs)
- [x] `CaseRepository` class (Infrastructure/Repositories/CaseRepository.cs)
- [x] `CaseExtractionRepository` class (Infrastructure/Repositories/CaseExtractionRepository.cs)
- [x] `ExtractionService` class (Application/Services/ExtractionService.cs)
- [x] `CaseService` class (Application/Services/CaseService.cs)
- [x] `IExtractionService` interface (Domain/Interfaces/IExtractionService.cs)
- [x] `ICaseService` interface (Domain/Interfaces/ICaseService.cs)
- [x] `ExtractedValue` DTO (Domain/Models/ExtractedValue.cs)
- [x] Update `AuditConsumerBackgroundService` (App/Services/AuditConsumerBackgroundService.cs)
- [x] Update `Program.cs` dependency injection

### Code Components to Remove ✅
- [x] Old extraction logic in `AuditConsumerBackgroundService` (replaced with new flow)
- Note: No TargetCaseRule or TargetCaseRuleCondition entities existed (simplified architecture)
- Note: Old ExtractedData and related services kept for backward compatibility if needed

### Testing Requirements ✅
- [x] Updated unit tests for `AuditConsumerBackgroundService` with new dependencies
- [x] Unit tests for `ExtractionService.ApplyRulesAsync` (13 tests covering all scenarios)
  - [x] Extraction with regex matches
  - [x] Multiple rules extraction
  - [x] No matches scenarios
  - [x] Different source fields (text, bindVariables, owner, etc.)
  - [x] Special characters in SQL
  - [x] Rule ordering
  - [x] Edge cases (empty fields, null values, no capturing groups)
- [x] Unit tests for `CaseService.CreateCaseWithExtractionsAsync` (14 tests)
  - [x] Case creation with single and multiple extractions
  - [x] Denormalization of rule info (name, pattern, source field)
  - [x] Reprocessing behavior (returns null if case exists)
  - [x] VALID field defaults to NULL
  - [x] Case status defaults to OPEN
  - [x] Timestamp handling
  - [x] Unique ID generation for cases and extractions
  - [x] Proper linking between cases and extractions
- [x] All 71 unit tests passing (0 failures)
- Note: Integration and performance tests available for future enhancement if needed
