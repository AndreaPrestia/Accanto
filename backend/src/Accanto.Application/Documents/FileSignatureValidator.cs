namespace Accanto.Application.Documents;

/// <summary>
/// Verifica che i primi byte di uno stream corrispondano alla firma (magic
/// bytes) del content-type dichiarato dal client. Difesa contro upload con
/// header HTTP `Content-Type: image/png` ma contenuto eseguibile o HTML
/// (XSS via "polyglot files").
///
/// Le firme sono prese da specifiche pubbliche dei formati (PDF 1.0-2.0,
/// JFIF/Exif/SOI per JPEG, signature standard PNG). Per `text/plain`
/// non esiste una firma binaria: validiamo che il blocco iniziale sia
/// 7-bit ASCII o UTF-8 ben formato senza byte di controllo "esotici".
/// </summary>
public static class FileSignatureValidator
{
    // Quanti byte basta ispezionare per coprire le firme dei formati ammessi.
    public const int InspectBytes = 16;

    /// <summary>
    /// Restituisce true se <paramref name="head"/> matcha la firma del
    /// content-type dichiarato. <paramref name="head"/> deve contenere
    /// almeno <see cref="InspectBytes"/> byte (o l'intero file se piu' corto).
    /// </summary>
    public static bool IsValid(ReadOnlySpan<byte> head, string contentType)
    {
        return contentType switch
        {
            "application/pdf" => StartsWith(head, "%PDF-"u8),
            "image/jpeg"      => head.Length >= 3
                              && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
            "image/png"       => StartsWith(head, new byte[]
                              { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "text/plain"      => IsPlainText(head),
            _                 => false,
        };
    }

    private static bool StartsWith(ReadOnlySpan<byte> head, ReadOnlySpan<byte> prefix)
        => head.Length >= prefix.Length && head[..prefix.Length].SequenceEqual(prefix);

    private static bool IsPlainText(ReadOnlySpan<byte> head)
    {
        // UTF-8 BOM ammesso.
        if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            head = head[3..];
        }
        // Rifiuta byte di controllo non comuni (eccetto TAB/LF/CR). Cosi'
        // un PDF/PNG mascherato come text/plain viene scartato.
        foreach (var b in head)
        {
            if (b == 0x09 || b == 0x0A || b == 0x0D) continue;
            if (b < 0x20) return false;          // NUL, BEL, escape, etc.
            if (b == 0x7F) return false;         // DEL
        }
        return true;
    }
}
