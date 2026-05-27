using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Ai;

/// <summary>
/// Cache idempotency 1h per chiamate AI. Chiave: SHA256 di
/// userId | circleId | function | input-normalizzato. Cache solo verdetti "Passed"
/// (i fallimenti vengono ritentati). Quando entry esistente, il chiamante non chiama
/// il modello e non crea una nuova AiInteraction.
/// </summary>
public sealed class AiIdempotencyCache
{
    private readonly IMemoryCache _cache;
    private readonly AiOptions _options;

    public AiIdempotencyCache(IMemoryCache cache, IOptions<AiOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public bool Enabled => _options.CacheTtlMinutes > 0;

    public string BuildKey(Guid userId, Guid? circleId, string function, string normalizedInput)
    {
        var payload = $"{userId:N}|{circleId:N}|{function}|{Normalize(normalizedInput)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return "ai:" + Convert.ToHexString(bytes);
    }

    public bool TryGet(string key, out CachedAiResponse entry)
    {
        if (!Enabled) { entry = default!; return false; }
        return _cache.TryGetValue(key, out entry!);
    }

    public void Set(string key, CachedAiResponse entry)
    {
        if (!Enabled) return;
        _cache.Set(key, entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.CacheTtlMinutes)
        });
    }

    /// <summary>Rimuove (es. dopo feedback negativo).</summary>
    public void Invalidate(string key) => _cache.Remove(key);

    /// <summary>
    /// Normalizzazione minima: trim + collapse di whitespace + lower-invariant.
    /// Sufficiente per evitare miss banali (capitalizzazione, spazi multipli).
    /// </summary>
    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        var prevSpace = false;
        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevSpace) { sb.Append(' '); prevSpace = true; }
            }
            else { sb.Append(ch); prevSpace = false; }
        }
        return sb.ToString();
    }
}

/// <summary>Entry di cache: risposta finale + id dell'interazione originale già persistita.</summary>
public sealed record CachedAiResponse(AiResponse Response, Guid InteractionId, string Language);
