using System.Globalization;
using System.Linq;

namespace Accanto.Application.SharedUpdates;

public class StaticSharedUpdateTemplateProvider : ISharedUpdateTemplateProvider
{
    // I template coprono entrambi i versanti dell'esperienza di caregiving:
    // 1-3) momenti difficili (riformulati con tono mite, non cupo);
    // 4-7) miglioramenti, giornate serene, gratitudine, piccoli traguardi.
    // L'obiettivo è non spingere chi scrive solo verso narrazioni negative.
    private static readonly IReadOnlyList<SharedUpdateTemplateDto> ItalianTemplates = new[]
    {
        new SharedUpdateTemplateDto(
            "Giornata complicata",
            "Oggi è una giornata complicata. Ci stiamo concentrando sul suo benessere e seguiamo le indicazioni dei medici. Vi aggiorneremo appena ci saranno novità."
        ),
        new SharedUpdateTemplateDto(
            "Meno telefonate, grazie",
            "In questo momento preferiamo evitare troppe telefonate, ma leggiamo i vostri messaggi. Grazie per la vicinanza."
        ),
        new SharedUpdateTemplateDto(
            "Dopo aver parlato con i medici",
            "Oggi abbiamo parlato con i medici. La situazione è articolata e stiamo affrontando una cosa alla volta."
        ),
        new SharedUpdateTemplateDto(
            "Un piccolo miglioramento",
            "Oggi c'è stato un piccolo miglioramento. Niente di definitivo, ma una boccata d'aria. Volevo condividerlo con voi."
        ),
        new SharedUpdateTemplateDto(
            "Una giornata serena",
            "Oggi è andata bene. Abbiamo passato del tempo insieme con calma, ed è stato un dono. Grazie per essere vicini."
        ),
        new SharedUpdateTemplateDto(
            "Grazie per la vicinanza",
            "Volevo solo dirvi grazie. I vostri messaggi, anche brevi, ci stanno facendo sentire meno soli."
        ),
        new SharedUpdateTemplateDto(
            "Un piccolo traguardo",
            "Oggi un piccolo traguardo: <descrivilo qui>. Sembra poco, ma per noi conta molto."
        )
    };

    private static readonly IReadOnlyList<SharedUpdateTemplateDto> EnglishTemplates = new[]
    {
        new SharedUpdateTemplateDto(
            "A tough day",
            "Today is a tough day. We're focusing on their comfort and following what the doctors suggest. We'll let you know as soon as there's news."
        ),
        new SharedUpdateTemplateDto(
            "Fewer phone calls, please",
            "Right now we'd rather keep phone calls to a minimum, but we do read your messages. Thank you for being close."
        ),
        new SharedUpdateTemplateDto(
            "After talking with the doctors",
            "We spoke with the doctors today. The picture is complex and we're taking one thing at a time."
        ),
        new SharedUpdateTemplateDto(
            "A small improvement",
            "Today there was a small improvement. Nothing definitive, but a breath of relief. I wanted to share it with you."
        ),
        new SharedUpdateTemplateDto(
            "A calm day",
            "Today went well. We spent some quiet time together, and it felt like a gift. Thank you for being close."
        ),
        new SharedUpdateTemplateDto(
            "Thank you for being close",
            "I just wanted to say thank you. Your messages, even the short ones, are making us feel less alone."
        ),
        new SharedUpdateTemplateDto(
            "A small milestone",
            "A small milestone today: <describe it here>. It may seem little, but it means a lot to us."
        )
    };

    private static readonly IReadOnlyList<SharedUpdateTemplateDto> SpanishTemplates = new[]
    {
        new SharedUpdateTemplateDto(
            "Un día complicado",
            "Hoy es un día complicado. Nos estamos centrando en su bienestar y seguimos las indicaciones de los médicos. Os avisaremos en cuanto haya novedades."
        ),
        new SharedUpdateTemplateDto(
            "Menos llamadas, gracias",
            "En este momento preferimos evitar muchas llamadas, pero leemos vuestros mensajes. Gracias por la cercanía."
        ),
        new SharedUpdateTemplateDto(
            "Después de hablar con los médicos",
            "Hoy hemos hablado con los médicos. La situación es compleja y estamos afrontando una cosa a la vez."
        ),
        new SharedUpdateTemplateDto(
            "Una pequeña mejoría",
            "Hoy ha habido una pequeña mejoría. Nada definitivo, pero un respiro. Quería compartirlo con vosotros."
        ),
        new SharedUpdateTemplateDto(
            "Un día tranquilo",
            "Hoy ha ido bien. Hemos pasado un rato con calma juntos, y ha sido un regalo. Gracias por estar cerca."
        ),
        new SharedUpdateTemplateDto(
            "Gracias por la cercanía",
            "Solo quería daros las gracias. Vuestros mensajes, aunque sean breves, nos hacen sentir menos solos."
        ),
        new SharedUpdateTemplateDto(
            "Un pequeño logro",
            "Hoy un pequeño logro: <descríbelo aquí>. Puede parecer poco, pero para nosotros significa mucho."
        )
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<SharedUpdateTemplateDto>> ByLanguage =
        new Dictionary<string, IReadOnlyList<SharedUpdateTemplateDto>>(StringComparer.OrdinalIgnoreCase)
        {
            ["it"] = ItalianTemplates,
            ["en"] = EnglishTemplates,
            ["es"] = SpanishTemplates
        };

    public IReadOnlyList<SharedUpdateTemplateDto> GetTemplates(string? acceptLanguage)
    {
        var lang = ResolveLanguage(acceptLanguage);
        return ByLanguage.TryGetValue(lang, out var list) ? list : ItalianTemplates;
    }

    /// <summary>
    /// Risolve un header Accept-Language in uno tra "it"/"en"/"es". Sceglie la lingua
    /// con priorità (q) più alta tra quelle supportate. Fallback a "it".
    /// Resolver locale per non contaminare la politica italo-tollerante usata da
    /// <c>AiPromptBuilder.ResolveLanguage</c>: per i testi statici dei template è più
    /// naturale rispettare la preferenza esplicita dell'utente.
    /// </summary>
    internal static string ResolveLanguage(string? acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage)) return "it";

        string? bestLang = null;
        double bestQ = double.NegativeInfinity;

        foreach (var parts in acceptLanguage
                     .Split(',', StringSplitOptions.RemoveEmptyEntries)
                     .Select(raw => raw.Split(';')))
        {
            var tag = parts[0].Trim();
            if (string.IsNullOrEmpty(tag) || tag == "*") continue;

            string two;
            try
            {
                two = CultureInfo.GetCultureInfo(tag).TwoLetterISOLanguageName;
            }
            catch (CultureNotFoundException)
            {
                continue;
            }

            if (!ByLanguage.ContainsKey(two)) continue;

            var q = 1.0;
            for (var i = 1; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (p.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(p.AsSpan(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    q = parsed;
                }
            }

            if (q > bestQ)
            {
                bestQ = q;
                bestLang = two.ToLowerInvariant();
            }
        }

        return bestLang ?? "it";
    }
}
