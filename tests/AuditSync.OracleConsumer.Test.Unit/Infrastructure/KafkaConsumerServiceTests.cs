using AuditSync.OracleConsumer.Infrastructure.Kafka;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.Infrastructure;

public class KafkaConsumerServiceTests
{
    private readonly Mock<IConsumer<string, string>> _consumerMock;
    private readonly Mock<ILogger<KafkaConsumerService>> _loggerMock;

    public KafkaConsumerServiceTests()
    {
        _consumerMock = new Mock<IConsumer<string, string>>();
        _loggerMock = new Mock<ILogger<KafkaConsumerService>>();
    }

    [Fact]
    public void Constructor_ShouldNotThrow_WithValidParameters()
    {
        // Act
        Action act = () => new KafkaConsumerService(_consumerMock.Object, _loggerMock.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Subscribe_ShouldCallConsumerSubscribe()
    {
        // Arrange
        var service = new KafkaConsumerService(_consumerMock.Object, _loggerMock.Object);

        // Act
        service.Subscribe("test-topic");

        // Assert
        _consumerMock.Verify(c => c.Subscribe("test-topic"), Times.Once);
    }

    [Fact]
    public void Consume_ShouldCallConsumerConsume()
    {
        // Arrange
        var service = new KafkaConsumerService(_consumerMock.Object, _loggerMock.Object);
        var cts = new CancellationTokenSource();

        // Act
        service.Consume(cts.Token);

        // Assert
        _consumerMock.Verify(c => c.Consume(cts.Token), Times.Once);
    }

    [Fact]
    public void Dispose_ShouldDisposeConsumer()
    {
        // Arrange
        var service = new KafkaConsumerService(_consumerMock.Object, _loggerMock.Object);

        // Act
        service.Dispose();

        // Assert
        _consumerMock.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public void Commit_ShouldCallConsumerCommit()
    {
        // Arrange
        var service = new KafkaConsumerService(_consumerMock.Object, _loggerMock.Object);
        var consumeResult = new ConsumeResult<string, string>
        {
            Message = new Message<string, string> { Key = "key", Value = "value" },
            Partition = new Partition(0),
            Offset = new Offset(100)
        };

        // Act
        service.Commit(consumeResult);

        // Assert
        _consumerMock.Verify(c => c.Commit(consumeResult), Times.Once);
    }
}
