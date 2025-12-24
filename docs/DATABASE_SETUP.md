# Database Setup Guide

This guide explains how to set up the Oracle database for AuditSync Oracle Consumer.

## Overview

The AuditSync Oracle Consumer uses 5 main tables:
1. **audit_logs** - Stores complete audit messages from Kafka
2. **targets** - Stores target database information
3. **target_rules** - Stores extraction rules (regex patterns)
4. **cases** - Stores cases created when extractions succeed
5. **case_extractions** - Stores extracted values with denormalized rule info

## Quick Start

### Option 1: Complete Database Recreation (Recommended for Fresh Setup)

Use this script to drop all existing tables and create the complete schema:

```bash
sqlplus username/password@database @docs/000_recreate_all_tables.sql
```

**⚠️ WARNING**: This script will DELETE ALL DATA in existing tables!

### Option 2: Migration from Old Schema (For Existing Deployments)

If you have existing data and want to migrate to the new case-based architecture:

```bash
sqlplus username/password@database @database/scripts/003_create_case_tables.sql
```

This only creates the new `cases` and `case_extractions` tables without dropping existing data.

## Script Files

| Script | Purpose | Use When |
|--------|---------|----------|
| `docs/000_recreate_all_tables.sql` | Drop all tables and recreate complete schema | Fresh setup, development reset, full migration |
| `database/scripts/003_create_case_tables.sql` | Create only new case tables | Adding case feature to existing system |

## Step-by-Step Setup

### 1. Run Database Script

Choose the appropriate script based on your scenario:

**Fresh Installation:**
```sql
@docs/000_recreate_all_tables.sql
```

**Existing Installation:**
```sql
@database/scripts/003_create_case_tables.sql
```

### 2. Verify Tables Created

```sql
-- Check all tables exist
SELECT table_name
FROM user_tables
WHERE table_name IN ('AUDIT_LOGS', 'TARGETS', 'TARGET_RULES', 'CASES', 'CASE_EXTRACTIONS')
ORDER BY table_name;

-- Should return 5 rows
```

### 3. Verify Indexes Created

```sql
-- Check indexes
SELECT table_name, index_name, uniqueness
FROM user_indexes
WHERE table_name IN ('AUDIT_LOGS', 'TARGETS', 'TARGET_RULES', 'CASES', 'CASE_EXTRACTIONS')
ORDER BY table_name, index_name;

-- Should return 18 indexes total
```

### 4. Insert Sample Targets

```sql
-- Insert a sample target
INSERT INTO targets VALUES (
  SYS_GUID(),
  'Production Oracle Database',
  'Production Oracle database for main application',
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

COMMIT;
```

### 5. Insert Extraction Rules

See `docs/data.md` for complete examples. Here's a simple example:

```sql
-- Get the target ID
SELECT ID FROM targets WHERE NAME = 'Production Oracle Database';

-- Insert extraction rule for MSISDN
INSERT INTO target_rules VALUES (
  SYS_GUID(),
  '<target-id-from-above>',
  'msisdn',
  'text',
  'msisdn\s*=\s*''(\d+)''',
  1,  -- IS_ACTIVE
  1,  -- RULE_ORDER
  SYSTIMESTAMP,
  SYSTIMESTAMP
);

COMMIT;
```

## Schema Details

### Table: audit_logs
- **Purpose**: Store complete Kafka messages
- **Key Features**:
  - UPSERT logic (PROCESS_COUNTER increments on duplicate)
  - Unique constraint on (KAFKA_PARTITION, KAFKA_OFFSET)
- **Indexes**: 5 indexes for performance

### Table: targets
- **Purpose**: Store target database information
- **Key Features**:
  - Unique constraint on NAME
- **Indexes**: 1 index on NAME

### Table: target_rules
- **Purpose**: Store extraction rules (regex patterns)
- **Key Features**:
  - Foreign key to targets
  - Unique constraint on (TARGET_ID, RULE_NAME)
  - RULE_ORDER determines execution sequence
- **Indexes**: 3 indexes for lookups

### Table: cases
- **Purpose**: Store cases created when extraction succeeds
- **Key Features**:
  - One case per audit_log (UNIQUE constraint)
  - CASE_STATUS: OPEN, RESOLVED, ASSIGNED
  - VALID: NULL, YES, NO (defaults to NULL)
- **Indexes**: 3 indexes for queries

### Table: case_extractions
- **Purpose**: Store extracted values with rule information
- **Key Features**:
  - Denormalized rule info (RULE_NAME, REGEX_PATTERN, SOURCE_FIELD)
  - Foreign keys to cases, audit_logs, target_rules
- **Indexes**: 6 indexes for complex queries

## Common Queries

### Check Table Row Counts
```sql
SELECT 'audit_logs' as table_name, COUNT(*) FROM audit_logs
UNION ALL
SELECT 'targets', COUNT(*) FROM targets
UNION ALL
SELECT 'target_rules', COUNT(*) FROM target_rules
UNION ALL
SELECT 'cases', COUNT(*) FROM cases
UNION ALL
SELECT 'case_extractions', COUNT(*) FROM case_extractions;
```

### View All Targets and Their Rules
```sql
SELECT
    t.NAME as target_name,
    tr.RULE_NAME,
    tr.SOURCE_FIELD,
    tr.REGEX_PATTERN,
    tr.IS_ACTIVE,
    tr.RULE_ORDER
FROM targets t
LEFT JOIN target_rules tr ON t.ID = tr.TARGET_ID
ORDER BY t.NAME, tr.RULE_ORDER;
```

### View Recent Cases
```sql
SELECT
    c.ID,
    c.AUDIT_LOG_ID,
    c.CASE_STATUS,
    c.VALID,
    c.CREATED_AT,
    COUNT(ce.ID) as extraction_count
FROM cases c
LEFT JOIN case_extractions ce ON c.ID = ce.CASE_ID
GROUP BY c.ID, c.AUDIT_LOG_ID, c.CASE_STATUS, c.VALID, c.CREATED_AT
ORDER BY c.CREATED_AT DESC
FETCH FIRST 10 ROWS ONLY;
```

## Troubleshooting

### Foreign Key Constraint Errors

If you get FK constraint errors when dropping tables:
```sql
-- Drop with CASCADE CONSTRAINTS
DROP TABLE case_extractions CASCADE CONSTRAINTS;
DROP TABLE cases CASCADE CONSTRAINTS;
DROP TABLE target_rules CASCADE CONSTRAINTS;
DROP TABLE targets CASCADE CONSTRAINTS;
DROP TABLE audit_logs CASCADE CONSTRAINTS;
```

### Index Already Exists

If indexes already exist:
```sql
-- Drop specific index
DROP INDEX IDX_AUDIT_SESSION;

-- Or use the recreation script which handles this automatically
```

### Check for Orphaned Tables

```sql
-- List all tables in schema
SELECT table_name
FROM user_tables
ORDER BY table_name;

-- Drop old tables if they exist
DROP TABLE audit_log_extracted_values CASCADE CONSTRAINTS;
```

## Performance Tuning

### Analyze Tables After Loading Data
```sql
-- Gather statistics for optimizer
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'AUDIT_LOGS');
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'TARGETS');
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'TARGET_RULES');
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'CASES');
EXEC DBMS_STATS.GATHER_TABLE_STATS(USER, 'CASE_EXTRACTIONS');
```

### Monitor Index Usage
```sql
-- Check index usage statistics
SELECT
    i.table_name,
    i.index_name,
    i.num_rows,
    i.last_analyzed
FROM user_indexes i
WHERE i.table_name IN ('AUDIT_LOGS', 'TARGETS', 'TARGET_RULES', 'CASES', 'CASE_EXTRACTIONS')
ORDER BY i.table_name, i.index_name;
```

## Migration Notes

### From Old Architecture to New

If you're migrating from the old architecture with `audit_log_extracted_values`:

1. **Backup your data**
   ```sql
   CREATE TABLE audit_log_extracted_values_backup AS
   SELECT * FROM audit_log_extracted_values;
   ```

2. **Run the new schema**
   ```sql
   @docs/000_recreate_all_tables.sql
   ```

3. **Optionally backfill cases from historical data**
   - Write a custom script to create cases from old extracted values
   - Or start fresh with the new architecture

## Next Steps

1. ✅ Create database tables
2. Insert sample targets and rules (see `docs/data.md`)
3. Configure application connection string (`.env` file)
4. Start the AuditSync Oracle Consumer application
5. Monitor logs for case creation
6. Query the `cases` and `case_extractions` tables to verify

## References

- Complete schema with examples: `docs/data.md`
- Architecture documentation: `docs/case_plan.md`
- Sample data inserts: `database/scripts/002_insert_sample_data.sql`
