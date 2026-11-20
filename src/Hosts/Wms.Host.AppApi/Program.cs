using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Wms.Host.AppApi;
using Wms.Infrastructure.Common;
using Wms.Platform.Endpoints;
using Wms.Web.Common;

// App API host: endpoints for the web app and handhelds (/api/app). HLD section 4.
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Modules composed by this host. Adding a module = one line here.
IModule[] modules =
[
    new PlatformModule(),
];

var connectionString = builder.Configuration.GetConnectionString("Wms")
    ?? throw new InvalidOperationException("Connection string 'Wms' is missing.");

builder.Services
    .AddWmsInfrastructure(connectionString)
    .AddWmsWeb();

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration);
}

// After all modules: wrap every handler in validation, transaction, logging.
builder.Services.AddHandlerPipeline();

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

var otlpEndpoint = builder.Configuration["Otel:Endpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("wms-app-api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddSource("Npgsql");
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

await app.RunAsync().ConfigureAwait(false);
