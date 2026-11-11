using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;
using UblTr.Api;
using UblTr.Api.Infrastructure;
using UblTr.Validator;

var builder = WebApplication.CreateBuilder(args);
builder.AddObservability();

builder.Services.Configure<ValidationApiOptions>(builder.Configuration.GetSection(ValidationApiOptions.Section));
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

// The GIB XSDs are compiled once at startup and shared: compiling them per request would cost more than validating.
builder.Services.AddSingleton(sp =>
{
    var configured = sp.GetRequiredService<IOptions<ValidationApiOptions>>().Value.GibDirectory;
    return string.IsNullOrWhiteSpace(configured) ? GibAssets.LoadDefault() : GibAssets.Load(configured);
});
builder.Services.AddSingleton(sp => new InvoiceValidator(sp.GetRequiredService<GibAssets>()));

// Validation is CPU-bound: a concurrency limiter (not a request-rate limiter) keeps latency flat under load and turns
// overload into fast 503s instead of a growing queue of slow requests.
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;
    limiter.AddPolicy(ValidationEndpoints.ValidationLimiter, context =>
    {
        var o = context.RequestServices.GetRequiredService<IOptions<ValidationApiOptions>>().Value;
        return RateLimitPartition.GetConcurrencyLimiter("all", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = o.MaxConcurrentValidations > 0 ? o.MaxConcurrentValidations : Environment.ProcessorCount,
            QueueLimit = o.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    });
});

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier));
builder.Services.AddHealthChecks().AddCheck<GibSchemasHealthCheck>("gib-schemas", tags: ["ready"]);

builder.Services.AddOpenApi("v1", o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "ubltr-validator API";
    doc.Info.Version = "v1";
    doc.Info.Description = "UBL-TR (e-Fatura / e-Arşiv) validation: GIB XSDs plus business rules, with Turkish and English explanations.";
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseRateLimiter();

if (app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("ubltr-validator"));
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).ExcludeFromDescription();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).ExcludeFromDescription();
app.MapGroup("/api/v1").WithGroupName("v1").MapValidationEndpoints();

// Compile the schemas before accepting traffic so the first request does not pay for it.
var assets = app.Services.GetRequiredService<GibAssets>();
if (app.Logger.IsEnabled(LogLevel.Information))
{
    app.Logger.LogInformation("GIB assets: schemas {Schemas} (compiled in {CompileMs} ms), code lists {CodeLists}",
        assets.Schemas is null ? "missing" : "loaded", (int)assets.SchemaCompileTime.TotalMilliseconds, assets.CodeLists.Source);
}

await app.RunAsync();

/// <summary>Entry point; public so WebApplicationFactory can boot the app in tests.</summary>
public partial class Program;

/// <summary>Ready only when the XSDs compiled: without them every request would fail XSD validation setup.</summary>
internal sealed class GibSchemasHealthCheck(GibAssets assets) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(assets.Schemas is not null ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("GIB schemas not found"));
}
