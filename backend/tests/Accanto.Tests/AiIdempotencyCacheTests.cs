using Accanto.Application.Ai;
using Accanto.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Accanto.Tests;

public class AiIdempotencyCacheTests
{
    private static AiIdempotencyCache Build(int ttlMinutes = 60)
        => new(new MemoryCache(new MemoryCacheOptions()),
               Options.Create(new AiOptions { CacheTtlMinutes = ttlMinutes }));

    [Fact]
    public void Same_inputs_produce_same_key()
    {
        var c = Build();
        var u = Guid.NewGuid();
        var k1 = c.BuildKey(u, null, "Rephrase", "ciao  mondo");
        var k2 = c.BuildKey(u, null, "Rephrase", "CIAO mondo");
        k1.Should().Be(k2);
    }

    [Fact]
    public void Different_users_produce_different_keys()
    {
        var c = Build();
        var k1 = c.BuildKey(Guid.NewGuid(), null, "Rephrase", "same");
        var k2 = c.BuildKey(Guid.NewGuid(), null, "Rephrase", "same");
        k1.Should().NotBe(k2);
    }

    [Fact]
    public void Set_then_get_returns_cached_entry()
    {
        var c = Build();
        var k = c.BuildKey(Guid.NewGuid(), null, AiInteractionFunction.Rephrase.ToString(), "hello");
        var resp = new AiResponse("ok", "m", 10, "d");
        c.Set(k, new CachedAiResponse(resp, Guid.NewGuid(), "it"));
        c.TryGet(k, out var got).Should().BeTrue();
        got.Response.Text.Should().Be("ok");
    }

    [Fact]
    public void Disabled_cache_never_stores()
    {
        var c = Build(ttlMinutes: 0);
        var k = c.BuildKey(Guid.NewGuid(), null, "X", "y");
        c.Set(k, new CachedAiResponse(new AiResponse("x", "m", 0, "d"), Guid.NewGuid(), "it"));
        c.TryGet(k, out _).Should().BeFalse();
    }
}
