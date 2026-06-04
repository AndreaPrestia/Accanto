using System.Text;

namespace Accanto.Application.Documents;

/// <summary>
/// Verifica che un upload corrisponda al content-type dichiarato. Difese
/// stratificate per neutralizzare upload polyglot, content-type spoofing
/// e file XSS-on-render:
///
/// 1. Coerenza estensione/content-type (es. .pdf NON puo' essere image/png).
/// 2. Rifiuto universale di header eseguibili/archivi noti (MZ, ELF, Mach-O,
///    PK ZIP, gzip, RAR, 7z) indipendentemente dal content-type dichiarato.
/// 3. Validazione strutturale per formato:
///    - PDF: header "%PDF-" + versione 1.x/2.x + presenza di "%%EOF" in coda
///    - PNG: signature 8 byte + primo chunk obbligatorio "IHDR"
///    - JPEG: SOI FFD8FF + marker EOI FFD9 in coda
///    - text/plain: validazione UTF-8 completa (sequenze multibyte ben formate,
///      niente NUL, niente surrogate, niente overlong, niente DEL/control esotici)
///
/// Tutte le firme provengono dalle specifiche pubbliche dei formati.
/// </summary>
public static class FileSignatureValidator
{
    // Quanti byte basta ispezionare per coprire le firme dell'HEAD dei formati ammessi.
    // Mantenuto per compat backward con l'API IsValid (head-only).
    public const int InspectBytes = 16;

    private static readonly Dictionary<string, string[]> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"]  = new[] { ".pdf" },
            ["image/jpeg"]       = new[] { ".jpg", ".jpeg" },
            ["image/png"]        = new[] { ".png" },
            ["text/plain"]       = new[] { ".txt", ".log", ".md" },
        };

    /// <summary>
    /// Validazione "head-only" (back-compat). Preferire <see cref="Validate"/>
    /// che esegue anche controlli di coda / strutturali e blocca magics
    /// pericolosi universalmente.
    /// </summary>
    public static bool IsValid(ReadOnlySpan<byte> head, string contentType)
    {
        return contentType switch
        {
            "application/pdf" => StartsWith(head, "%PDF-"u8),
            "image/jpeg"      => head.Length >= 3
                              && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
            "image/png"       => StartsWith(head, PngSignature),
            "text/plain"      => IsPlainTextHead(head),
            _                 => false,
        };
    }

    /// <summary>
    /// Validazione completa: estensione, magics "noti pericolosi",
    /// struttura del formato dichiarato. Restituisce <c>null</c> se OK,
    /// altrimenti la motivazione del rifiuto (italiano, sicura da
    /// mostrare all'utente — niente leak di dettagli interni).
    /// </summary>
    public static string? Validate(ReadOnlySpan<byte> content, string contentType, string? fileName)
    {
        // 1) Coerenza estensione: blocca il classico "evil.pdf" con dentro un PNG.
        //    Il sniffing dei byte basta gia' a rifiutare lo spoof, ma una
        //    estensione coerente impedisce confusione client-side al download.
        if (!string.IsNullOrEmpty(fileName))
        {
            var ext = System.IO.Path.GetExtension(fileName);
            if (!AllowedExtensions.TryGetValue(contentType, out var allowedExts)
                || !allowedExts.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return "estensione del file non coerente con il tipo dichiarato";
            }
        }

        // 2) Rifiuto universale (defense-in-depth) di magics palesemente
        //    pericolosi, indipendentemente dal content-type dichiarato.
        if (LooksLikeDangerousBinary(content))
        {
            return "contenuto eseguibile o archivio non consentito";
        }

        // 3) Validazione strutturale per formato.
        return contentType switch
        {
            "application/pdf" => ValidatePdf(content),
            "image/jpeg"      => ValidateJpeg(content),
            "image/png"       => ValidatePng(content),
            "text/plain"      => ValidatePlainText(content),
            _                 => "tipo file non supportato",
        };
    }

    // ---------------------------------------------------------------- helpers

    private static readonly byte[] PngSignature =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static bool StartsWith(ReadOnlySpan<byte> head, ReadOnlySpan<byte> prefix)
        => head.Length >= prefix.Length && head[..prefix.Length].SequenceEqual(prefix);

    /// <summary>
    /// Rifiuta header di formati binari mai ammessi su Accanto:
    /// PE/COFF (MZ), ELF, Mach-O, ZIP (PK\x03\x04 — anche docx/xlsx/jar/apk),
    /// gzip (1F 8B), RAR (Rar!), 7z. Difesa "belt-and-braces" che evita di
    /// dipendere solo dallo switch per content-type: se domani aggiungiamo
    /// un nuovo formato all'allowlist, questi rimangono sempre bloccati
    /// salvo intervento esplicito.
    /// </summary>
    private static bool LooksLikeDangerousBinary(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 2)
        {
            // MZ -> PE/COFF (Windows exe/dll).
            if (head[0] == 0x4D && head[1] == 0x5A) return true;
            // gzip.
            if (head[0] == 0x1F && head[1] == 0x8B) return true;
        }
        if (head.Length >= 4)
        {
            // ELF (Linux/BSD eseguibili).
            if (head[0] == 0x7F && head[1] == 0x45 && head[2] == 0x4C && head[3] == 0x46) return true;
            // ZIP / Office Open XML / JAR / APK -> "PK\x03\x04".
            if (head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04) return true;
            // Mach-O magic (FE ED FA CE/CF in big o little endian, fat CA FE BA BE).
            if (head[0] == 0xFE && head[1] == 0xED && head[2] == 0xFA && (head[3] == 0xCE || head[3] == 0xCF))
                return true;
            if (head[3] == 0xFE && head[2] == 0xED && head[1] == 0xFA && (head[0] == 0xCE || head[0] == 0xCF))
                return true;
            if (head[0] == 0xCA && head[1] == 0xFE && head[2] == 0xBA && head[3] == 0xBE)
                return true;
            // RAR ("Rar!").
            if (head[0] == 0x52 && head[1] == 0x61 && head[2] == 0x72 && head[3] == 0x21) return true;
        }
        if (head.Length >= 6)
        {
            // 7z signature "7z\xBC\xAF\x27\x1C".
            if (head[0] == 0x37 && head[1] == 0x7A && head[2] == 0xBC
                && head[3] == 0xAF && head[4] == 0x27 && head[5] == 0x1C) return true;
        }
        return false;
    }

    // ---- PDF -------------------------------------------------------

    private static string? ValidatePdf(ReadOnlySpan<byte> content)
    {
        if (!StartsWith(content, "%PDF-"u8))
            return "il contenuto non e' un PDF valido (header mancante)";

        // Versione: %PDF-X.Y dove X = 1 o 2 e Y = cifra ASCII.
        if (content.Length < 8 || (content[5] != (byte)'1' && content[5] != (byte)'2')
            || content[6] != (byte)'.' || content[7] < (byte)'0' || content[7] > (byte)'9')
        {
            return "versione PDF non supportata";
        }

        // %%EOF deve comparire negli ultimi 1024 byte (tolleranza per
        // commenti / whitespace finali ammessi dalla spec PDF).
        var tailLen = Math.Min(1024, content.Length);
        var tail = content[^tailLen..];
        if (tail.IndexOf("%%EOF"u8) < 0)
            return "PDF troncato o malformato (manca marker %%EOF)";

        return null;
    }

    // ---- PNG -------------------------------------------------------

    private static string? ValidatePng(ReadOnlySpan<byte> content)
    {
        if (!StartsWith(content, PngSignature))
            return "il contenuto non e' un PNG valido (signature mancante)";

        // Primo chunk dopo la signature DEVE essere IHDR (offset 8: length 4 byte,
        // poi 4 byte type a offset 12..16). Vedi RFC 2083 / PNG spec.
        if (content.Length < 16)
            return "PNG troncato";

        if (content[12] != (byte)'I' || content[13] != (byte)'H'
            || content[14] != (byte)'D' || content[15] != (byte)'R')
        {
            return "PNG malformato (primo chunk diverso da IHDR)";
        }

        return null;
    }

    // ---- JPEG ------------------------------------------------------

    private static string? ValidateJpeg(ReadOnlySpan<byte> content)
    {
        if (content.Length < 4 || content[0] != 0xFF || content[1] != 0xD8 || content[2] != 0xFF)
            return "il contenuto non e' un JPEG valido (SOI mancante)";

        // Cerca EOI (FF D9) negli ultimi 16 byte. Molti JPEG hanno trailing
        // padding minimale; 16 byte e' margine ampio senza essere
        // sfruttabile per appending massicci.
        var tailLen = Math.Min(16, content.Length);
        var tail = content[^tailLen..];
        for (var i = 0; i < tail.Length - 1; i++)
        {
            if (tail[i] == 0xFF && tail[i + 1] == 0xD9) return null;
        }
        return "JPEG troncato o malformato (manca marker EOI)";
    }

    // ---- text/plain ------------------------------------------------

    private static string? ValidatePlainText(ReadOnlySpan<byte> content)
    {
        // UTF-8 BOM ammesso.
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            content = content[3..];
        }

        // Decoder UTF-8 strict: throws su sequenze invalide, overlong,
        // surrogate. Decodifica TUTTO il file: text/plain dovrebbe essere
        // piccolo (cap MaxFileSizeBytes a livello service) e non e' un
        // costo significativo.
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            var decoded = utf8.GetString(content);
            foreach (var ch in decoded)
            {
                if (ch == '\u0000') return "il file di testo contiene NUL bytes";
                if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r')
                    return "il file di testo contiene caratteri di controllo non consentiti";
                if (ch == 0x7F) return "il file di testo contiene caratteri di controllo non consentiti";
            }
            return null;
        }
        catch (DecoderFallbackException)
        {
            return "il file dichiarato come text/plain non e' UTF-8 valido";
        }
    }

    // Vecchio helper IsPlainText (head-only). Conservato per IsValid back-compat.
    private static bool IsPlainTextHead(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            head = head[3..];
        }
        foreach (var b in head)
        {
            if (b == 0x09 || b == 0x0A || b == 0x0D) continue;
            if (b < 0x20) return false;
            if (b == 0x7F) return false;
        }
        return true;
    }
}
