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
┌──────────────────────────┐
│   AuditSync Consumer     │
│  ┌───────────────────┐   │
│  │ Message Consumer  │   │
│  └─────────┬─────────┘   │
│            │             │
│            ▼             │
│  ┌───────────────────┐   │
│  │ Oracle Persist    │   │
│  │ (audit_logs)      │   │
│  └─────────┬─────────┘   │
│            │             │
│            ▼             │
│  ┌───────────────────┐   │
│  │ Extraction Engine │   │
│  │ (Regex Matching)  │   │
│  └─────────┬─────────┘   │
│            │             │
│       Any Match?         │
│         │     │          │
│        Yes    No         │
│         │     └─────┐    │
│         ▼           │    │
│  ┌───────────────┐  │    │
│  │ Case Service  │  │    │
│  │ (Create Case) │  │    │
│  └─────────┬─────┘  │    │
│            │        │    │
│            ▼        │    │
│  ┌──────────────────┐   │
│  │ Oracle Persist   │   │
│  │ (cases +         │◄──┘
│  │  extractions)    │   │
│  └────────┬─────────┘   │
└───────────┼─────────────┘
            │
            ▼
┌────────────────────────────┐
│   Oracle Database          │
│  ┌──────────────────────┐  │
│  │   audit_logs         │  │
│  │ (All audit messages) │  │
│  └──────────────────────┘  │
│  ┌──────────────────────┐  │
│  │   targets            │  │
│  │ (Target databases)   │  │
│  └──────────────────────┘  │
│  ┌──────────────────────┐  │
│  │   target_rules       │  │
│  │ (Extraction rules)   │  │
│  └──────────────────────┘  │
│  ┌──────────────────────┐  │
│  │   cases              │  │
│  │ (When extraction OK) │  │
│  └──────────────────────┘  │
│  ┌──────────────────────┐  │
│  │  case_extractions    │  │
│  │ (Extracted values +  │  │
│  │  rule information)   │  │
│  └──────────────────────┘  │
└────────────────────────────┘
```

## Case-Based Processing

The consumer implements an intelligent case-based system:

- **All audit messages** are stored in the `audit_logs` table
- **Extraction rules** (regex patterns) are applied to extract sensitive data (e.g., MSISDNs, IMSIs)
- **Cases are created** only when ANY extraction rule successfully matches
- **No case is created** if no rules match (reduces noise)
- **One case per audit log** (enforced by unique constraint)
- **Complete audit trail**: Each extraction stores the rule name, regex pattern, and source field that matched

This approach ensures that only relevant audit logs with extracted sensitive data generate cases for investigation.

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
3. **Persist Audit Log**: Save complete message to `audit_logs` table (MERGE/upsert)
4. **Load Rules**: Fetch extraction rules for the target from `target_rules` table
5. **Extract**: Apply regex rules to extract values from audit message fields
6. **Create Case** (conditional):
   - **If ANY extraction succeeds**: Create case in `cases` table
   - **Store extractions**: Save to `case_extractions` with denormalized rule info
   - **If NO extractions**: Skip to next message (no case created)
7. **Commit**: Commit Kafka offset only after successful database write

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

### ✅ Case-Based Processing
- **Intelligent Case Creation**: Cases created only when extraction rules successfully extract values
- **No Noise**: Audit logs with no extracted values don't create cases (reduces noise)
- **Complete Audit Trail**: Each extraction records which rule matched and the exact regex pattern used
- **Denormalized Rule Info**: Case extractions store rule name, pattern, and source field for historical reference
- **Idempotent**: Reprocessing same audit log won't create duplicate cases
- **One Case Per Audit**: Enforced by unique constraint on AUDIT_LOG_ID

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
  - **`audit_logs`**: Store all audit messages from Kafka (with deduplication via MERGE)
  - **`targets`**: Store target database information (ID, NAME, DESCRIPTION)
  - **`target_rules`**: Extraction rules per target (linked via foreign key)
    - Different targets can have different extraction rules
    - Rules loaded lazily on first use and cached in memory
    - Minimal database queries - only when rule not in cache
    - Rules can be added, updated, or deactivated without code changes
    - Supports rule ordering and active/inactive flags
  - **`cases`**: Store cases created when extraction succeeds
    - One case per audit_log (unique constraint)
    - Case status: OPEN, RESOLVED, ASSIGNED
    - VALID field for manual validation (YES, NO, NULL)
  - **`case_extractions`**: Store extracted values with rule information
    - Denormalized rule data (RULE_NAME, REGEX_PATTERN, SOURCE_FIELD)
    - Complete audit trail of which rule extracted which value
    - Linked to cases, audit_logs, and target_rules

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
- **[architecture.md](docs/architecture.md)** - Detailed architecture, code examples, and configuration
- **[plan.md](docs/plan.md)** - Implementation tasks and phases
- **[data.md](docs/data.md)** - Database schema, sample data, and queries
- **[case_plan.md](docs/case_plan.md)** - Case-based architecture design and rationale
- **[DATABASE_SETUP.md](docs/DATABASE_SETUP.md)** - Database setup guide
- **[IMPLEMENTATION_COMPLETE.md](docs/IMPLEMENTATION_COMPLETE.md)** - Implementation status

---

**Bottom Line:**
The AuditSync Consumer guarantees **reliable, duplicate-free persistence** of Oracle audit events from Kafka into Oracle database with **intelligent case-based processing** — no gaps, no replays, no silent failures, no noise.
