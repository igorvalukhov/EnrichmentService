using EnrichmentService.Abstractions;
using EnrichmentService.Configuration;
using EnrichmentService.Kafka;
using EnrichmentService.Services;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Confluent.Kafka", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/enrichment-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services
    .AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection(KafkaOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<EnrichmentSchemaOptions>()
    .Bind(builder.Configuration.GetSection(EnrichmentSchemaOptions.SectionName));

builder.Services.AddSingleton<IJsonPathAccessor, JsonPathAccessor>();
builder.Services.AddHostedService<KafkaConsumerService>();

var host = builder.Build();

try
{
    Log.Information("Starting Enrichment Service");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application Terminated Unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}