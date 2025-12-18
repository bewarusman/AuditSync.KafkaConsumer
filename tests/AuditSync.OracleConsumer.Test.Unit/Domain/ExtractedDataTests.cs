using AuditSync.OracleConsumer.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Domain;

public class ExtractedDataTests
{
    [Fact]
    public void ExtractedData_ShouldInitializeExtractedFieldsDictionary()
    {
        // Arrange & Act
        var extractedData = new ExtractedData();

        // Assert
        extractedData.ExtractedFields.Should().NotBeNull();
        extractedData.ExtractedFields.Should().BeEmpty();
    }

    [Fact]
    public void ExtractedData_ShouldStoreFieldValuePairs()
    {
        // Arrange
        var extractedData = new ExtractedData
        {
            AuditRecordId = "test-id",
            Schema = "TESTSCHEMA",
            TableName = "TESTTABLE",
            SqlText = "SELECT * FROM TESTTABLE",
            ProcessedAt = DateTime.UtcNow
        };

        // Act
        extractedData.ExtractedFields["MSISDN"] = "1234567890";
        extractedData.ExtractedFields["STATUS_ID"] = "1";

        // Assert
        extractedData.ExtractedFields.Should().HaveCount(2);
        extractedData.ExtractedFields["MSISDN"].Should().Be("1234567890");
        extractedData.ExtractedFields["STATUS_ID"].Should().Be("1");
    }

    [Fact]
    public void ExtractedData_ShouldAllowEmptyExtractedFields()
    {
        // Arrange & Act
        var extractedData = new ExtractedData
        {
            AuditRecordId = "test-id",
            Schema = "SCHEMA",
            TableName = "TABLE",
            SqlText = "SELECT 1",
            ProcessedAt = DateTime.UtcNow
        };

        // Assert
        extractedData.ExtractedFields.Should().BeEmpty();
    }
}
