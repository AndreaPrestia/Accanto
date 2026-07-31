namespace Accanto.Admin.Infrastructure.Email;

/// <summary>Config SMTP del control plane admin (sezione <c>AdminEmail</c>).</summary>
public class AdminEmailOptions
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseStartTls { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Accanto Admin";
}
