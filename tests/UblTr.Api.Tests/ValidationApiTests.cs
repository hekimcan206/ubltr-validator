using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using UblTr.SampleGen;

namespace UblTr.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("Validation:MaxDocumentBytes", "200000").UseSetting("Validation:MaxConcurrentValidations", "2");
}

public sealed class ValidationApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static StringContent Xml(string xml, string mediaType = "application/xml") => new(xml, Encoding.UTF8, mediaType);

    private async Task<JsonElement> PostAsync(string xml, string query = "")
    {
        var response = await _client.PostAsync($"/api/v1/validate{query}", Xml(xml), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Valid_invoice_returns_valid_report()
    {
        var report = await PostAsync(InvoiceWriter.ToXmlString(SampleCatalog.Basic));

        report.GetProperty("valid").GetBoolean().Should().BeTrue();
        report.GetProperty("schemaValidated").GetBoolean().Should().BeTrue();
        report.GetProperty("document").GetProperty("lineCount").GetInt32().Should().Be(4);
        report.GetProperty("codeLists").GetString().Should().Contain("UBL-TR_Codelist.xml");
    }

    [Fact]
    public async Task Invalid_invoice_is_200_with_turkish_issues_by_default()
    {
        var xml = InvoiceWriter.ToXmlString(SampleCatalog.Basic with { Id = "SYN26-0001" });
        var report = await PostAsync(xml);

        report.GetProperty("valid").GetBoolean().Should().BeFalse();
        report.GetProperty("language").GetString().Should().Be("tr");
        var issue = report.GetProperty("issues")[0];
        issue.GetProperty("ruleId").GetString().Should().Be("HDR-010");
        issue.GetProperty("fix").GetString().Should().Contain("sıra");
    }

    [Fact]
    public async Task Language_comes_from_query_or_accept_language()
    {
        var xml = InvoiceWriter.ToXmlString(SampleCatalog.Basic with { Id = "bad" });
        (await PostAsync(xml, "?lang=en")).GetProperty("language").GetString().Should().Be("en");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/validate") { Content = Xml(xml) };
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-GB"));
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        report.GetProperty("issues")[0].GetProperty("title").GetString().Should().Be("Invoice number has the wrong format");
    }

    [Fact]
    public async Task Kind_parameter_selects_the_earchive_regime()
    {
        var report = await PostAsync(InvoiceWriter.ToXmlString(SampleCatalog.Basic), "?kind=earsiv");
        report.GetProperty("issues")[0].GetProperty("ruleId").GetString().Should().Be("HDR-003");
    }

    [Fact]
    public async Task Malformed_xml_is_a_finding_not_a_server_error()
    {
        var report = await PostAsync("<Invoice><broken></Invoice>");
        report.GetProperty("issues")[0].GetProperty("ruleId").GetString().Should().Be("XML-001");
    }

    [Fact]
    public async Task Wrong_content_type_is_415_problem()
    {
        var response = await _client.PostAsync("/api/v1/validate", new StringContent("{}", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Oversized_body_is_413_problem()
    {
        var huge = InvoiceWriter.ToXmlString(SyntheticData.RandomInvoice(3, 600));
        huge.Length.Should().BeGreaterThan(200_000);
        var response = await _client.PostAsync("/api/v1/validate", Xml(huge), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Text_xml_is_accepted_too()
    {
        var response = await _client.PostAsync("/api/v1/validate", Xml(InvoiceWriter.ToXmlString(SampleCatalog.EArchive), "text/xml"),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rules_are_listed_and_fetched_by_id()
    {
        var rules = await _client.GetFromJsonAsync<JsonElement>("/api/v1/rules?lang=en", TestContext.Current.CancellationToken);
        rules.GetArrayLength().Should().BeGreaterThan(40);
        rules[0].GetProperty("origin").GetString().Should().Be("structure");

        var rule = await _client.GetFromJsonAsync<JsonElement>("/api/v1/rules/tot-002", TestContext.Current.CancellationToken);
        rule.GetProperty("title").GetString().Should().Be("Satır toplamı LegalMonetaryTotal ile uyuşmuyor");

        (await _client.GetAsync("/api/v1/rules/NOPE-000", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_and_openapi_are_served()
    {
        (await _client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        var openApi = await _client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        openApi.Should().Contain("/api/v1/validate");
    }

    [Fact]
    public async Task Concurrent_requests_all_get_correct_answers()
    {
        var xmls = Enumerable.Range(0, 24).Select(i => InvoiceWriter.ToXmlString(SyntheticData.RandomInvoice(100 + i, 20))).ToList();
        var responses = await Task.WhenAll(xmls.Select(x => _client.PostAsync("/api/v1/validate", Xml(x), TestContext.Current.CancellationToken)));
        foreach (var response in responses)
        {
            // Queue limit 64 > 24 requests, so none is rejected; each must come back valid.
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var report = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
            report.GetProperty("valid").GetBoolean().Should().BeTrue();
        }
    }
}
