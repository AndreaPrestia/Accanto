namespace Accanto.Application.Auth.TwoFactor;

public sealed record TwoFactorSetupResponse(string Secret, string OtpAuthUri);

public sealed record EnableTwoFactorRequest(string Code);

public sealed record EnableTwoFactorResponse(IReadOnlyList<string> RecoveryCodes);

public sealed record DisableTwoFactorRequest(string Password, string? Code, string? RecoveryCode);

public sealed record RegenerateRecoveryCodesRequest(string Password);

public sealed record TwoFactorStatusDto(bool Enabled, int RemainingRecoveryCodes);
