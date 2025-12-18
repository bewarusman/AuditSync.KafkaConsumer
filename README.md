# AuditSync Consumer Application – High Level Design Document

## Overview

**AuditSync Consumer** is a .NET Core background service that consumes Oracle audit events from Apache Kafka and persists them reliably into an Oracle database. It guarantees ordered, idempotent, and fault-tolerant ingestion of audit data.

## Purpose

The consumer is designed to:
- Consume audit messages from Kafka topic `oracle.audit.events`
- Extract relevant data using configurable regex-based rules
- Persist complete audit records and extracted values to Oracle database
- Ensure exactly-once processing semantics at the database level
- Provide fault tolerance and crash recovery

## High-Level Architecture

```
┌─────────────────┐
│  Kafka Topic    │
│ oracle.audit... │
└────────┬────────┘
         │
         ▼
┌─────────────────────────┐
│   AuditSync Consumer    │
│  ┌──────────────────┐   │
│  │ Message Consumer │   │
│  └────────┬─────────┘   │
│           │             │
│           ▼             │
│  ┌──────────────────┐   │
│  │   Rule Engine    │   │
│  │  (Regex Extract) │   │
│  └────────┬─────────┘   │
│           │             │
│           ▼             │
│  ┌──────────────────┐   │
│  │ Oracle Persist   │   │
│  │   (Dapper)       │   │
│  └────────┬─────────┘   │
└───────────┼─────────────┘
            │
            ▼
┌─────────────────────────┐
│   Oracle Database       │
│  ┌──────────────────┐   │
│  │   audit_logs     │   │
│  └──────────────────┘   │
│  ┌──────────────────┐   │
│  │ audit_log_       │   │
│  │ extracted_values │   │
│  └──────────────────┘   │
└─────────────────────────┘
```

## Core Principles

### 1. Reliability
- **Manual Offset Management**: Offsets committed only after successful database persistence
- **Deduplication Logic**: Uses MERGE (upsert) to update existing records instead of creating duplicates
- **Process Counter**: Tracks how many times each record has been processed
- **Transaction Safety**: All database operations wrapped in transactions

### 2. Flexibility
- **Configurable Rules**: Regex-based extraction rules stored in database
- **Target-Specific Rules**: Different extraction rules for different targets (e.g., Production vs Development)
- **Lazy Loading**: Rules loaded on first use per target and cached in memory for performance
- **Runtime Updates**: Rules can be modified without restarting the application (new rules loaded on next cache miss)
- **Environment-Driven**: Configuration via `.env` file
- **Extensible Design**: Easy to add new extraction rules or modify processing logic

### 3. Performance
- **Batch Processing**: Efficient bulk operations where applicable
- **Connection Pooling**: Optimized database connections
- **Async Operations**: Non-blocking I/O throughout

## Data Flow

1. **Consume**: Poll Kafka topic for new audit messages
2. **Deserialize**: Convert JSON to domain objects (22 properties)
3. **Extract**: Apply regex rules to extract relevant fields
4. **Persist**: Save to two Oracle tables:
   - `audit_logs`: Complete audit record
   - `audit_log_extracted_values`: Extracted field/value pairs
5. **Commit**: Commit Kafka offset only after successful database write

## Key Features

### ✅ Guaranteed Message Processing
- No message loss through manual offset management
- Deduplication prevents duplicate records (uses MERGE/upsert logic)
- Process counter tracks reprocessing of same records
- Crash recovery from last committed offset

### ✅ Flexible Rule Engine
- Regex-based extraction from any message field
- Support for required and optional rules
- Database-driven rule configuration with lazy loading
- Different extraction rules per target
- Rules loaded on first use and cached in memory
- Minimal database queries for optimal performance
- Rules can be updated without redeploying the application

### ✅ Robust Error Handling
- Retry logic with exponential backoff
- Comprehensive logging for troubleshooting
- Dead letter queue support (optional)

### ✅ Production Ready
- Health check endpoints
- Consumer lag monitoring
- Graceful shutdown handling

## Configuration

Configuration is managed through:
- **`.env` File**: Kafka settings, Oracle connection details, processing options
- **Database Tables**:
  - **`targets`**: Store target database information (ID, NAME, DESCRIPTION)
  - **`target_rules`**: Extraction rules per target (linked via foreign key)
    - Different targets can have different extraction rules
    - Rules loaded lazily on first use and cached in memory
    - Minimal database queries - only when rule not in cache
    - Rules can be added, updated, or deactivated without code changes
    - Supports rule ordering and required/optional flags

## Scalability

- **Horizontal Scaling**: Deploy multiple consumer instances in the same consumer group
- **Partition-Based**: Kafka partitions enable parallel processing
- **Ordering Guarantee**: Preserved per partition
- **Throughput Control**: Tunable via batch size and poll interval

## Security Considerations

- Kafka credentials with read-only access
- Oracle user with insert-only permissions
- Encrypted secrets management
- Complete audit trail of all operations

---

**For detailed implementation, see:**
- **[architecture.md](architecture.md)** - Detailed architecture, code examples, and configuration
- **[plan.md](plan.md)** - Implementation tasks and phases
- **[data.md](data.md)** - Sample data transformations

---

**Bottom Line:**
The AuditSync Consumer guarantees **reliable, duplicate-free persistence** of Oracle audit events from Kafka into Oracle database — no gaps, no replays, no silent failures.
