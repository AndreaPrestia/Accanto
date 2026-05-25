using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Accanto.Application.Common.Exceptions;
using Accanto.Application.Common.Persistence;
using Accanto.Application.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Accanto.Application.Auth.TwoFactor;

public class TwoFactorService : ITwoFactorService
{
    private readonly IAccantoDbContext _db;
    private readonly IFieldProtector _protector;
    private readonly IPasswordHasher _hasher;
    private readonly IRefreshTokenService _refresh;
    private readonly TwoFactorOptions _opt;

    public TwoFactorService(
        IAccantoDbContext db,
        IFieldProtector protector,
        IPasswordHasher hasher,
        IRefreshTokenService refresh,
        IOptions<TwoFactorOptions> opt)
    {
        _db = db;
        _protector = protector;
        _hasher = hasher;
        _refresh = refresh;
        _opt = opt.Value;
    }

    public async Task<TwoFactorStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");
        var remaining = LoadRecoveryHashes(user.TwoFactorRecoveryCodesJson).Count;
        return new TwoFactorStatusDto(user.TwoFactorEnabled, remaining);
    }

    public async Task<TwoFactorSetupResponse> SetupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (user.TwoFactorEnabled)
            throw new ConflictException("2FA già attiva. Disattivala prima di rigenerare il segreto.");

        var secretBytes = KeyGeneration.GenerateRandomKey(20); // 160 bit, standard TOTP.
        var secretBase32 = Base32Encoding.ToString(secretBytes);

        user.TwoFactorPendingSecret = _protector.Encrypt(secretBase32);
        await _db.SaveChangesAsync(cancellationToken);

        var issuer = Uri.EscapeDataString(_opt.Issuer);
        var label = Uri.EscapeDataString($"{_opt.Issuer}:{user.Email}");
        var uri = $"otpauth://totp/{label}?secret={secretBase32}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
        return new TwoFactorSetupResponse(secretBase32, uri);
    }

    public async Task<EnableTwoFactorResponse> EnableAsync(Guid userId, EnableTwoFactorRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new AppValidationException("Codice mancante.",
                new Dictionary<string, string[]> { ["Code"] = new[] { "Inserisci il codice generato dall'app." } });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (user.TwoFactorEnabled)
            throw new ConflictException("2FA già attiva.");

        if (string.IsNullOrEmpty(user.TwoFactorPendingSecret))
            throw new ConflictException("Avvia prima la procedura di configurazione (setup).");

        var secret = _protector.Decrypt(user.TwoFactorPendingSecret);
        if (!VerifyCode(secret, request.Code))
            throw new ForbiddenException("Codice non valido.");

        var (codes, hashes) = GenerateRecoveryCodes(_opt.RecoveryCodeCount);
        user.TwoFactorSecret = user.TwoFactorPendingSecret;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorEnabled = true;
        user.TwoFactorRecoveryCodesJson = JsonSerializer.Serialize(hashes);
        await _db.SaveChangesAsync(cancellationToken);

        return new EnableTwoFactorResponse(codes);
    }

    public async Task DisableAsync(Guid userId, DisableTwoFactorRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!_hasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
            throw new ForbiddenException("Password non corretta.");

        if (!user.TwoFactorEnabled) return;

        // Servono codice TOTP O recovery code per evitare disattivazioni con sola password rubata.
        var verified = false;
        if (!string.IsNullOrWhiteSpace(request.Code) && !string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var secret = _protector.Decrypt(user.TwoFactorSecret);
            verified = VerifyCode(secret, request.Code);
        }
        if (!verified && !string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            verified = TryConsumeRecoveryCode(user, request.RecoveryCode);
        }
        if (!verified)
            throw new ForbiddenException("Codice 2FA non valido.");

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorRecoveryCodesJson = null;
        await _db.SaveChangesAsync(cancellationToken);

        // Le sessioni attive restano, ma per sicurezza notifichiamo: per ora niente revoca esplicita.
    }

    public async Task<EnableTwoFactorResponse> RegenerateRecoveryCodesAsync(Guid userId, RegenerateRecoveryCodesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Utente non trovato.");

        if (!_hasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
            throw new ForbiddenException("Password non corretta.");

        if (!user.TwoFactorEnabled)
            throw new ConflictException("2FA non attiva.");

        var (codes, hashes) = GenerateRecoveryCodes(_opt.RecoveryCodeCount);
        user.TwoFactorRecoveryCodesJson = JsonSerializer.Serialize(hashes);
        await _db.SaveChangesAsync(cancellationToken);
        return new EnableTwoFactorResponse(codes);
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var trimmed = code.Replace(" ", string.Empty).Trim();
        try
        {
            var bytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(bytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
            return totp.VerifyTotp(trimmed, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ConsumeRecoveryCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return false;
        if (!TryConsumeRecoveryCode(user, code)) return false;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> VerifyUserCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret)) return false;
        var secret = _protector.Decrypt(user.TwoFactorSecret);
        return VerifyCode(secret, code);
    }

    private static bool TryConsumeRecoveryCode(Domain.Entities.User user, string code)
    {
        var normalized = NormalizeRecoveryCode(code);
        if (normalized.Length == 0) return false;
        var hash = HashRecoveryCode(normalized);
        var hashes = LoadRecoveryHashes(user.TwoFactorRecoveryCodesJson);
        if (!hashes.Remove(hash)) return false;
        user.TwoFactorRecoveryCodesJson = hashes.Count == 0 ? "[]" : JsonSerializer.Serialize(hashes);
        return true;
    }

    private static (IReadOnlyList<string> codes, List<string> hashes) GenerateRecoveryCodes(int count)
    {
        var codes = new List<string>(count);
        var hashes = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            // 10 caratteri base32 → formato XXXXX-XXXXX
            var raw = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(8))
                .TrimEnd('=')
                .Substring(0, 10);
            var pretty = $"{raw.Substring(0, 5)}-{raw.Substring(5, 5)}";
            codes.Add(pretty);
            hashes.Add(HashRecoveryCode(NormalizeRecoveryCode(pretty)));
        }
        return (codes, hashes);
    }

    private static string NormalizeRecoveryCode(string code)
        => new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string HashRecoveryCode(string normalized)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static List<string> LoadRecoveryHashes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new List<string>(); }
    }
}
