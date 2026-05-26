using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accanto.Infrastructure.Ai;

/// <summary>
/// Implementazione di <see cref="IAiAssistant"/> contro un server Ollama self-hosted
/// (https://ollama.com). Usa l'endpoint REST <c>POST {endpoint}/api/generate</c> in modalità
/// non-streaming. Le 4 funzioni condividono la stessa chiamata: la differenziazione avviene
/// nel prompt costruito da <see cref="AiPromptBuilder"/>.
/// </summary>
public sealed class OllamaAssistant : IAiAssistant
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<OllamaAssistant> _logger;

    public OllamaAssistant(HttpClient http, IOptions<AiOptions> options, ILogger<OllamaAssistant> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            _http.BaseAddress = new Uri(_options.Endpoint.TrimEnd('/') + "/");
        }
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));
    }

    public Task<AiResponse> SummarizeTimelineAsync(string prompt, string language, CancellationToken cancellationToken = default)
        => GenerateAsync(prompt, cancellationToken);

    public Task<AiResponse> DraftDoctorQuestionAsync(string prompt, string language, CancellationToken cancellationToken = default)
        => GenerateAsync(prompt, cancellationToken);

    public Task<AiResponse> RephraseSharedUpdateAsync(string prompt, string language, CancellationToken cancellationToken = default)
        => GenerateAsync(prompt, cancellationToken);

    public Task<AiResponse> ReflectCheckInAsync(string prompt, string language, CancellationToken cancellationToken = default)
        => GenerateAsync(prompt, cancellationToken);

    private async Task<AiResponse> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var body = new OllamaGenerateRequest
        {
            Model = _options.Model,
            Prompt = prompt,
            Stream = false,
            Options = new OllamaGenerateOptions { NumPredict = _options.MaxOutputTokens }
        };

        try
        {
            var resp = await _http.PostAsJsonAsync("api/generate", body, cancellationToken);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ollama returned non-success status {Status} for model {Model}", (int)resp.StatusCode, _options.Model);
                throw new InvalidOperationException($"ai_provider_error:{(int)resp.StatusCode}");
            }

            var parsed = await resp.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            var text = (parsed?.Response ?? string.Empty).Trim();
            return new AiResponse(text, parsed?.Model ?? _options.Model, sw.ElapsedMilliseconds, string.Empty);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Ollama timeout after {Timeout}s", _options.TimeoutSeconds);
            throw new TimeoutException("ai_provider_timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Ollama HTTP error");
            throw new InvalidOperationException("ai_provider_unreachable", ex);
        }
    }

    private sealed class OllamaGenerateRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("options")] public OllamaGenerateOptions? Options { get; set; }
    }

    private sealed class OllamaGenerateOptions
    {
        [JsonPropertyName("num_predict")] public int NumPredict { get; set; }
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")] public string? Response { get; set; }
        [JsonPropertyName("model")] public string? Model { get; set; }
    }
}
