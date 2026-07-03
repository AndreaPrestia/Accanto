using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Accanto.Application.Push;
using Accanto.Domain.Enums;
using Accanto.Infrastructure.Push;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Accanto.Tests;

public class ExpoPushClientTests
{
    [Fact]
    public async Task SendAsync_returns_empty_when_no_tokens()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("no call expected"));
        var client = BuildClient(handler);

        var result = await client.SendAsync(Array.Empty<string>(),
            new ExpoPushMessage("t", "b", null, NotificationTopic.TimelineEntryCreated));

        result.Should().BeEmpty();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_short_circuits_when_disabled()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("no call expected"));
        var client = BuildClient(handler, opts => opts.Disabled = true);

        var result = await client.SendAsync(new[] { "ExponentPushToken[x]" },
            new ExpoPushMessage("t", "b", null, NotificationTopic.SharedUpdateCreated));

        result.Should().BeEmpty();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_parses_DeviceNotRegistered_as_invalid_token()
    {
        var handler = new StubHandler(req =>
        {
            req.RequestUri!.ToString().Should().Contain("exp.host");
            var body = """
            {
              "data": [
                { "status": "ok", "id": "tk-1" },
                { "status": "error", "message": "gone", "details": { "error": "DeviceNotRegistered" } }
              ]
            }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var client = BuildClient(handler);

        var result = await client.SendAsync(
            new[] { "tok-A", "tok-B" },
            new ExpoPushMessage("title", "body", null, NotificationTopic.TimelineEntryCreated));

        result.Should().BeEquivalentTo(new[] { "tok-B" });
    }

    [Fact]
    public async Task SendAsync_ignores_non_token_errors()
    {
        // Errori come MessageTooBig non implicano token morto → NON cleanup.
        var handler = new StubHandler(_ =>
        {
            var body = """
            { "data": [ { "status": "error", "details": { "error": "MessageTooBig" } } ] }
            """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var client = BuildClient(handler);

        var result = await client.SendAsync(new[] { "tok-A" },
            new ExpoPushMessage("t", "b", null, NotificationTopic.TimelineEntryCreated));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_sends_topic_in_data_payload()
    {
        string? capturedBody = null;
        var handler = new StubHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"status":"ok","id":"x"}]}""")
            };
        });
        var client = BuildClient(handler);

        await client.SendAsync(new[] { "tok" },
            new ExpoPushMessage("ti", "bo",
                new Dictionary<string, string> { ["circleId"] = "abc" },
                NotificationTopic.InviteAccepted));

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        var first = doc.RootElement[0];
        first.GetProperty("to").GetString().Should().Be("tok");
        first.GetProperty("title").GetString().Should().Be("ti");
        var data = first.GetProperty("data");
        data.GetProperty("topic").GetString().Should().Be(nameof(NotificationTopic.InviteAccepted));
        data.GetProperty("circleId").GetString().Should().Be("abc");
    }

    private static ExpoPushClient BuildClient(StubHandler handler, Action<ExpoPushOptions>? configure = null)
    {
        var http = new HttpClient(handler);
        var options = new ExpoPushOptions();
        configure?.Invoke(options);
        return new ExpoPushClient(http, Options.Create(options), NullLogger<ExpoPushClient>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int Calls { get; private set; }
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) { _responder = responder; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_responder(request));
        }
    }
}
