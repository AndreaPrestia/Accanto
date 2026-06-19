namespace Accanto.Application.Account;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Richiesta di cancellazione GDPR (right-to-erasure). Richiede
/// password corrente e, se 2FA attivo, anche un codice TOTP o di
/// recupero. Il campo Confirmation deve essere esattamente "ERASE"
/// per evitare cancellazioni accidentali da client malformati.
/// </summary>
public sealed record DeleteAccountRequest(
    string CurrentPassword,
    string? TwoFactorCode = null,
    string? Confirmation = null);

public sealed record UpdateLanguageRequest(string? Language);
