namespace Accanto.Infrastructure.Email;

public class EmailOptions
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseStartTls { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Accanto";
}
