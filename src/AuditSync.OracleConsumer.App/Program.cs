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
        Environment.GetEnvironmentVariable("KAFKA_AUTO_OFFSET_RESET") ?? "Earliest", ignoreCase: true),
    EnableAutoCommit = bool.Parse(
        Environment.GetEnvironmentVariable("KAFKA_ENABLE_AUTO_COMMIT") ?? "false"),
    SessionTimeoutMs = int.Parse(
        Environment.GetEnvironmentVariable("KAFKA_SESSION_TIMEOUT_MS") ?? "30000"),
    MaxPollIntervalMs = int.Parse(
        Environment.GetEnvironmentVariable("KAFKA_MAX_POLL_INTERVAL_MS") ?? "300000")
};

// Add SSL/TLS configuration if specified
var securityProtocol = Environment.GetEnvironmentVariable("KAFKA_SECURITY_PROTOCOL");
if (!string.IsNullOrEmpty(securityProtocol))
{
    kafkaConfig.SecurityProtocol = Enum.Parse<SecurityProtocol>(securityProtocol, ignoreCase: true);

    var sslCaLocation = Environment.GetEnvironmentVariable("KAFKA_SSL_CA_LOCATION");
    if (!string.IsNullOrEmpty(sslCaLocation))
    {
        kafkaConfig.SslCaLocation = Path.Combine(Directory.GetCurrentDirectory(), sslCaLocation);
    }

    var sslCertLocation = Environment.GetEnvironmentVariable("KAFKA_SSL_CERTIFICATE_LOCATION");
    if (!string.IsNullOrEmpty(sslCertLocation))
    {
        kafkaConfig.SslCertificateLocation = Path.Combine(Directory.GetCurrentDirectory(), sslCertLocation);
    }

    var sslKeyLocation = Environment.GetEnvironmentVariable("KAFKA_SSL_KEY_LOCATION");
    if (!string.IsNullOrEmpty(sslKeyLocation))
    {
        kafkaConfig.SslKeyLocation = Path.Combine(Directory.GetCurrentDirectory(), sslKeyLocation);
    }
}

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

builder.Services.AddSingleton<ITargetRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TargetRepository>>();
    return new TargetRepository(oracleConnectionString, logger);
});

// Register Case Repositories
builder.Services.AddSingleton<ICaseRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CaseRepository>>();
    return new CaseRepository(oracleConnectionString, logger);
});

builder.Services.AddSingleton<ICaseExtractionRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<CaseExtractionRepository>>();
    return new CaseExtractionRepository(oracleConnectionString, logger);
});

builder.Services.AddSingleton<IRuleTagRepository>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RuleTagRepository>>();
    return new RuleTagRepository(oracleConnectionString, logger);
});

// Register Rules Engine Repositories (NEW)
var enableRulesEngine = bool.Parse(Environment.GetEnvironmentVariable("AUDITSYNC_ENABLE_RULES_ENGINE") ?? "false");
if (enableRulesEngine)
{
    builder.Services.AddSingleton<IRulesEngineRepository>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<RulesEngineRepository>>();
        return new RulesEngineRepository(oracleConnectionString, logger);
    });

    builder.Services.AddSingleton<IRuleExtractionRepository>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<RuleExtractionRepository>>();
        return new RuleExtractionRepository(oracleConnectionString, logger);
    });

    // Register Rules Engine Services (NEW)
    var jsTimeout = int.Parse(Environment.GetEnvironmentVariable("AUDITSYNC_JAVASCRIPT_TIMEOUT_SECONDS") ?? "5");
    builder.Services.AddSingleton(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<JavaScriptExtractor>>();
        return new JavaScriptExtractor(logger, jsTimeout);
    });

    builder.Services.AddSingleton<RulesEngineService>();

    // Register Rules Cache (NEW)
    var targetName = Environment.GetEnvironmentVariable("AUDITSYNC_TARGET_SYSTEM_NAME") ?? "DWH";
    var refreshHours = int.Parse(Environment.GetEnvironmentVariable("AUDITSYNC_RULES_CACHE_REFRESH_HOURS") ?? "24");
    builder.Services.AddSingleton(sp =>
    {
        var repository = sp.GetRequiredService<IRulesEngineRepository>();
        var logger = sp.GetRequiredService<ILogger<RulesCache>>();
        return new RulesCache(repository, targetName, logger, refreshHours);
    });

    Console.WriteLine($"Rules Engine ENABLED - Target: {targetName}, Cache Refresh: {refreshHours}h, JS Timeout: {jsTimeout}s");
}
else
{
    Console.WriteLine("Rules Engine DISABLED - Using legacy extraction rules");
}

// Register Services
builder.Services.AddSingleton<IRuleEngine, RegexRuleEngine>();
builder.Services.AddSingleton<IAuditDataService, AuditDataService>();
builder.Services.AddSingleton<IOffsetManager, OffsetManager>();
builder.Services.AddSingleton<ITagEvaluationService, TagEvaluationService>();
builder.Services.AddSingleton<IExtractionService, ExtractionService>();
builder.Services.AddSingleton<ICaseService, CaseService>();

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
