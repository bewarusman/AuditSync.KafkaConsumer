# Integration Tests

## Overview
This project contains integration tests for the AuditSync Oracle Consumer application.

## Prerequisites

### For Oracle Database Integration Tests
These tests use Testcontainers to spin up a real Oracle database instance.

**Requirements:**
- Docker Desktop installed and running
- Sufficient system resources (Oracle container requires ~2GB RAM)
- Internet connection to pull Docker images

### For Kafka Integration Tests
- Kafka and Zookeeper containers via Testcontainers
- Or connection to an existing Kafka broker (update connection string in tests)

## Running Tests

### Run All Integration Tests
```bash
dotnet test
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~AuditMessageRepositoryIntegrationTests"
```

### Skip Integration Tests (CI/CD)
```bash
dotnet test --filter "Category!=Integration"
```

## Test Categories

### Database Integration Tests
- **AuditMessageRepositoryIntegrationTests** - Tests MERGE (upsert) behavior with real Oracle
- **ExtractedValuesRepositoryIntegrationTests** - Tests extracted values persistence
- **RuleRepositoryIntegrationTests** - Tests rule loading with JOIN queries
- **TransactionalIntegrationTests** - Tests atomicity and rollback

### End-to-End Tests
- **EndToEndIntegrationTests** - Full Kafka → Consumer → Database flow

## Notes

- Tests marked with `[Trait("Category", "Integration")]` require external dependencies
- Integration tests may take longer to run (30s - 2min) due to container startup
- Tests clean up after themselves (DROP tables, delete test data)
- If tests fail, check Docker Desktop is running and has sufficient resources

## Testcontainers Setup

Testcontainers will automatically:
1. Pull required Docker images (if not cached)
2. Start containers before tests
3. Stop and remove containers after tests

First run may take several minutes to download images.
