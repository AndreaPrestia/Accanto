using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Accanto.Application.Ai;

/// <summary>
/// Compone i prompt per le funzioni AI e applica una redazione best-effort dei dati personali
/// (email, telefoni, codici fiscali italiani) prima di inviarli al provider.
/// La redazione NON è una garanzia di privacy: i prompt restano sotto il controllo dell'utente
/// (provider self-hosted, default off), ma riduce la superficie di esposizione accidentale.
/// </summary>
public sealed class AiPromptBuilder
{
    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Telefoni italiani / internazionali: gestisce +xx prefisso, spazi e trattini.
    // Cattura sequenze 8+ cifre con separatori. Falsi positivi possibili (es. CAP lunghi): accettabile.
    private static readonly Regex PhoneRegex = new(
        @"(?:\+?\d{1,3}[\s\-]?)?(?:\(?\d{2,4}\)?[\s\-]?)?\d{3}[\s\-]?\d{3,4}(?:[\s\-]?\d{0,4})?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Codice fiscale italiano: 6 lettere + 2 cifre + 1 lettera + 2 cifre + 1 lettera + 3 cifre + 1 lettera.
    private static readonly Regex CodiceFiscaleRegex = new(
        @"\b[A-Z]{6}\d{2}[A-Z]\d{2}[A-Z]\d{3}[A-Z]\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Sostituisce email/telefoni/CF con marker [email], [phone], [cf].
    /// </summary>
    public string RedactPii(string input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
        var s = EmailRegex.Replace(input, "[email]");
        s = CodiceFiscaleRegex.Replace(s, "[cf]");
        // Phone regex va eseguita per ultima per non rovinare email/CF.
        // Limita a sequenze di ≥ 8 caratteri numerici totali per ridurre falsi positivi.
        s = PhoneRegex.Replace(s, match =>
        {
            var digits = 0;
            foreach (var c in match.Value) if (char.IsDigit(c)) digits++;
            return digits >= 8 ? "[phone]" : match.Value;
        });
        return s;
    }

    /// <summary>
    /// Restituisce un codice lingua a 2 caratteri (it/en) da un Accept-Language o cultura.
    /// Default "it".
    /// </summary>
    public string ResolveLanguage(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage)) return "it";
        var first = acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .FirstOrDefault()?.Split(';')[0].Trim();
        if (string.IsNullOrEmpty(first)) return "it";
        try
        {
            var culture = CultureInfo.GetCultureInfo(first);
            var two = culture.TwoLetterISOLanguageName;
            return string.Equals(two, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "it";
        }
        catch (CultureNotFoundException)
        {
            return "it";
        }
    }

    /// <summary>
    /// Costruisce il prompt di sistema con la lingua e una breve descrizione del ruolo.
    /// </summary>
    public string BuildSystemPrompt(string language, string role)
    {
        if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
        {
            return $"You are a careful, empathetic assistant for family caregivers. {role} " +
                   "Reply concisely in English. Never provide medical, legal, or financial advice. " +
                   "If information is missing, say so.";
        }
        return $"Sei un assistente attento ed empatico per familiari caregiver. {role} " +
               "Rispondi in italiano in modo conciso. Non fornire pareri medici, legali o finanziari. " +
               "Se mancano informazioni, dichiaralo.";
    }

    /// <summary>
    /// Concatena la sezione contesto + istruzione utente in un prompt finale, redigendo PII dal contesto.
    /// </summary>
    public string BuildUserPrompt(string instruction, string context)
    {
        var sb = new StringBuilder();
        sb.AppendLine(instruction);
        if (!string.IsNullOrWhiteSpace(context))
        {
            sb.AppendLine();
            sb.AppendLine("--- Contesto ---");
            sb.AppendLine(RedactPii(context));
        }
        return sb.ToString();
    }

    /// <summary>Disclaimer standard sempre allegato alle risposte AI.</summary>
    public string GetDisclaimer(string language)
    {
        return string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            ? "AI-generated text. Not medical, legal, or financial advice. Verify with a professional."
            : "Testo generato da AI. Non sostituisce un parere medico, legale o professionale. Verifica sempre con un esperto.";
    }
}
