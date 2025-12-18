using AuditSync.OracleConsumer.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Domain;

public class AuditMessageTests
{
    [Fact]
    public void AuditMessage_ShouldHaveAllRequiredProperties()
    {
        // Arrange & Act
        var message = new AuditMessage
        {
            Id = "test-id",
            Target = "Production Oracle Database",
            SessionId = 123456,
            EntryId = 1,
            Statement = 1,
            DbUser = "TESTUSER",
            UserHost = "TESTHOST",
            Terminal = "TERMINAL1",
            Action = 3,
            ReturnCode = 0,
            Owner = "OWNER1",
            Name = "TABLE1",
            AuthPrivileges = "SELECT",
            AuthGrantee = "GRANTEE1",
            NewOwner = "NEWOWNER",
            NewName = "NEWNAME",
            OsUser = "osuser",
            PrivilegeUsed = "PRIV1",
            Timestamp = DateTime.UtcNow,
            BindVariables = "#1(10):test",
            SqlText = "SELECT * FROM TABLE1",
            ProducedAt = DateTime.UtcNow
        };

        // Assert
        message.Id.Should().Be("test-id");
        message.Target.Should().Be("Production Oracle Database");
        message.SessionId.Should().Be(123456);
        message.EntryId.Should().Be(1);
        message.Action.Should().Be(3);
        message.Owner.Should().Be("OWNER1");
        message.Name.Should().Be("TABLE1");
        message.SqlText.Should().Be("SELECT * FROM TABLE1");
    }

    [Fact]
    public void AuditMessage_PrivilegeUsed_CanBeNull()
    {
        // Arrange & Act
        var message = new AuditMessage
        {
            PrivilegeUsed = null
        };

        // Assert
        message.PrivilegeUsed.Should().BeNull();
    }

    [Fact]
    public void AuditMessage_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var message = new AuditMessage();

        // Assert
        message.Id.Should().BeEmpty();
        message.Target.Should().BeEmpty();
        message.DbUser.Should().BeEmpty();
        message.SqlText.Should().BeEmpty();
    }
}
