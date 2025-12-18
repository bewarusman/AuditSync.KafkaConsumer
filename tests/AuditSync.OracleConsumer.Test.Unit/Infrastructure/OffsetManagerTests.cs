using AuditSync.OracleConsumer.Infrastructure.Kafka;
using FluentAssertions;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Infrastructure;

public class OffsetManagerTests
{
    [Fact]
    public void StoreOffset_ShouldStoreOffsetForPartition()
    {
        // Arrange
        var offsetManager = new OffsetManager();
        int partition = 0;
        long offset = 12345;

        // Act
        offsetManager.StoreOffset(partition, offset);

        // Assert
        var retrieved = offsetManager.GetLastOffset(partition);
        retrieved.Should().Be(offset);
    }

    [Fact]
    public void GetLastOffset_ShouldReturnNull_WhenPartitionNotFound()
    {
        // Arrange
        var offsetManager = new OffsetManager();

        // Act
        var result = offsetManager.GetLastOffset(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void StoreOffset_ShouldUpdateExistingOffset()
    {
        // Arrange
        var offsetManager = new OffsetManager();
        int partition = 0;

        // Act
        offsetManager.StoreOffset(partition, 100);
        offsetManager.StoreOffset(partition, 200);

        // Assert
        var result = offsetManager.GetLastOffset(partition);
        result.Should().Be(200);
    }

    [Fact]
    public void StoreOffset_ShouldHandleMultiplePartitions()
    {
        // Arrange
        var offsetManager = new OffsetManager();

        // Act
        offsetManager.StoreOffset(0, 100);
        offsetManager.StoreOffset(1, 200);
        offsetManager.StoreOffset(2, 300);

        // Assert
        offsetManager.GetLastOffset(0).Should().Be(100);
        offsetManager.GetLastOffset(1).Should().Be(200);
        offsetManager.GetLastOffset(2).Should().Be(300);
    }

    [Fact]
    public void OffsetManager_ShouldBeThreadSafe()
    {
        // Arrange
        var offsetManager = new OffsetManager();
        int partition = 0;
        var tasks = new List<Task>();

        // Act - Multiple threads storing offsets
        for (int i = 0; i < 100; i++)
        {
            var offset = i;
            tasks.Add(Task.Run(() => offsetManager.StoreOffset(partition, offset)));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - Should have a valid offset stored (last one wins)
        var result = offsetManager.GetLastOffset(partition);
        result.Should().NotBeNull();
        result.Should().BeGreaterOrEqualTo(0).And.BeLessThan(100);
    }
}
