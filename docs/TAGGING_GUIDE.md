# Extraction Value Tagging Guide

## Overview

The AuditSync Oracle Consumer now supports **automatic tagging** of extracted values based on configurable rules. This allows you to classify and categorize extracted data dynamically, such as marking MSISDNs starting with specific prefixes as "VIP" customers.

## Architecture

### Components

1. **rule_tags Table**: Stores tag configurations for extraction rules
2. **TagEvaluationService**: Evaluates extracted values against tag rules
3. **RuleTagRepository**: Manages tag rule data access
4. **ExtractionService**: Enhanced to apply tags during extraction

### Flow

```
Extraction Rule → Extract Value → Apply Tag Rules → Tagged Extracted Value → Store in case_extractions
```

## Database Schema

### rule_tags Table

```sql
CREATE TABLE rule_tags (
    ID VARCHAR2(100) PRIMARY KEY,
    RULE_ID VARCHAR2(100) NOT NULL,              -- FK to target_rules
    TAG_NAME VARCHAR2(100) NOT NULL,             -- Tag to apply (e.g., "VIP")
    CONDITION_TYPE VARCHAR2(50) NOT NULL,        -- Type of condition
    CONDITION_VALUE VARCHAR2(1000) NOT NULL,     -- Value to match
    TAG_PRIORITY NUMBER DEFAULT 0,               -- Higher = more important
    IS_ACTIVE NUMBER(1) DEFAULT 1,               -- Enable/disable tag rule
    CREATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    UPDATED_AT TIMESTAMP DEFAULT SYSTIMESTAMP
);
```

### case_extractions Table (Updated)

The `case_extractions` table now includes a `TAGS` column:

```sql
TAGS VARCHAR2(1000) DEFAULT NULL  -- Comma-separated list of tags (e.g., "VIP,Priority")
```

## Supported Condition Types

| Condition Type | Description | Example |
|----------------|-------------|---------|
| **StartsWith** | Value starts with the condition value | `964750445` matches MSISDNs starting with 964750445 |
| **EndsWith** | Value ends with the condition value | `777` matches MSISDNs ending with 777 |
| **Equals** | Value exactly matches | `9647507703030` matches only that specific MSISDN |
| **Contains** | Value contains the substring | `750` matches any value containing 750 |
| **Regex** | Value matches regex pattern | `.*777$` matches values ending with 777 |
| **Range** | Value is within numeric/lexicographic range | `9647504450000-9647504459999` matches values in range |

## Setup Guide

### 1. Run Migration Script

Apply the tagging support migration:

```bash
sqlplus username/password@database @database/scripts/004_add_tagging_support.sql
```

### 2. Configure Tag Rules

#### Example 1: Tag VIP MSISDNs

Tag any MSISDN starting with `964750445` as "VIP":

```sql
INSERT INTO rule_tags VALUES (
    'tag-vip-1',
    'rule-id-for-msisdn',  -- Your MSISDN extraction rule ID
    'VIP',                  -- Tag name
    'StartsWith',           -- Condition type
    '964750445',            -- Value to match
    10,                     -- Priority
    1,                      -- Active
    SYSTIMESTAMP,
    SYSTIMESTAMP
);
```

#### Example 2: Tag Premium MSISDNs (Range)

Tag MSISDNs in a specific range as "Premium":

```sql
INSERT INTO rule_tags VALUES (
    'tag-premium-1',
    'rule-id-for-msisdn',
    'Premium',
    'Range',
    '9647504450000-9647504459999',  -- min-max format
    8,
    1,
    SYSTIMESTAMP,
    SYSTIMESTAMP
);
```

#### Example 3: Tag Special MSISDNs (Regex)

Tag MSISDNs ending with 777 as "Special":

```sql
INSERT INTO rule_tags VALUES (
    'tag-special-1',
    'rule-id-for-msisdn',
    'Special',
    'Regex',
    '.*777$',
    7,
    1,
    SYSTIMESTAMP,
    SYSTIMESTAMP
);
```

#### Example 4: Tag Specific IMSI

Tag a specific IMSI as "High-Priority":

```sql
INSERT INTO rule_tags VALUES (
    'tag-high-priority-1',
    'rule-id-for-imsi',
    'High-Priority',
    'Equals',
    '418123456789',
    5,
    1,
    SYSTIMESTAMP,
    SYSTIMESTAMP
);
```

### 3. Verify Tag Configuration

Check all active tag rules:

```sql
SELECT
    rt.TAG_NAME,
    rt.CONDITION_TYPE,
    rt.CONDITION_VALUE,
    rt.TAG_PRIORITY,
    tr.RULE_NAME as EXTRACTION_RULE,
    t.NAME as TARGET
FROM rule_tags rt
JOIN target_rules tr ON rt.RULE_ID = tr.ID
JOIN targets t ON tr.TARGET_ID = t.ID
WHERE rt.IS_ACTIVE = 1
ORDER BY rt.TAG_PRIORITY DESC;
```

## Querying Tagged Data

### Find All VIP Cases

```sql
SELECT
    ce.FIELD_VALUE,
    ce.TAGS,
    c.CASE_STATUS,
    a.TIMESTAMP
FROM case_extractions ce
JOIN cases c ON ce.CASE_ID = c.ID
JOIN audit_logs a ON ce.AUDIT_LOG_ID = a.ID
WHERE ce.TAGS LIKE '%VIP%'
ORDER BY a.TIMESTAMP DESC;
```

### Count Cases by Tag

```sql
SELECT
    CASE
        WHEN ce.TAGS LIKE '%VIP%' THEN 'VIP'
        WHEN ce.TAGS LIKE '%Premium%' THEN 'Premium'
        WHEN ce.TAGS LIKE '%Special%' THEN 'Special'
        ELSE 'Untagged'
    END as TAG_CATEGORY,
    COUNT(DISTINCT ce.CASE_ID) as CASE_COUNT
FROM case_extractions ce
GROUP BY CASE
    WHEN ce.TAGS LIKE '%VIP%' THEN 'VIP'
    WHEN ce.TAGS LIKE '%Premium%' THEN 'Premium'
    WHEN ce.TAGS LIKE '%Special%' THEN 'Special'
    ELSE 'Untagged'
END
ORDER BY CASE_COUNT DESC;
```

### Find Cases with Multiple Tags

```sql
SELECT
    c.ID as CASE_ID,
    a.DB_USER,
    ce.FIELD_VALUE,
    ce.TAGS
FROM case_extractions ce
JOIN cases c ON ce.CASE_ID = c.ID
JOIN audit_logs a ON ce.AUDIT_LOG_ID = a.ID
WHERE ce.TAGS LIKE '%,%'  -- Contains comma (multiple tags)
ORDER BY a.TIMESTAMP DESC;
```

## Tag Priority

Tags are evaluated in **descending priority order** (highest first). Multiple tags can be applied to a single extracted value.

Example:
- MSISDN `9647504457777` could match both:
  - "VIP" tag (StartsWith: 964750445, Priority: 10)
  - "Special" tag (Regex: .*777$, Priority: 7)
  - Result: Tags = "VIP,Special"

## Best Practices

1. **Use Descriptive Tag Names**: Use clear, business-meaningful names like "VIP", "Premium", "High-Risk"

2. **Set Appropriate Priorities**: Assign higher priorities to more specific/important tags

3. **Test Regex Patterns**: Test regex patterns before deployment to avoid performance issues

4. **Monitor Tag Performance**: Check tag evaluation performance in logs

5. **Use Range for Numeric Ranges**: Range condition is more efficient than regex for numeric ranges

6. **Disable Instead of Delete**: Set `IS_ACTIVE = 0` instead of deleting tag rules to maintain history

## Performance Considerations

- Tag rules are **batch-loaded** per target to minimize database queries
- Regex evaluation has a **100ms timeout** to prevent performance issues
- Use **StartsWith/EndsWith** instead of Regex when possible for better performance
- Index on `TAGS` column enables efficient filtering

## Example: Complete VIP Tagging Setup

```sql
-- Step 1: Identify your MSISDN extraction rule
SELECT ID, RULE_NAME, TARGET_ID
FROM target_rules
WHERE RULE_NAME = 'msisdn';

-- Step 2: Create VIP tag rule
INSERT INTO rule_tags VALUES (
    SYS_GUID(),             -- Generate unique ID
    'your-rule-id-here',    -- Replace with actual rule ID from step 1
    'VIP',
    'StartsWith',
    '964750445',
    10,
    1,
    SYSTIMESTAMP,
    SYSTIMESTAMP
);

-- Step 3: Verify tag rule was created
SELECT * FROM rule_tags WHERE TAG_NAME = 'VIP';

-- Step 4: Wait for new audit messages to be processed

-- Step 5: Query tagged extractions
SELECT
    ce.FIELD_VALUE as MSISDN,
    ce.TAGS,
    a.DB_USER,
    a.TIMESTAMP
FROM case_extractions ce
JOIN audit_logs a ON ce.AUDIT_LOG_ID = a.ID
WHERE ce.RULE_NAME = 'msisdn'
  AND ce.TAGS LIKE '%VIP%'
ORDER BY a.TIMESTAMP DESC;
```

## Troubleshooting

### Tags Not Appearing

1. Verify tag rule is active: `SELECT * FROM rule_tags WHERE IS_ACTIVE = 1`
2. Check rule_id matches extraction rule: `SELECT * FROM target_rules WHERE ID = 'your-rule-id'`
3. Ensure condition value matches extracted values
4. Check application logs for tag evaluation errors

### Performance Issues

1. Avoid complex regex patterns
2. Use more specific conditions (StartsWith instead of Regex when possible)
3. Monitor tag evaluation time in logs
4. Consider reducing number of active tag rules per extraction rule

## Migration from Existing Data

To apply tags to existing data, you would need to:

1. Create tag rules
2. Run an UPDATE script to re-evaluate existing extractions (not provided - requires custom script)

Or simply let new messages get tagged automatically going forward.

## Future Enhancements

Potential future improvements:
- Tag hierarchy (parent/child tags)
- Tag-based alerting
- Tag statistics dashboard
- Dynamic tag rule updates without restart
- Tag-based case routing
