using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Accanto.Application.Push;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Accanto.Infrastructure.Push;

/// <summary>
/// Implementazione di <see cref="IExpoPushClient"/> verso Expo Push
/// Service. Spedisce messaggi in batch da max 100 (limite documentato di
/// Expo) e legge la response per estrarre i token che il server segnala
/// come invalidi (es. <c>DeviceNotRegistered</c>), così che il caller
/// possa rimuoverli dal DB.
/// </summary>
public class ExpoPushClient : IExpoPushClient
{
    private const int MaxBatchSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ExpoPushOptions _options;
    private readonly ILogger<ExpoPushClient> _logger;

    public ExpoPushClient(HttpClient http, IOptions<ExpoPushOptions> options, ILogger<ExpoPushClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, ExpoPushMessage message, CancellationToken cancellationToken = default)
    {
        if (tokens is null || tokens.Count == 0) return Array.Empty<string>();
        if (_options.Disabled) return Array.Empty<string>();

        var invalid = new List<string>();

        // Manteniamo l'ordine 1:1 tra batch inviato e ticket ricevuto in
        // modo da poter mappare ciascun ticket al token corrispondente.
        foreach (var batch in Chunk(tokens, MaxBatchSize))
        {
            var messages = batch
                .Select(t => new ExpoMessageDto(
                    To: t,
                    Title: message.Title,
                    Body: message.Body,
                    Data: BuildData(message),
                    Sound: "default",
                    Priority: "high"))
                .ToList();

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(_options.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            }
            request.Content = JsonContent.Create(messages, options: JsonOptions);

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Fire-and-forget: il fallimento di rete non blocca il caller.
                _logger.LogWarning(ex, "Expo push: errore di rete inviando {Count} messaggi", batch.Count);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var bodyText = await SafeReadBody(response, cancellationToken);
                _logger.LogWarning("Expo push: status {Status}, body {Body}", response.StatusCode, bodyText);
                // 401/403 → access token mancante o scaduto, niente da
                // ripulire lato DB. Per altri 4xx/5xx non sappiamo quale
                // token sia il problema, quindi non rimuoviamo nulla.
                continue;
            }

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<ExpoSendResponseDto>(JsonOptions, cancellationToken);
                if (payload?.Data is null) continue;
                for (var i = 0; i < payload.Data.Count && i < batch.Count; i++)
                {
                    var ticket = payload.Data[i];
                    if (string.Equals(ticket.Status, "error", StringComparison.OrdinalIgnoreCase))
                    {
                        var errorCode = ticket.Details?.Error ?? string.Empty;
                        if (IsTokenInvalid(errorCode))
                        {
                            invalid.Add(batch[i]);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Expo push: errore non-token {Code} {Message}",
                                errorCode,
                                ticket.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Expo push: parsing response fallito");
            }
        }

        return invalid;
    }

    private static IReadOnlyDictionary<string, string> BuildData(ExpoPushMessage message)
    {
        var data = new Dictionary<string, string>(message.Data ?? new Dictionary<string, string>())
        {
            ["topic"] = message.Topic.ToString()
        };
        return data;
    }

    private static IEnumerable<List<string>> Chunk(IReadOnlyList<string> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.Skip(i).Take(size).ToList();
        }
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage response, CancellationToken ct)
    {
        try { return await response.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }

    // Codici Expo che indicano "token non più valido" → vanno rimossi dal DB.
    // Doc: https://docs.expo.dev/push-notifications/sending-notifications/#push-tickets
    private static bool IsTokenInvalid(string errorCode) =>
        errorCode is "DeviceNotRegistered" or "InvalidCredentials";

    // ---------- DTO interni serializzati su filo HTTP ----------

    private sealed record ExpoMessageDto(
        string To,
        string Title,
        string Body,
        IReadOnlyDictionary<string, string>? Data,
        string Sound,
        string Priority);

    private sealed record ExpoSendResponseDto(
        [property: JsonPropertyName("data")] IReadOnlyList<ExpoTicketDto>? Data);

    private sealed record ExpoTicketDto(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("details")] ExpoErrorDetailsDto? Details);

    private sealed record ExpoErrorDetailsDto(
        [property: JsonPropertyName("error")] string? Error);
}
