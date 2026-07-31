using System.Net.Http.Headers;
using System.Net.Http.Json;
using Accanto.Admin.Application.Users;

namespace Accanto.Admin.Infrastructure.Internal;

/// <summary>
/// HTTP client service-to-service verso /internal/admin/* della app pubblica.
/// Ogni chiamata allega un token InternalAdmin di breve durata. Ritorna SOLO
/// metadata e inoltra SOLO comandi account: nessun contenuto utente transita.
/// </summary>
public class InternalAppClient : IInternalAppClient
{
    private readonly HttpClient _http;
    private readonly InternalServiceTokenIssuer _tokens;

    public InternalAppClient(HttpClient http, InternalServiceTokenIssuer tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<AdminUserListResponse> ListUsersAsync(string? query, bool? disabled, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var url = $"/internal/admin/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(query)) url += $"&q={Uri.EscapeDataString(query)}";
        if (disabled.HasValue) url += $"&disabled={disabled.Value.ToString().ToLowerInvariant()}";

        using var req = NewRequest(HttpMethod.Get, url);
        var resp = await _http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<AdminUserListResponse>(cancellationToken: cancellationToken)
               ?? new AdminUserListResponse(Array.Empty<AdminUserMetadataDto>(), page, pageSize, 0);
    }

    public async Task<AdminUserMetadataDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var req = NewRequest(HttpMethod.Get, $"/internal/admin/users/{userId}");
        var resp = await _http.SendAsync(req, cancellationToken);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<AdminUserMetadataDto>(cancellationToken: cancellationToken);
    }

    public Task DisableUserAsync(Guid userId, string? reason, CancellationToken cancellationToken = default)
        => PostAsync($"/internal/admin/users/{userId}/disable", new { reason }, cancellationToken);

    public Task EnableUserAsync(Guid userId, string? reason, CancellationToken cancellationToken = default)
        => PostAsync($"/internal/admin/users/{userId}/enable", new { reason }, cancellationToken);

    public Task RevokeUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
        => PostAsync($"/internal/admin/users/{userId}/revoke-sessions", null, cancellationToken);

    public Task StartUserDeletionAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
        => PostAsync($"/internal/admin/users/{userId}/deletion-requests", new { reason }, cancellationToken);

    private async Task PostAsync(string url, object? body, CancellationToken ct)
    {
        using var req = NewRequest(HttpMethod.Post, url);
        req.Content = JsonContent.Create(body ?? new { });
        var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.Issue());
        return req;
    }
}
