namespace Accanto.Domain.Enums;

public enum SecurityAuditEventType
{
    AccountRegistered,
    LoginSuccess,
    LoginFailed,
    LoginLocked,
    TwoFactorChallengeIssued,
    TwoFactorSuccess,
    TwoFactorFailed,
    TwoFactorEnabled,
    TwoFactorDisabled,
    RecoveryCodeUsed,
    RecoveryCodesRegenerated,
    PasswordChanged,
    SessionRevoked,
    AllSessionsRevoked
}
