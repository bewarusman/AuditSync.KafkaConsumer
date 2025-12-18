-- =============================================================================
-- AuditSync Oracle Consumer - Database Schema Creation Script
-- =============================================================================
-- Description: Creates all required tables for the AuditSync Consumer application
-- Tables: audit_logs, audit_log_extracted_values, targets, target_rules
-- =============================================================================

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
    PROCESS_COUNTER NUMBER DEFAULT 1,
    PROCESSED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSUMED_AT TIMESTAMP DEFAULT SYSTIMESTAMP,
    CONSTRAINT UK_AUDIT_OFFSET UNIQUE (KAFKA_PARTITION, KAFKA_OFFSET)
);

-- Indexes for audit_logs
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

-- Indexes for audit_log_extracted_values
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

-- Indexes for targets
CREATE INDEX IDX_TARGETS_NAME ON targets(NAME);

-- Table 4: Store extraction rules for each target
-- Different targets can have different extraction rules
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

-- Indexes for target_rules
CREATE INDEX IDX_RULES_TARGET_ID ON target_rules(TARGET_ID);
CREATE INDEX IDX_RULES_ACTIVE ON target_rules(IS_ACTIVE);
CREATE INDEX IDX_RULES_ORDER ON target_rules(TARGET_ID, RULE_ORDER);

-- Success message
SELECT 'All tables created successfully!' AS STATUS FROM DUAL;
