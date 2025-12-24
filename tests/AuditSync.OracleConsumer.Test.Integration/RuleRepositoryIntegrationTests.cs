using AuditSync.OracleConsumer.Infrastructure.Repositories;
using FluentAssertions;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Integration;

[Trait("Category", "Integration")]
public class RuleRepositoryIntegrationTests : DatabaseIntegrationTestBase
{
    [Fact]
    public async Task GetRulesByTargetAsync_ShouldReturnRulesForTarget()
    {
        // Arrange
        var repository = new RuleRepository(ConnectionString, CreateLogger<RuleRepository>());
        await InsertTestTargetAndRulesAsync();

        // Act
        var rules = await repository.GetRulesByTargetAsync("Production Oracle Database");

        // Assert
        rules.Should().NotBeNull();
        rules.Should().HaveCount(3);

        // Verify rules are ordered by RULE_ORDER
        rules[0].RuleName.Should().Be("TABLE_NAME");
        rules[0].SourceField.Should().Be("name");
        rules[0].RegexPattern.Should().Be("^(\\w+)$");
        rules[0].IsActive.Should().BeTrue();
        rules[0].RuleOrder.Should().Be(1);

        rules[1].RuleName.Should().Be("SCHEMA");
        rules[1].SourceField.Should().Be("owner");
        rules[1].RuleOrder.Should().Be(2);

        rules[2].RuleName.Should().Be("MSISDN");
        rules[2].SourceField.Should().Be("sqlText");
        rules[2].RuleOrder.Should().Be(3);
    }

    [Fact]
    public async Task GetRulesByTargetAsync_ShouldReturnEmptyList_ForNonExistentTarget()
    {
        // Arrange
        var repository = new RuleRepository(ConnectionString, CreateLogger<RuleRepository>());
        await InsertTestTargetAndRulesAsync();

        // Act
        var rules = await repository.GetRulesByTargetAsync("Non-Existent Target");

        // Assert
        rules.Should().NotBeNull();
        rules.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRulesByTargetAsync_ShouldOnlyReturnActiveRules()
    {
        // Arrange
        var repository = new RuleRepository(ConnectionString, CreateLogger<RuleRepository>());
        await InsertTestTargetWithActiveAndInactiveRulesAsync();

        // Act
        var rules = await repository.GetRulesByTargetAsync("Test Target");

        // Assert
        rules.Should().NotBeNull();
        rules.Should().HaveCount(2); // Only 2 active rules
        rules.Should().AllSatisfy(r => r.IsActive.Should().BeTrue());
    }

    [Fact]
    public async Task GetRulesByTargetAsync_ShouldIncludeTargetNameInResults()
    {
        // Arrange
        var repository = new RuleRepository(ConnectionString, CreateLogger<RuleRepository>());
        await InsertTestTargetAndRulesAsync();

        // Act
        var rules = await repository.GetRulesByTargetAsync("Production Oracle Database");

        // Assert
        rules.Should().AllSatisfy(r => r.TargetName.Should().Be("Production Oracle Database"));
    }

    [Fact]
    public async Task GetRulesByTargetAsync_ShouldOrderByRuleOrder()
    {
        // Arrange
        var repository = new RuleRepository(ConnectionString, CreateLogger<RuleRepository>());
        await InsertTestTargetWithUnorderedRulesAsync();

        // Act
        var rules = await repository.GetRulesByTargetAsync("Order Test");

        // Assert
        rules.Should().HaveCount(4);
        rules[0].RuleOrder.Should().Be(1);
        rules[1].RuleOrder.Should().Be(2);
        rules[2].RuleOrder.Should().Be(5);
        rules[3].RuleOrder.Should().Be(10);
    }

    // Helper methods to insert test data
    private async Task InsertTestTargetAndRulesAsync()
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();

        // Insert target
        using var targetCmd = connection.CreateCommand();
        targetCmd.CommandText = @"
            INSERT INTO targets (ID, NAME, DESCRIPTION)
            VALUES ('target-1', 'Production Oracle Database', 'Test target')";
        await targetCmd.ExecuteNonQueryAsync();

        // Insert rules
        using var ruleCmd1 = connection.CreateCommand();
        ruleCmd1.CommandText = @"
            INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_ACTIVE, RULE_ORDER)
            VALUES ('rule-1', 'target-1', 'TABLE_NAME', 'name', '^(\w+)$', 1, 1)";
        await ruleCmd1.ExecuteNonQueryAsync();

        using var ruleCmd2 = connection.CreateCommand();
        ruleCmd2.CommandText = @"
            INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_ACTIVE, RULE_ORDER)
            VALUES ('rule-2', 'target-1', 'SCHEMA', 'owner', '^(\w+)$', 1, 2)";
        await ruleCmd2.ExecuteNonQueryAsync();

        using var ruleCmd3 = connection.CreateCommand();
        ruleCmd3.CommandText = @"
            INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_ACTIVE, RULE_ORDER)
            VALUES ('rule-3', 'target-1', 'MSISDN', 'sqlText', 'MSISDN=:(\\w+)', 1, 3)";
        await ruleCmd3.ExecuteNonQueryAsync();
    }

    private async Task InsertTestTargetWithActiveAndInactiveRulesAsync()
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();

        // Insert target
        using var targetCmd = connection.CreateCommand();
        targetCmd.CommandText = @"
            INSERT INTO targets (ID, NAME, DESCRIPTION)
            VALUES ('target-2', 'Test Target', 'Test target with active/inactive rules')";
        await targetCmd.ExecuteNonQueryAsync();

        // Insert active rule 1
        using var ruleCmd1 = connection.CreateCommand();
        ruleCmd1.CommandText = @"
            INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_ACTIVE, RULE_ORDER)
            VALUES ('rule-4', 'target-2', 'ACTIVE_RULE_1', 'name', 'test', 1, 1)";
        await ruleCmd1.ExecuteNonQueryAsync();

        // Insert inactive rule
        using var ruleCmd2 = connection.CreateCommand();
        ruleCmd2.CommandText = @"
            INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_ACTIVE, RULE_ORDER)
            VALUES ('rule-5', 'target-2', 'INACTIVE_RULE', 'name', 'test', 0, 2)";
        await ruleCmd2.ExecuteNonQueryAsync();

        // Insert active rule 2
        using var ruleCmd3 = connection.CreateCommand();
        ruleCmd3.CommandText = @"
            INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_ACTIVE, RULE_ORDER)
            VALUES ('rule-6', 'target-2', 'ACTIVE_RULE_2', 'owner', 'test', 1, 3)";
        await ruleCmd3.ExecuteNonQueryAsync();
    }

    private async Task InsertTestTargetWithUnorderedRulesAsync()
    {
        using var connection = new OracleConnection(ConnectionString);
        await connection.OpenAsync();

        // Insert target
        using var targetCmd = connection.CreateCommand();
        targetCmd.CommandText = @"
            INSERT INTO targets (ID, NAME, DESCRIPTION)
            VALUES ('target-3', 'Order Test', 'Test rule ordering')";
        await targetCmd.ExecuteNonQueryAsync();

        // Insert rules in non-sequential order
        var rules = new[] { (10, "RULE_D"), (2, "RULE_B"), (5, "RULE_C"), (1, "RULE_A") };

        foreach (var (order, name) in rules)
        {
            using var ruleCmd = connection.CreateCommand();
            ruleCmd.CommandText = @"
                INSERT INTO target_rules (ID, TARGET_ID, RULE_NAME, SOURCE_FIELD, REGEX_PATTERN, IS_ACTIVE, RULE_ORDER)
                VALUES (:Id, 'target-3', :Name, 'name', 'test', 1, :Order)";
            ruleCmd.Parameters.Add("Id", $"rule-order-{order}");
            ruleCmd.Parameters.Add("Name", name);
            ruleCmd.Parameters.Add("Order", order);
            await ruleCmd.ExecuteNonQueryAsync();
        }
    }
}
