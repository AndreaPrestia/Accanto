namespace Accanto.Infrastructure.Push;

public class PushOptions
{
    public string? VapidPublicKey { get; set; }
    public string? VapidPrivateKey { get; set; }
    public string VapidSubject { get; set; } = "mailto:admin@accanto.local";
}
