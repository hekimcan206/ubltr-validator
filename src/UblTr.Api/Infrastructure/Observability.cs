using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;
using UblTr.Validator;

namespace UblTr.Api.Infrastructure;

internal static class Observability
{
    /// <summary>
    /// Structured JSON logs on stdout plus OpenTelemetry traces and metrics. The OTLP exporter is enabled only when
    /// OTEL_EXPORTER_OTLP_ENDPOINT is set, so the service runs without a collector.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(new RenderedCompactJsonFormatter()));

        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "ubltr-validator-api";
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion: typeof(Observability).Assembly.GetName().Version?.ToString(3)))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                .AddSource(InvoiceValidator.ActivitySource.Name))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(ValidationMetrics.MeterName));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        builder.Services.AddSingleton<ValidationMetrics>();
        return builder;
    }
}

/// <summary>Validation outcomes, latency and document size: what an operator needs to size and alert on the service.</summary>
internal sealed class ValidationMetrics : IDisposable
{
    public const string MeterName = "UblTr.Api";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _validations;
    private readonly Histogram<double> _duration;
    private readonly Histogram<long> _size;

    public ValidationMetrics()
    {
        _validations = _meter.CreateCounter<long>("ubltr.validations", description: "Validated documents by outcome");
        _duration = _meter.CreateHistogram<double>("ubltr.validation.duration", unit: "ms", description: "Validation time per document");
        _size = _meter.CreateHistogram<long>("ubltr.document.size", unit: "By", description: "Validated document size");
    }

    public void Record(ValidationResult result, double milliseconds)
    {
        var outcome = new KeyValuePair<string, object?>("outcome", result.IsValid ? "valid" : "invalid");
        _validations.Add(1, outcome);
        _duration.Record(milliseconds, outcome);
        _size.Record(result.SizeBytes);
    }

    public void Dispose() => _meter.Dispose();
}
