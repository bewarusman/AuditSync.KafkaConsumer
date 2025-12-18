using AuditSync.OracleConsumer.App.Services;
using AuditSync.OracleConsumer.Application.Services;
using AuditSync.OracleConsumer.Domain.Interfaces;
using AuditSync.OracleConsumer.Infrastructure.Kafka;
using AuditSync.OracleConsumer.Infrastructure.Repositories;
using Confluent.Kafka;
using DotNetEnv;

var builder = WebApplication.CreateBuilder(args);

// Load .env file
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
    Console.WriteLine("Loaded .env file");
}

// Load environment variables into configuration
builder.Configuration.AddEnvironmentVariables();

// Build Oracle connection string
var oracleConnectionString = $"User Id={Environment.GetEnvironmentVariable("ORACLE_USERNAME")};" +
                             $"Password={Environment.GetEnvironmentVariable("ORACLE_PASSWORD")};" +
                             $"Data Source={Environment.GetEnvironmentVariable("ORACLE_HOST")}:" +
                             $"{Environment.GetEnvironmentVariable("ORACLE_PORT")}/" +
                             $"{Environment.GetEnvironmentVariable("ORACLE_SERVICE_NAME")};" +
                             $"Min Pool Size={Environment.GetEnvironmentVariable("ORACLE_MIN_POOL_SIZE") ?? "1"};" +
                             $"Max Pool Size={Environment.GetEnvironmentVariable("ORACLE_MAX_POOL_SIZE") ?? "10"};" +
                             $"Connection Timeout={Environment.GetEnvironmentVariable("ORACLE_CONNECTION_TIMEOUT") ?? "30"}";

// Configure Kafka Consumer
var kafkaConfig = new ConsumerConfig
{
    BootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092",
    GroupId = Environment.GetEnvironmentVariable("KAFKA_GROUP_ID") ?? "auditsync-consumer-group",
    AutoOffsetReset = Enum.Parse<AutoOffsetReset>(
        Environment.GetEnvironmentVariable("KAFKA_AUTO_OFFSET_RESET") ?? "Earliest"),
    EnableAutoCommit = bool.Parse(
        Environment.GetEnvironmentVariable("KAFKA_ENABLE_AUTO_COMMIT") ?? "false"),
    SessionTimeoutMs = int.Parse(
        Environment.GetEnvironmentVariable("KAFKA_SESSION_TIMEOUT_MS") ?? "30000"),
    MaxPollIntervalMs = int.Parse(
        Environment.GetEnvironmentVariable("KAFKA_MAX_POLL_INTERVAL_MS") ?? "300000")
};

// Register Kafka Consumer
builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var consumer = new ConsumerBuilder<string, string>(kafkaConfig).Build();
    return consumer;
});

builder.Services.AddSingleton<KafkaConsumerService>();

// Register Repositories
builder.Services.AddSingleton<IRuleRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RuleRepository>>();
    return new RuleRepository(oracleConnectionString, logger);
});

builder.Services.AddSingleton<IAuditMessageRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<AuditMessageRepository>>();
    return new AuditMessageRepository(oracleConnectionString, logger);
});

builder.Services.AddSingleton<IExtractedValuesRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ExtractedValuesRepository>>();
    return new ExtractedValuesRepository(oracleConnectionString, logger);
});

// Register Services
builder.Services.AddSingleton<IRuleEngine, RegexRuleEngine>();
builder.Services.AddSingleton<IAuditDataService, AuditDataService>();
builder.Services.AddSingleton<IOffsetManager, OffsetManager>();

// Register Background Service
builder.Services.AddHostedService<AuditConsumerBackgroundService>();

// Add Health Checks
builder.Services.AddHealthChecks();

// Add controllers and API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

Console.WriteLine("AuditSync Oracle Consumer Application Starting...");
Console.WriteLine($"Kafka Bootstrap Servers: {kafkaConfig.BootstrapServers}");
Console.WriteLine($"Kafka Group ID: {kafkaConfig.GroupId}");
Console.WriteLine($"Kafka Topic: {Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "oracle.audit.events"}");

app.Run();
