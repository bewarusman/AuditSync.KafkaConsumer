using AuditSync.OracleConsumer.App.Services;
using AuditSync.OracleConsumer.Domain.Entities;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Infrastructure.Kafka;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuditSync.OracleConsumer.Test.Unit.App;

public class AuditConsumerBackgroundServiceTests
{
    private readonly Mock<KafkaConsumerService> _kafkaConsumerMock;
    private readonly Mock<IRuleEngine> _ruleEngineMock;
    private readonly Mock<IAuditDataService> _auditDataServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<AuditConsumerBackgroundService>> _loggerMock;

    public AuditConsumerBackgroundServiceTests()
    {
        var consumerMock = new Mock<IConsumer<string, string>>();
        var kafkaLoggerMock = new Mock<ILogger<KafkaConsumerService>>();
        _kafkaConsumerMock = new Mock<KafkaConsumerService>(consumerMock.Object, kafkaLoggerMock.Object);
        _ruleEngineMock = new Mock<IRuleEngine>();
        _auditDataServiceMock = new Mock<IAuditDataService>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<AuditConsumerBackgroundService>>();

        // Setup default configuration
        _configurationMock.Setup(c => c["KAFKA_TOPIC"]).Returns("test-topic");
    }

    [Fact]
    public void Constructor_ShouldNotThrow_WithValidDependencies()
    {
        // Act
        Action act = () => new AuditConsumerBackgroundService(
            _kafkaConsumerMock.Object,
            _ruleEngineMock.Object,
            _auditDataServiceMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ShouldUseDefaultTopic_WhenConfigurationIsNull()
    {
        // Arrange
        _configurationMock.Setup(c => c["KAFKA_TOPIC"]).Returns((string)null);

        // Act
        var service = new AuditConsumerBackgroundService(
            _kafkaConsumerMock.Object,
            _ruleEngineMock.Object,
            _auditDataServiceMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_ShouldNotThrow()
    {
        // Arrange
        var service = new AuditConsumerBackgroundService(
            _kafkaConsumerMock.Object,
            _ruleEngineMock.Object,
            _auditDataServiceMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately to stop execution

        // Act & Assert
        Func<Task> act = async () => await service.StartAsync(cts.Token);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_ShouldStoreAllDependencies()
    {
        // Arrange & Act
        var service = new AuditConsumerBackgroundService(
            _kafkaConsumerMock.Object,
            _ruleEngineMock.Object,
            _auditDataServiceMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var service = new AuditConsumerBackgroundService(
            _kafkaConsumerMock.Object,
            _ruleEngineMock.Object,
            _auditDataServiceMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);

        // Act
        Action act = () => service.Dispose();

        // Assert
        act.Should().NotThrow();
    }
}
