using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using UblTr.Api.Infrastructure;
using UblTr.Validator;
using UblTr.Validator.Rules;

namespace UblTr.Api;

public sealed class ValidationApiOptions
{
    public const string Section = "Validation";

    /// <summary>Largest accepted request body. A 50,000-line invoice is about 30 MB; the default is 20 MB.</summary>
    public long MaxDocumentBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Validations running at once (0 = processor count). Validation is CPU-bound, so more only adds latency.</summary>
    public int MaxConcurrentValidations { get; set; }

    /// <summary>Requests that may wait for a slot before getting 503.</summary>
    public int QueueLimit { get; set; } = 64;

    /// <summary>Directory with the GIB xsdrt/ and schematron/ folders; empty = next to the binaries.</summary>
    public string? GibDirectory { get; set; }
}

public sealed record RuleInfo(string Id, string Category, Severity Severity, RuleOrigin Origin, string Source, string Title, string Explanation);

internal static class ValidationEndpoints
{
    public const string ValidationLimiter = "validation";

    private static readonly string[] XmlContentTypes = ["application/xml", "text/xml"];

    public static RouteGroupBuilder MapValidationEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/validate", ValidateAsync)
            .WithName("Validate")
            .WithSummary("Validate one UBL-TR invoice (XSD + business rules)")
            .WithDescription("Send the invoice XML as the raw request body (Content-Type: application/xml). A validation failure is not an HTTP error: " +
                             "the response is 200 with valid=false and one issue per finding, each with a Turkish or English explanation and fix.")
            .Accepts<string>("application/xml", "text/xml")
            .Produces<ValidationReport>()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(ValidationLimiter);

        api.MapGet("/rules", (string? lang) =>
            {
                var language = LanguageParser.Parse(lang);
                return RuleCatalog.All.Select(r => Describe(r, language));
            })
            .WithName("ListRules")
            .WithSummary("Every rule with its origin (GIB Schematron, GIB-derived, consistency, structure)");

        api.MapGet("/rules/{id}", (string id, string? lang) =>
                RuleCatalog.Find(id) is { } rule ? Results.Ok(Describe(rule, LanguageParser.Parse(lang))) : Results.Problem(
                    statusCode: StatusCodes.Status404NotFound, title: "Unknown rule", detail: $"No rule with id '{id}'."))
            .WithName("GetRule")
            .WithSummary("One rule");

        return api;
    }

    private static async Task<IResult> ValidateAsync(HttpContext http, InvoiceValidator validator, ValidationMetrics metrics,
        IOptions<ValidationApiOptions> options, string? lang, string? kind, CancellationToken ct)
    {
        var language = lang is null ? FromAcceptLanguage(http.Request) : LanguageParser.Parse(lang);
        var mediaType = http.Request.ContentType?.Split(';')[0].Trim();
        if (mediaType is null || !XmlContentTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
        {
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Unsupported media type",
                detail: "Send the invoice as the raw request body with Content-Type: application/xml.");
        }

        var limit = options.Value.MaxDocumentBytes;
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeFeature)
        {
            sizeFeature.MaxRequestBodySize = limit;
        }

        if (http.Request.ContentLength > limit)
        {
            return TooLarge(limit);
        }

        // Kestrel forbids synchronous reads of the request body, and the validator reads synchronously (XmlReader with
        // schema validation feeding XDocument). The body is therefore buffered first, bounded by the size limit.
        using var buffer = new MemoryStream();
        try
        {
            await CopyBoundedAsync(http.Request.Body, buffer, limit, ct);
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge(limit);
        }
        catch (InvalidDataException)
        {
            return TooLarge(limit);
        }

        buffer.Position = 0;
        var validationOptions = new ValidationOptions
        {
            Kind = kind?.ToLowerInvariant() switch { "efatura" => DocumentKind.EFatura, "earsiv" => DocumentKind.EArsiv, _ => DocumentKind.Auto },
        };

        var started = Stopwatch.GetTimestamp();
        var result = validator.Validate(buffer, validationOptions);
        metrics.Record(result, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return Results.Json(result.ToReport(language), ReportJson.Compact);
    }

    private static async Task CopyBoundedAsync(Stream source, Stream destination, long limit, CancellationToken ct)
    {
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            total += read;
            if (total > limit)
            {
                throw new InvalidDataException("Request body exceeds the configured limit.");
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }

    private static IResult TooLarge(long limit) => Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "Document too large", detail: $"The invoice exceeds the {limit:N0}-byte limit (Validation:MaxDocumentBytes).");

    private static Language FromAcceptLanguage(HttpRequest request)
    {
        var header = request.Headers.AcceptLanguage.ToString();
        return header.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? Language.En : Language.Tr;
    }

    private static RuleInfo Describe(RuleDescriptor r, Language language) =>
        new(r.Id, r.Category, r.Severity, r.Origin, r.Source, r.Title.In(language), r.Explanation.In(language));
}
