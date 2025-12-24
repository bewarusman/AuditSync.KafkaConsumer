# Oracle Audit Consumer – Domain-Driven Design Architecture

## 🧩 Architecture Overview

This is a **production-ready, enterprise-grade** Kafka → Oracle Consumer implemented using **Domain-Driven Design (DDD)** and **Clean Architecture** principles.

The consumer is responsible for:

* Consuming audit events from Kafka
* Persisting them into Oracle
* Committing Kafka offsets **only after successful DB write**

---

## ✅ Key Characteristics

* ✅ **Clean DDD architecture**
* ✅ **Manual Kafka offset commit**
* ✅ **Transactional Oracle persistence**
* ✅ **At-least-once consumption**
* ✅ **Effectively exactly-once storage (DB constraint)**
* ❌ No duplication store
* ❌ No record-level tracking
* ❌ No checkpoints

---

## 🗂️ Project Structure

```
OracleAuditKafkaConsumer.DDD/
├── src/
│   ├── OracleAuditConsumer.Domain/            # Domain Layer
│   │   ├── Entities/
│   │   │   └── AuditRecord.cs
│   │   ├── ValueObjects/
│   │   │   └── ValueObjects.cs
│   │   ├── Repositories/
│   │   │   └── IAuditWriteRepository.cs
│   │   └── Services/
│   │       └── AuditValidationService.cs
│   │
│   ├── OracleAuditConsumer.Application/       # Application Layer
│   │   ├── Commands/
│   │   │   └── ConsumeAuditBatchCommand.cs
│   │   └── Interfaces/
│   │       └── IMessageConsumer.cs
│   │
│   ├── OracleAuditConsumer.Infrastructure/    # Infrastructure Layer
│   │   ├── Kafka/
│   │   │   └── KafkaAuditConsumer.cs
│   │   └── Oracle/
│   │       └── OracleAuditWriteRepository.cs
│   │
│   └── OracleAuditConsumer.Worker/             # Presentation Layer
│       └── Worker.cs
│
└── tests/
    ├── Unit/
    └── Integration/
```

---

## 🧠 DDD Patterns Implemented

### 1. **Entity**

`AuditRecord` is the aggregate root.

```csharp
public class AuditRecord : Entity<AuditRecordId>
{
    public decimal SessionId { get; }
    public decimal EntryId { get; }
    public DateTime EventTimestamp { get; }
    public string UserId { get; }

    public bool IsValid() => SessionId > 0 && EntryId > 0;
}
```

---

### 2. **Value Objects**

* `AuditRecordId` (SessionId + EntryId)
* `AuditTimestamp`
* `ActionType`

Immutable, self-validating, equality-based.

---

### 3. **Repository (Write-Only)**

```csharp
public interface IAuditWriteRepository
{
    Task InsertBatchAsync(
        IReadOnlyCollection<AuditRecord> records,
        CancellationToken cancellationToken);
}
```

> Domain does **not** know Oracle exists.

---

### 4. **Domain Services**

```csharp
public interface IAuditValidationService
{
    IReadOnlyList<AuditRecord> Validate(
        IReadOnlyList<AuditRecord> records);
}
```

* Schema validation
* Required fields
* Domain invariants only
* ❌ No dedup logic

---

### 5. **Application Command**

```csharp
public sealed class ConsumeAuditBatchCommand
{
    public IReadOnlyList<AuditRecord> Records { get; }
}
```

**Handler responsibilities:**

1. Validate records
2. Persist to Oracle
3. Return success/failure

No Kafka logic here.

---

### 6. **Kafka Consumer (Infrastructure)**

```csharp
EnableAutoCommit = false
```

Responsibilities:

* Poll Kafka
* Deserialize messages
* Execute application command
* Commit offsets **only on success**

---

## 🔁 Processing Flow

```
Kafka Poll
   ↓
Deserialize JSON → AuditRecord
   ↓
ConsumeAuditBatchCommandHandler
   ↓
AuditValidationService
   ↓
OracleAuditWriteRepository (transaction)
   ↓
SUCCESS
   ↓
Kafka Commit Offset
```

---

## 🧾 Offset Commit Strategy (Critical)

**Rules:**

* ❌ Never auto-commit
* ❌ Never commit before DB write
* ✅ Commit only after Oracle transaction succeeds

```csharp
try
{
    await _handler.Handle(command);
    _consumer.Commit(consumeResult);
}
catch
{
    // no commit → Kafka will retry
    throw;
}
```

---

## 🗄️ Oracle Target Table

```sql
CREATE TABLE AUDIT_EVENTS (
    SESSIONID   NUMBER NOT NULL,
    ENTRYID     NUMBER NOT NULL,
    EVENT_TS    TIMESTAMP,
    USERID      VARCHAR2(128),
    PAYLOAD     CLOB,
    CONSTRAINT UK_AUDIT UNIQUE (SESSIONID, ENTRYID)
);
```

> Unique constraint guarantees **storage-level idempotency**.

---

## 🧪 Testing Strategy

### Unit Tests

* Domain validation
* Command handler logic
* Failure paths (no offset commit)

### Integration Tests

* Kafka → Oracle happy path
* Oracle failure → offset not committed
* Restart → reprocessing works

---

## ⚙️ Configuration Example

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topic": "oracle.audit.events",
    "GroupId": "oracle-audit-consumer",
    "EnableAutoCommit": false
  },
  "Oracle": {
    "ConnectionString": "User Id=audit;Password=***;Data Source=ORCL"
  },
  "Processing": {
    "BatchSize": 500
  }
}
```

---

## 🧱 Clean Architecture Dependency Rule

```
Worker
  ↓
Application
  ↓
Domain
↑
Infrastructure
```

* Domain has **zero dependencies**
* Kafka & Oracle are replaceable
* Fully testable without Kafka or Oracle

---

## 🧠 Design Rationale (Straight Talk)

* Kafka already tracks offsets → **don't reinvent state**
* Oracle unique constraint → **no app-level dedup needed**
* Offset commit = processing contract
* Simpler system = fewer failure modes

This consumer is **boring by design** — and that's exactly what you want in production.

If you want next:

* sequence diagram
* failure matrix
* retry & DLQ strategy
* multi-partition scaling rules

---

# AuditSync Oracle Consumer - Detailed Architecture

## Overview
**AuditSync.OracleConsumer** is a .NET Core background service that consumes Oracle audit records from Apache Kafka (produced by AuditSync.OracleProducer), processes them using configurable regex/rule-based extraction, and persists relevant data to an Oracle database using Dapper.

**Last Updated:** 2025-12-18
**Recent Changes:**
- Updated for new flattened Kafka message structure (removed nested objects)
- Message now contains 22 direct properties instead of nested `actionType`, `affectedObject`, and `sqlDetails` objects
- Added new fields: `target`, `authPrivileges`, `authGrantee`, `newOwner`, `newName`, `privilegeUsed`, `producedAt`
- Field mappings changed: `action` (was `actionType.number`), `owner` (was `affectedObject.owner`), `name` (was `affectedObject.name`), `text` (was `sqlDetails.text`), `bindVariables` (was `sqlDetails.bindVariables`)

---

## Architecture Components

### 1. **Application Layer**
- **Background Service**: `IHostedService` implementation for continuous Kafka consumption
- **Health Checks**: Monitor consumer lag, database connectivity
- **Configuration Management**: Load all settings from `.env` file

### 2. **Domain Layer (DDD)**
```csharp
// Domain Models
public class AuditMessage
{
    public string Id { get; set; }
    public string Target { get; set; }
    public long SessionId { get; set; }
    public int EntryId { get; set; }
    public int Statement { get; set; }
    public string DbUser { get; set; }
    public string UserHost { get; set; }
    public string Terminal { get; set; }
    public int Action { get; set; }
    public int ReturnCode { get; set; }
    public string Owner { get; set; }
    public string Name { get; set; }
    public string AuthPrivileges { get; set; }
    public string AuthGrantee { get; set; }
    public string NewOwner { get; set; }
    public string NewName { get; set; }
    public string OsUser { get; set; }
    public string PrivilegeUsed { get; set; }
    public DateTime Timestamp { get; set; }
    public string BindVariables { get; set; }
    public string Text { get; set; }
    public DateTime ProducedAt { get; set; }
}

public class ExtractedData
{
    public string AuditRecordId { get; set; }
    public string Schema { get; set; }
    public string TableName { get; set; }
    public string SqlText { get; set; }
    public Dictionary<string, string> ExtractedFields { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public interface IRuleEngine
{
    Task<ExtractedData> ApplyRulesAsync(AuditMessage message);
}
```

### 3. **Infrastructure Layer**
- **Kafka Consumer**: Confluent.Kafka consumer with offset management
- **Oracle Repository**: Dapper-based data access
- **Rule Engine**: Regex/pattern-based extraction system

### 4. **Processing Pipeline**
```
Kafka Topic (oracle.audit.events)
    ↓ (Consume)
AuditSync Consumer Service
    ↓ (Deserialize JSON - flattened structure)
Domain Model (AuditMessage)
    ↓ (Apply Rules)
Rule Engine (Regex/Pattern Matching)
    ↓ (Extract Data)
Extracted Data Model
    ↓ (Persist using Dapper)
Oracle Database
    ↓ (Commit Offset)
Kafka Offset Store
```

---

## Detailed Project Structure

```
AuditSync.OracleConsumer/
├── src/
│   ├── AuditSync.OracleConsumer.Domain/
│   │   ├── Entities/
│   │   │   ├── AuditMessage.cs
│   │   │   └── ExtractedData.cs
│   │   ├── Interfaces/
│   │   │   ├── IRuleEngine.cs
│   │   │   ├── IExtractedDataRepository.cs
│   │   │   └── IOffsetManager.cs
│   │   └── Rules/
│   │       ├── ExtractionRule.cs
│   │       └── RuleResult.cs
│   │
│   ├── AuditSync.OracleConsumer.Application/
│   │   ├── Services/
│   │   │   ├── RuleEngine.cs
│   │   │   └── MessageProcessor.cs
│   │   └── Interfaces/
│   │       └── IMessageProcessor.cs
│   │
│   ├── AuditSync.OracleConsumer.Infrastructure/
│   │   ├── Kafka/
│   │   │   ├── KafkaConsumerService.cs
│   │   │   └── OffsetManager.cs
│   │   ├── Oracle/
│   │   │   ├── ExtractedDataRepository.cs
│   │   │   └── OracleConnectionFactory.cs
│   │   ├── Configuration/
│   │   │   ├── KafkaConsumerOptions.cs
│   │   │   ├── OracleOptions.cs
│   │   │   └── RuleOptions.cs
│   │   └── Rules/
│   │       ├── RegexRuleEngine.cs
│   │       └── RuleParser.cs
│   │
│   └── AuditSync.OracleConsumer.App/
│       ├── Services/
│       │   ├── AuditConsumerBackgroundService.cs
│       │   └── HealthChecks/
│       │       └── KafkaConsumerHealthCheck.cs
│       ├── Program.cs
│       ├── .env
│       └── .env.example
│
└── tests/
    ├── AuditSync.OracleConsumer.UnitTests/
    └── AuditSync.OracleConsumer.IntegrationTests/
```

---

## Testing Strategy

### Unit Tests
- Rule engine with various regex patterns
- Message deserialization
- Repository methods

### Integration Tests
- End-to-end Kafka consumption
- Database persistence
- Offset management

### Load Tests
- Simulate high message throughput
- Verify no message loss
- Monitor consumer lag

---

## Future Enhancements

1. **Advanced Rule Types**
   - JSON path expressions
   - XPath for XML data
   - JavaScript expressions

2. **Dead Letter Queue**
   - Send failed messages to DLQ topic
   - Manual reprocessing capability

3. **Metrics & Observability**
   - Prometheus metrics
   - OpenTelemetry integration
   - Grafana dashboards

4. **Performance**
   - Batch database inserts
   - Parallel message processing
   - Consumer instance scaling

---

## Code Examples & Reference Implementations

### Domain Model - ExtractionRule
```csharp
// ExtractionRule.cs
public class ExtractionRule
{
    public string Name { get; set; }
    public string Pattern { get; set; } // Regex pattern
    public string FieldName { get; set; } // Output field name
    public bool IsRequired { get; set; }
    public string SourceField { get; set; } // text, bindVariables, owner, name, etc.

    public static ExtractionRule FromEnvironmentVariable(string ruleDefinition)
    {
        // Parse: "CONTRACT_TYPE:text:MERGE INTO (\\w+):required"
        // ...
    }
}
```

### Infrastructure - KafkaConsumerService
```csharp
public class KafkaConsumerService : IDisposable
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaConsumerService> _logger;

    public async Task<ConsumeResult<string, string>> ConsumeAsync(CancellationToken ct)
    {
        return _consumer.Consume(ct);
    }

    public void Commit(ConsumeResult<string, string> result)
    {
        _consumer.Commit(result);
    }
}
```

### Infrastructure - OffsetManager
```csharp
public class OffsetManager : IOffsetManager
{
    // Track last committed offset
    // Handle offset storage in database (optional)
    // Provide offset reset capability
}
```

### Application - RegexRuleEngine
```csharp
public class RegexRuleEngine : IRuleEngine
{
    private readonly IRuleRepository _ruleRepository;
    private readonly ILogger<RegexRuleEngine> _logger;
    private readonly Dictionary<string, List<ExtractionRule>> _ruleCache;
    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    public RegexRuleEngine(IRuleRepository ruleRepository, ILogger<RegexRuleEngine> logger)
    {
        _ruleRepository = ruleRepository;
        _logger = logger;
        _ruleCache = new Dictionary<string, List<ExtractionRule>>();
    }

    public async Task<ExtractedData> ApplyRulesAsync(AuditMessage message)
    {
        var extractedData = new ExtractedData
        {
            AuditRecordId = message.Id,
            Schema = message.Owner,
            TableName = message.Name,
            SqlText = message.SqlText,
            ExtractedFields = new Dictionary<string, string>(),
            ProcessedAt = DateTime.UtcNow
        };

        // Lazy load rules: check cache first, load from DB if not found
        var rules = await GetRulesForTargetAsync(message.Target);

        if (rules == null || rules.Count == 0)
        {
            _logger.LogWarning("No rules found for target: {Target}", message.Target);
            return extractedData;
        }

        // Apply each rule
        foreach (var rule in rules)
        {
            var sourceValue = GetSourceValue(message, rule.SourceField);
            var match = Regex.Match(sourceValue ?? string.Empty, rule.Pattern);

            if (match.Success && match.Groups.Count > 1)
            {
                extractedData.ExtractedFields[rule.RuleName] = match.Groups[1].Value;
            }
            else if (rule.IsRequired)
            {
                throw new RuleValidationException($"Required rule '{rule.RuleName}' failed for target '{message.Target}'");
            }
        }

        return extractedData;
    }

    private async Task<List<ExtractionRule>> GetRulesForTargetAsync(string target)
    {
        // Check cache first (read without lock for performance)
        if (_ruleCache.TryGetValue(target, out var cachedRules))
        {
            return cachedRules;
        }

        // Rule not in cache - load from database
        await _cacheLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock (another thread might have loaded it)
            if (_ruleCache.TryGetValue(target, out var doubleCheckedRules))
            {
                return doubleCheckedRules;
            }

            // Load from database
            var rules = await _ruleRepository.GetRulesByTargetAsync(target);

            // Add to cache for future use
            _ruleCache[target] = rules;

            _logger.LogInformation("Loaded and cached {Count} rules for target: {Target}",
                rules.Count, target);

            return rules;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private string GetSourceValue(AuditMessage message, string sourceField)
    {
        // Use reflection or expression trees to access properties
        // e.g., "sqlText" -> message.SqlText, "bindVariables" -> message.BindVariables
        return sourceField.ToLower() switch
        {
            "sqltext" => message.SqlText,
            "bindvariables" => message.BindVariables,
            "owner" => message.Owner,
            "name" => message.Name,
            "dbuser" => message.DbUser,
            "userhost" => message.UserHost,
            _ => null
        };
    }
}
```

### Infrastructure - RuleRepository
```csharp
public class RuleRepository : IRuleRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RuleRepository> _logger;

    public async Task<List<ExtractionRule>> GetRulesByTargetAsync(string targetName)
    {
        using var connection = new OracleConnection(_connectionString);

        var sql = @"
            SELECT r.ID, r.TARGET_ID, t.NAME AS TARGET_NAME, r.RULE_NAME,
                   r.SOURCE_FIELD, r.REGEX_PATTERN,
                   r.IS_REQUIRED, r.IS_ACTIVE, r.RULE_ORDER
            FROM target_rules r
            INNER JOIN targets t ON r.TARGET_ID = t.ID
            WHERE t.NAME = :TargetName
              AND r.IS_ACTIVE = 1
            ORDER BY r.RULE_ORDER";

        var rules = await connection.QueryAsync<ExtractionRule>(sql, new { TargetName = targetName });

        _logger.LogInformation("Loaded {Count} extraction rules for target: {Target}",
            rules.Count(), targetName);

        return rules.ToList();
    }
}
```

---

## Configuration Examples

### .env File Configuration
```env
# =============================================================================
# AuditSync Oracle Consumer Configuration
# =============================================================================

# -----------------------------------------------------------------------------
# Kafka Configuration
# -----------------------------------------------------------------------------
KAFKA_BOOTSTRAP_SERVERS=localhost:9092
KAFKA_TOPIC=oracle.audit.events
KAFKA_GROUP_ID=auditsync-consumer-group
KAFKA_AUTO_OFFSET_RESET=earliest
KAFKA_ENABLE_AUTO_COMMIT=false
KAFKA_SESSION_TIMEOUT_MS=30000
KAFKA_MAX_POLL_INTERVAL_MS=300000

# -----------------------------------------------------------------------------
# Oracle Configuration
# -----------------------------------------------------------------------------
ORACLE_HOST=localhost
ORACLE_PORT=1521
ORACLE_SERVICE_NAME=ORCL
ORACLE_USERNAME=audit_consumer
ORACLE_PASSWORD=your_password
ORACLE_MIN_POOL_SIZE=1
ORACLE_MAX_POOL_SIZE=10
ORACLE_CONNECTION_TIMEOUT=30

# -----------------------------------------------------------------------------
# Processing Configuration
# -----------------------------------------------------------------------------
PROCESSING_BATCH_SIZE=100
PROCESSING_MAX_RETRY_COUNT=3
PROCESSING_RETRY_BACKOFF_MS=5000
```

### Program.cs Configuration Loading
```csharp
// Load .env file
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

// Load Kafka configuration from environment
var kafkaConfig = new ConsumerConfig
{
    BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS"),
    GroupId = Environment.GetEnvironmentVariable("KAFKA_GROUP_ID"),
    AutoOffsetReset = Enum.Parse<AutoOffsetReset>(
        Environment.GetEnvironmentVariable("KAFKA_AUTO_OFFSET_RESET") ?? "Earliest"),
    EnableAutoCommit = bool.Parse(
        Environment.GetEnvironmentVariable("KAFKA_ENABLE_AUTO_COMMIT") ?? "false"),
    SessionTimeoutMs = int.Parse(
        Environment.GetEnvironmentVariable("KAFKA_SESSION_TIMEOUT_MS") ?? "30000"),
    MaxPollIntervalMs = int.Parse(
        Environment.GetEnvironmentVariable("KAFKA_MAX_POLL_INTERVAL_MS") ?? "300000")
};

// Load Oracle configuration
var oracleConfig = new OracleOptions
{
    Host = Environment.GetEnvironmentVariable("ORACLE_HOST") ?? "localhost",
    Port = int.Parse(Environment.GetEnvironmentVariable("ORACLE_PORT") ?? "1521"),
    ServiceName = Environment.GetEnvironmentVariable("ORACLE_SERVICE_NAME") ?? "ORCL",
    Username = Environment.GetEnvironmentVariable("ORACLE_USERNAME"),
    Password = Environment.GetEnvironmentVariable("ORACLE_PASSWORD")
};

// Register rule repository and rule engine
builder.Services.AddSingleton<IRuleRepository, RuleRepository>();
builder.Services.AddSingleton<IRuleEngine, RegexRuleEngine>();

// Note: Rules are loaded lazily on first use (per target)
// No need to load all rules at startup
```

### Health Check Registration
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<KafkaConsumerHealthCheck>("kafka_consumer");

app.MapHealthChecks("/health");
```

### Oracle Database Schema
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
    PROCESS_COUNTER NUMBER DEFAULT 1, -- Increments on duplicate
    PROCESSED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSUMED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT UK_AUDIT_OFFSET UNIQUE (KAFKA_PARTITION, KAFKA_OFFSET)
);

CREATE INDEX IDX_AUDIT_SESSION ON audit_logs(SESSION_ID, ENTRY_ID);
CREATE INDEX IDX_AUDIT_PROCESSED_AT ON audit_logs(PROCESSED_AT);
CREATE INDEX IDX_AUDIT_DB_USER ON audit_logs(DB_USER);
CREATE INDEX IDX_AUDIT_TARGET ON audit_logs(TARGET);
CREATE INDEX IDX_AUDIT_PROCESS_COUNTER ON audit_logs(PROCESS_COUNTER);

-- Table 2: Store extracted (name, value) pairs
-- Note: On duplicate message, DELETE all old extracted values and re-INSERT new ones
-- This ensures extracted values always reflect the latest message data
CREATE TABLE audit_log_extracted_values (
    ID VARCHAR2(100) PRIMARY KEY,
    AUDIT_MESSAGE_ID VARCHAR2(100) NOT NULL,
    FIELD_NAME VARCHAR2(100) NOT NULL,
    FIELD_VALUE VARCHAR2(4000),
    EXTRACTED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT FK_AUDIT_MESSAGE FOREIGN KEY (AUDIT_MESSAGE_ID)
        REFERENCES audit_logs(ID) ON DELETE CASCADE
);

CREATE INDEX IDX_EXTRACTED_MESSAGE_ID ON audit_log_extracted_values(AUDIT_MESSAGE_ID);
CREATE INDEX IDX_EXTRACTED_FIELD_NAME ON audit_log_extracted_values(FIELD_NAME);
CREATE INDEX IDX_EXTRACTED_NAME_VALUE ON audit_log_extracted_values(FIELD_NAME, FIELD_VALUE);

-- Table 3: Store target information
CREATE TABLE targets (
    ID VARCHAR2(100) PRIMARY KEY,
    NAME VARCHAR2(256) NOT NULL UNIQUE,
    DESCRIPTION VARCHAR2(1000),
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
);

CREATE INDEX IDX_TARGETS_NAME ON targets(NAME);

-- Table 4: Store extraction rules for each target
CREATE TABLE target_rules (
    ID VARCHAR2(100) PRIMARY KEY,
    TARGET_ID VARCHAR2(100) NOT NULL,
    RULE_NAME VARCHAR2(100) NOT NULL,
    SOURCE_FIELD VARCHAR2(100) NOT NULL,
    REGEX_PATTERN VARCHAR2(1000) NOT NULL,
    IS_REQUIRED NUMBER(1) DEFAULT 0,
    IS_ACTIVE NUMBER(1) DEFAULT 1,
    RULE_ORDER NUMBER NOT NULL,
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT FK_TARGET FOREIGN KEY (TARGET_ID)
        REFERENCES targets(ID) ON DELETE CASCADE,
    CONSTRAINT UK_TARGET_RULE UNIQUE (TARGET_ID, RULE_NAME)
);

CREATE INDEX IDX_RULES_TARGET_ID ON target_rules(TARGET_ID);
CREATE INDEX IDX_RULES_ACTIVE ON target_rules(IS_ACTIVE);
CREATE INDEX IDX_RULES_ORDER ON target_rules(TARGET_ID, RULE_ORDER);
```
