using EnrichmentService.Abstractions;
using EnrichmentService.Configuration;
using EnrichmentService.Kafka;
using EnrichmentService.Services;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
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
builder.Services
    .AddOptions<ExternalApiOptions>()
    .Bind(builder.Configuration.GetSection(ExternalApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddHttpClient<IExternalApiClient, ExternalApiClient>((sp, client) =>
    {
        var opts = sp.GetRequiredService<IOptions<ExternalApiOptions>>().Value;
        client.BaseAddress = new Uri(opts.BaseUrl);
        client.Timeout = opts.Timeout;
    })
    .AddPolicyHandler((sp, _) =>
    {
        var opts = sp.GetRequiredService<IOptions<ExternalApiOptions>>().Value;
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                opts.RetryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    var logger = sp.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning(
                        "Retry {Attempt} after {Delay}s. Reason: {Reason}",
                        attempt, delay.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString());
                });
    });

builder.Services.AddSingleton<IJsonPathAccessor, JsonPathAccessor>();
builder.Services.AddSingleton<IMessageMerger, MessageMerger>();
builder.Services.AddScoped<IEnrichmentOrchestrator, EnrichmentOrchestrator>();
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