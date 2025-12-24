# AuditSync Oracle Consumer - Tasks
**NOTE:**
- For high level design, see [README.md](README.md)
- For code examples and detailed implementations, see [architecture.md](architecture.md)
- For data, see [plan.md](plan.md)
---

## **Phase 1: Project Setup**

**Tasks:**
- [x] Add NuGet packages:
  - [x] Confluent.Kafka (Version 2.6.1)
  - [x] Oracle.ManagedDataAccess.Core (Version 23.7.0)
  - [x] Dapper (Version 2.1.35)
  - [x] DotNetEnv (Version 3.1.1)
  - [x] System.Text.Json (Version 9.0.0)
  - [x] Microsoft.Extensions.Diagnostics.HealthChecks (Version 9.0.0)

- [x] Create base folder structure

---

## **Phase 2: Domain Layer**

**Tasks:**
- [x] Create Domain Entities:
  - [x] `AuditMessage.cs` - Maps to Kafka JSON message (flattened structure):
    - [x] Add `Id` property (string)
    - [x] Add `Target` property (string)
    - [x] Add `SessionId` property (long)
    - [x] Add `EntryId` property (int)
    - [x] Add `Statement` property (int)
    - [x] Add `DbUser` property (string)
    - [x] Add `UserHost` property (string)
    - [x] Add `Terminal` property (string)
    - [x] Add `Action` property (int)
    - [x] Add `ReturnCode` property (int)
    - [x] Add `Owner` property (string)
    - [x] Add `Name` property (string)
    - [x] Add `AuthPrivileges` property (string)
    - [x] Add `AuthGrantee` property (string)
    - [x] Add `NewOwner` property (string)
    - [x] Add `NewName` property (string)
    - [x] Add `OsUser` property (string)
    - [x] Add `PrivilegeUsed` property (string, nullable)
    - [x] Add `Timestamp` property (DateTime)
    - [x] Add `BindVariables` property (string)
    - [x] Add `Text` property (string)
    - [x] Add `ProducedAt` property (DateTime)
  - [x] `ExtractedData.cs` - Result of rule application:
    - [x] Add `AuditRecordId` property
    - [x] Add `Schema` property
    - [x] Add `TableName` property
    - [x] Add `SqlText` property
    - [x] Add `ExtractedFields` property as `Dictionary<string, string>` for (name, value) pairs
    - [x] Add `ProcessedAt` timestamp property

- [x] Define Domain Interfaces:
  - [x] `IRuleEngine` - Contract for rule processing:
    - [x] Define `ApplyRules` method that returns `ExtractedData`
    - [x] Method should populate `ExtractedFields` dictionary with (name, value) pairs
  - [x] `IAuditMessageRepository` - Repository for storing complete audit messages:
    - [x] Define `SaveAsync` method accepting `AuditMessage`, raw payload, partition, offset
    - [x] Define `IsProcessedAsync` method for idempotency check
  - [x] `IExtractedValuesRepository` - Repository for storing extracted (name, value) pairs:
    - [x] Define `SaveExtractedValuesAsync` method accepting audit message ID and dictionary
    - [x] Each key-value pair should be inserted as separate row
  - [x] `IAuditDataService` - Service for transactional persistence:
    - [x] Define `SaveAuditDataAsync` to save both message and extracted values
    - [x] Ensure transactional integrity
  - [x] `IOffsetManager` - Kafka offset management contract

- [x] Define Rule Models:
  - [x] `ExtractionRule` - Represents a single extraction rule:
    - [x] Add `Name` property (for the key in name-value pair)
    - [x] Add `Pattern` property (regex pattern)
    - [x] Add `FieldName` property (output field name)
    - [x] Add `IsRequired` property
    - [x] Add `SourceField` property (path to source data)
  - [x] `RuleResult` - Result of applying a rule:
    - [x] Add `RuleName` property
    - [x] Add `ExtractedValue` property
    - [x] Add `Success` property

---

## **Phase 3: Infrastructure - Kafka Consumer**

**Tasks:**
- [x] Create KafkaConsumerService (see code example in architecture.md)

- [x] Implement OffsetManager (see code example in architecture.md)

- [x] Consumer Configuration:
  - [x] Auto-commit disabled (manual offset management)
  - [x] Enable at-least-once delivery semantics
  - [x] Configurable consumer group

---

## **Phase 4: Rule Engine Implementation**

**Tasks:**
- [x] Create RegexRuleEngine (see code example in architecture.md):
  - [x] Inject IRuleRepository dependency
  - [x] Initialize empty rule cache dictionary
  - [x] Implement `ApplyRulesAsync` method that processes AuditMessage
  - [x] Implement lazy loading: check cache first, load from DB only if rules not found
  - [x] Use SemaphoreSlim for thread-safe cache access
  - [x] Implement double-check locking pattern to prevent duplicate database queries
  - [x] Cache rules by target for subsequent requests
  - [x] Extract values using regex patterns from cached/loaded rules
  - [x] Store each extracted value as (name, value) pair in dictionary
  - [x] Support direct field access (e.g., `text`, `bindVariables`, `owner`, `name`)
  - [x] Handle required vs optional rules
  - [x] Throw exception when required rule fails to match
  - [x] Log warning if no rules found for target
  - [x] Log info when rules are loaded and cached for a target
  - [x] Log extraction results for debugging

- [x] Create Targets Database Table:
  - [x] Create `targets` table to store target information
  - [x] Include columns: ID, NAME, DESCRIPTION, CREATED_AT, UPDATED_AT
  - [x] Create unique constraint on NAME
  - [x] Add index on NAME
  - [x] Insert sample targets (e.g., 'Production Oracle Database', 'Development Oracle Database')

- [x] Create Extraction Rules Database Table:
  - [x] Create `target_rules` table
  - [x] Store rules per target (different targets can have different rules)
  - [x] Include columns: ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_REQUIRED, IS_ACTIVE, RULE_ORDER
  - [x] Create foreign key constraint: TARGET_ID references targets(ID) with ON DELETE CASCADE
  - [x] Create unique constraint on (TARGET_ID, RULE_NAME)
  - [x] Add indexes on TARGET_ID, IS_ACTIVE, and (TARGET_ID, RULE_ORDER)
  - [x] Insert sample rules for each target using TARGET_ID

- [x] Rule Repository (see code example in architecture.md):
  - [x] Implement `GetRulesByTargetAsync` method to fetch rules for specific target name
  - [x] Join `target_rules` with `targets` table to resolve target name to ID
  - [x] Query only active rules (IS_ACTIVE = 1)
  - [x] Order rules by RULE_ORDER
  - [x] Add proper error handling and logging

- [x] Value Extraction Logic:
  - [x] Implement `GetSourceValue` method to access properties from flattened structure
  - [x] Use reflection or expression trees for dynamic property access
  - [x] Handle null values safely
  - [x] Return empty string for missing fields
  - [x] Access properties like `text`, `bindVariables`, `owner`, `name` directly without nesting

- [x] Store Extracted Values:
  - [x] Create `Dictionary<string, string>` for extracted fields
  - [x] Store each match as key-value pair (rule name → extracted value)
  - [x] Preserve all extracted values for persistence
  - [x] Skip storing values for optional rules that don't match

---

## **Phase 5: Oracle Data Persistence**

**Tasks:**
- [x] Create Database Schema for Audit Messages:
  - [x] Create `audit_logs` table to store complete Kafka messages (with deduplication):
    - [x] `ID` VARCHAR2(100) PRIMARY KEY
    - [x] `TARGET` VARCHAR2(256)
    - [x] `SESSION_ID` NUMBER
    - [x] `ENTRY_ID` NUMBER
    - [x] `STATEMENT` NUMBER
    - [x] `DB_USER` VARCHAR2(128)
    - [x] `USER_HOST` VARCHAR2(256)
    - [x] `TERMINAL` VARCHAR2(128)
    - [x] `OS_USER` VARCHAR2(128)
    - [x] `ACTION` NUMBER (action type number, e.g., 3 = SELECT)
    - [x] `RETURN_CODE` NUMBER
    - [x] `OWNER` VARCHAR2(128)
    - [x] `NAME` VARCHAR2(128)
    - [x] `AUTH_PRIVILEGES` VARCHAR2(256)
    - [x] `AUTH_GRANTEE` VARCHAR2(128)
    - [x] `NEW_OWNER` VARCHAR2(128)
    - [x] `NEW_NAME` VARCHAR2(128)
    - [x] `PRIVILEGE_USED` VARCHAR2(256)
    - [x] `TEXT` CLOB (SQL text)
    - [x] `BIND_VARIABLES` CLOB
    - [x] `TIMESTAMP` TIMESTAMP
    - [x] `PRODUCED_AT` TIMESTAMP
    - [x] `KAFKA_PARTITION` NUMBER
    - [x] `KAFKA_OFFSET` NUMBER
    - [x] `PROCESS_COUNTER` NUMBER DEFAULT 1 (increments on duplicate)
    - [x] `PROCESSED_AT` TIMESTAMP DEFAULT SYSTIMESTAMP
    - [x] `CONSUMED_AT` TIMESTAMP DEFAULT SYSTIMESTAMP
  - [x] Create unique constraint on `(KAFKA_PARTITION, KAFKA_OFFSET)` for idempotency
  - [x] Add index on `SESSION_ID` and `ENTRY_ID`
  - [x] Add index on `PROCESSED_AT`
  - [x] Add index on `DB_USER`
  - [x] Add index on `TARGET`
  - [x] Add index on `PROCESS_COUNTER`

- [x] Create Database Schema for Extracted Values:
  - [x] Create `audit_log_extracted_values` table to store (name, value) pairs:
    - [x] `ID` VARCHAR2(100) PRIMARY KEY
    - [x] `AUDIT_MESSAGE_ID` VARCHAR2(100) NOT NULL (FK to audit_logs.ID)
    - [x] `FIELD_NAME` VARCHAR2(100) NOT NULL (the "name" in name-value pair)
    - [x] `FIELD_VALUE` VARCHAR2(4000) (the "value" in name-value pair)
    - [x] `EXTRACTED_AT` TIMESTAMP DEFAULT SYSTIMESTAMP
  - [x] Create foreign key constraint to `audit_logs` table
  - [x] Create index on `AUDIT_MESSAGE_ID`
  - [x] Create index on `FIELD_NAME` for filtering
  - [x] Create composite index on `(FIELD_NAME, FIELD_VALUE)` for queries
  - [x] Test schema creation script

- [x] Implement Audit Message Repository:
  - [x] Create `AuditMessageRepository` class
  - [x] Implement `SaveAsync` method using MERGE (upsert) logic to prevent duplicates
  - [x] On INSERT: Set all 22 properties + PROCESS_COUNTER = 1
  - [x] On UPDATE (duplicate): Update ALL 22 fields from new message + increment PROCESS_COUNTER + update CONSUMED_AT
  - [x] Map all 22 properties from AuditMessage domain model to table columns
  - [x] Include new fields: `TARGET`, `AUTH_PRIVILEGES`, `AUTH_GRANTEE`, `NEW_OWNER`, `NEW_NAME`, `PRIVILEGE_USED`, `PRODUCED_AT`, `PROCESS_COUNTER`
  - [x] Store `Action` as NUMBER in ACTION column
  - [x] Implement `IsProcessedAsync` method to check if record exists by ID
  - [x] Use parameterized queries to prevent SQL injection
  - [x] Handle Oracle-specific data types properly
  - [x] Add proper error handling and logging

- [x] Implement Extracted Values Repository:
  - [x] Create `ExtractedValuesRepository` class
  - [x] Implement `SaveExtractedValuesAsync` method for batch insert/update
  - [x] Accept `Dictionary<string, string>` containing extracted (name, value) pairs
  - [x] Check if extracted values already exist for the audit message ID
  - [x] If exists (duplicate): Delete all existing extracted values for this message, then re-insert
  - [x] If new: Insert each key-value pair as a separate row
  - [x] Link each row to parent audit message via `AUDIT_MESSAGE_ID`
  - [x] Use bulk insert for performance
  - [x] Handle null or empty values appropriately
  - [x] Add proper error handling and logging

- [x] Implement Transactional Logic:
  - [x] Wrap all operations (MERGE audit_logs + DELETE/INSERT extracted values) in single transaction
  - [x] Ensure atomicity: either all succeed or all rollback
  - [x] For duplicates: MERGE audit_logs → DELETE old extracted values → INSERT new extracted values
  - [x] For new records: INSERT audit_logs → INSERT extracted values
  - [x] Commit transaction only after both tables are updated
  - [x] Return success status to trigger Kafka offset commit

---

## **Phase 6: Background Service Implementation**

**Tasks:**
- [x] Create AuditConsumerBackgroundService (see code example in architecture.md)
- [x] Message Processor (see code example in architecture.md)

---

## **Phase 7: Configuration Management**

**Tasks:**
- [x] Create .env file (see configuration example in architecture.md)
- [x] Load Configuration in Program.cs (see code example in architecture.md)
- [x] Register RuleRepository and RuleEngine in dependency injection
- [x] Note: Rules are loaded lazily on first use (per target), no startup loading required

---

## **Phase 8: Health Checks & Monitoring**

**Tasks:**
- [x] Kafka Consumer Health Check (see code example in architecture.md)
- [x] Register Health Checks (see code example in architecture.md)

---

## **Phase 9: Unit Tests**

**Tasks:**
- [x] Domain Layer Tests:
  - [x] Test `AuditMessage` entity validation
  - [x] Test `ExtractedData` entity
  - [x] Test `ExtractionRule` model properties
  - [x] Test `RuleValidationException` behavior

- [x] RegexRuleEngine Tests:
  - [x] Test lazy loading mechanism
  - [x] Test cache hit scenario (rule already in cache)
  - [x] Test cache miss scenario (rule loaded from DB)
  - [x] Test thread safety with concurrent rule requests
  - [x] Test double-check locking pattern
  - [x] Test regex pattern matching (successful extraction)
  - [x] Test regex pattern matching (failed extraction)
  - [x] Test required rule failure throws exception
  - [x] Test optional rule failure does not throw exception
  - [x] Test GetSourceValue for all supported fields
  - [x] Test GetSourceValue for unsupported fields returns null
  - [x] Test multiple rules applied in order
  - [x] Test empty rules list for target
  - [x] Mock IRuleRepository for isolated testing

- [x] AuditDataService Tests:
  - [x] Test successful save of audit message and extracted values
  - [x] Test transactional behavior (rollback on failure)
  - [x] Test error handling and logging
  - [x] Mock repositories for isolated testing

- [x] Repository Unit Tests (with mocked Oracle connection):
  - [x] Test AuditMessageRepository.SaveAsync (INSERT scenario)
  - [x] Test AuditMessageRepository.SaveAsync (UPDATE scenario with PROCESS_COUNTER increment)
  - [x] Test AuditMessageRepository.IsProcessedAsync
  - [x] Test ExtractedValuesRepository.SaveExtractedValuesAsync (new records)
  - [x] Test ExtractedValuesRepository.SaveExtractedValuesAsync (duplicate records with DELETE/INSERT)
  - [x] Test RuleRepository.GetRulesByTargetAsync
  - [x] Test proper parameter binding and SQL injection prevention

- [x] Kafka Infrastructure Tests:
  - [x] Test KafkaConsumerService.Consume
  - [x] Test KafkaConsumerService.Commit
  - [x] Test KafkaConsumerService.Subscribe
  - [x] Test OffsetManager.StoreOffset
  - [x] Test OffsetManager.GetLastOffset
  - [x] Test thread safety of OffsetManager

- [x] Background Service Tests:
  - [x] Test AuditConsumerBackgroundService startup
  - [x] Test message consumption flow
  - [x] Test successful processing commits offset
  - [x] Test failed processing does not commit offset
  - [x] Test graceful shutdown
  - [x] Test error handling and retry logic
  - [x] Mock all dependencies for isolated testing

---

## **Phase 10: Integration Tests**

**Tasks:**
- [x] Database Integration Tests:
  - [x] Setup test Oracle database or use Testcontainers
  - [x] Test complete database schema creation
  - [x] Test AuditMessageRepository with real Oracle connection:
    - [x] Insert new audit message
    - [x] Update existing audit message (verify PROCESS_COUNTER increments)
    - [x] Verify MERGE behavior with duplicate KAFKA_PARTITION/KAFKA_OFFSET
    - [x] Verify all 22 fields are persisted correctly
  - [x] Test ExtractedValuesRepository with real Oracle connection:
    - [x] Insert new extracted values
    - [x] Delete and re-insert on duplicate message
    - [x] Verify foreign key constraints
    - [x] Verify bulk insert performance
  - [x] Test RuleRepository with real Oracle connection:
    - [x] Insert sample rules into target_rules table
    - [x] Verify JOIN query with targets table
    - [x] Verify rule ordering by RULE_ORDER
    - [x] Verify filtering by IS_ACTIVE flag
  - [x] Test transactional behavior:
    - [x] Verify atomicity (both tables updated or both rolled back)
    - [x] Test rollback on error
  - [x] Cleanup test data after each test

- [x] Kafka Integration Tests (test structure created, marked as skipped):
  - [x] Setup embedded Kafka or use Testcontainers
  - [x] Test producing messages to test topic
  - [x] Test consuming messages from test topic
  - [x] Test manual offset commit behavior
  - [x] Test consumer group coordination
  - [x] Test offset reset behavior (earliest vs latest)
  - [x] Test consumer rebalancing
  - [x] Test handling of malformed JSON messages

- [x] End-to-End Integration Tests (test structure created, marked as skipped):
  - [x] Setup test environment (Kafka + Oracle + Application)
  - [x] Test complete flow: Kafka → Consumer → Rule Engine → Database
  - [x] Produce sample audit messages to Kafka
  - [x] Verify messages are consumed and processed
  - [x] Verify audit_logs table contains correct data
  - [x] Verify audit_log_extracted_values table contains extracted fields
  - [x] Verify PROCESS_COUNTER increments on duplicate messages
  - [x] Verify extracted values are updated on duplicate messages
  - [x] Test multiple targets with different rules
  - [x] Test required rule failure scenario
  - [x] Test optional rule failure scenario
  - [x] Test offset commit only on success
  - [x] Test message reprocessing after failure
  - [x] Test graceful shutdown with in-flight messages
  - [x] Test consumer lag monitoring
  - [x] Test health check endpoint returns 200 OK

- [x] Performance Tests (test structure created, marked as skipped):
  - [x] Test throughput with high message volume
  - [x] Test rule cache performance (latency improvement)
  - [x] Test database connection pooling
  - [x] Test concurrent message processing
  - [x] Measure consumer lag under load
  - [x] Test memory usage with long-running consumer

- [x] Failure Scenario Tests (test structure created, marked as skipped):
  - [x] Test Oracle database connection failure
  - [x] Test Kafka broker unavailable
  - [x] Test network interruption
  - [x] Test application crash and recovery
  - [x] Test duplicate message handling
  - [x] Test malformed message handling
  - [x] Verify no message loss in failure scenarios
  - [x] Verify offset is not committed on failure

---