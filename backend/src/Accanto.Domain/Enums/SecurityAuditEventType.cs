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
    PasswordResetRequested,
    PasswordResetCompleted,
    SessionRevoked,
    AllSessionsRevoked,
    AccountErased,
    AiCall
}
