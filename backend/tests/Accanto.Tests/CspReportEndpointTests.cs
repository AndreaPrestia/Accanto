using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Accanto.Tests;

public class CspReportEndpointTests : IClassFixture<AccantoFactory>
{
    private readonly AccantoFactory _factory;

    public CspReportEndpointTests(AccantoFactory factory) => _factory = factory;

    [Fact]
    public async Task Legacy_csp_report_returns_204()
    {
        var client = _factory.CreateClient();
        var payload = """
        { "csp-report": {
            "document-uri": "https://app.accanto.care/",
            "violated-directive": "script-src",
            "blocked-uri": "https://evil.example.com/x.js",
            "source-file": "https://app.accanto.care/main.js",
            "line-number": 12,
            "column-number": 5,
            "disposition": "enforce"
        } }
        """;
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/security/csp-report")
        {
            Content = new StringContent(payload, Encoding.UTF8)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/csp-report");

        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reporting_api_format_returns_204()
    {
        var client = _factory.CreateClient();
        var payload = """
        [{
          "type": "csp-violation",
          "age": 10,
          "url": "https://app.accanto.care/",
          "user_agent": "Mozilla/5.0",
          "body": {
            "documentURL": "https://app.accanto.care/",
            "blockedURL": "https://evil.example.com/x.js",
            "effectiveDirective": "script-src-elem",
            "originalPolicy": "default-src 'self'",
            "disposition": "enforce",
            "statusCode": 200,
            "lineNumber": 1,
            "columnNumber": 1
          }
        }]
        """;
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/security/csp-report")
        {
            Content = new StringContent(payload, Encoding.UTF8)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/reports+json");

        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Invalid_json_returns_204_silently()
    {
        // Non vogliamo dare segnali utili a chi tenta di sondare l'endpoint:
        // 204 sempre, anche su body invalido.
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/security/csp-report")
        {
            Content = new StringContent("{ not json", Encoding.UTF8)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/csp-report");

        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Empty_body_returns_204()
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/security/csp-report")
        {
            Content = new StringContent("", Encoding.UTF8)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/csp-report");

        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Endpoint_is_anonymous()
    {
        // Nessun token Authorization -> deve comunque accettare il report,
        // perche' il browser non puo' allegare credenziali a un report CSP.
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/security/csp-report")
        {
            Content = new StringContent("{\"csp-report\":{}}", Encoding.UTF8)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/csp-report");

        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        resp.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
