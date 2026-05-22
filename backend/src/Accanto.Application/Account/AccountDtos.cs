namespace Accanto.Application.Account;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record DeleteAccountRequest(string CurrentPassword);
