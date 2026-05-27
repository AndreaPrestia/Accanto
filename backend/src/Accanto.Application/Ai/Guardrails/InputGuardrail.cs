using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Accanto.Application.Ai.Guardrails;

/// <summary>
/// Esito della validazione input pre-LLM.
/// Non emette messaggi specifici verso l'utente: il chiamante mappa il verdict
/// in un messaggio neutro o in una risposta canonica (safety / fuori-scopo).
/// </summary>
public enum InputGuardrailDecision
{
    Allow,
    BlockInjection,
    OffTopic,
    SelfHarm
}

public sealed class InputGuardrailResult
{
    public InputGuardrailDecision Decision { get; init; }

    /// <summary>Etichetta breve della prima regola triggerata (solo per audit interno, MAI verso l'utente).</summary>
    public string? RuleHit { get; init; }
}

/// <summary>
/// Layer A — validazione deterministica dell'input prima di chiamare il modello.
/// Esegue:
///   1) match di pattern di prompt-injection → BlockInjection (risposta neutra, 422 lato API)
///   2) match di parole-chiave fuori-scopo → OffTopic (risposta canonica, 200)
///   3) match di pattern di autolesionismo → SelfHarm (risposta di supporto con numeri d'aiuto, 200)
/// Le tre liste sono interamente configurabili via <see cref="AiOptions"/>.
/// </summary>
public sealed class InputGuardrail
{
    private readonly AiOptions _options;
    private readonly Regex[] _injection;
    private readonly Regex[] _offTopic;
    private readonly Regex[] _selfHarm;

    public InputGuardrail(IOptions<AiOptions> options)
    {
        _options = options.Value;
        _injection = Compile(_options.InjectionPatterns);
        _offTopic = Compile(_options.OffTopicPatterns);
        _selfHarm = Compile(_options.SelfHarmPatterns);
    }

    /// <summary>
    /// Valuta uno o più testi forniti dall'utente. L'ordine di precedenza è:
    /// SelfHarm > BlockInjection > OffTopic. SelfHarm ha la priorità perché
    /// rappresenta un caso di supporto che non deve essere mascherato da un blocco.
    /// </summary>
    public InputGuardrailResult Inspect(params string?[] inputs)
    {
        var combined = string.Join("\n", inputs.Where(s => !string.IsNullOrWhiteSpace(s))!);
        if (string.IsNullOrWhiteSpace(combined))
            return new InputGuardrailResult { Decision = InputGuardrailDecision.Allow };

        var harm = FirstMatch(_selfHarm, combined);
        if (harm is not null)
            return new InputGuardrailResult { Decision = InputGuardrailDecision.SelfHarm, RuleHit = harm };

        var inj = FirstMatch(_injection, combined);
        if (inj is not null)
            return new InputGuardrailResult { Decision = InputGuardrailDecision.BlockInjection, RuleHit = inj };

        var off = FirstMatch(_offTopic, combined);
        if (off is not null)
            return new InputGuardrailResult { Decision = InputGuardrailDecision.OffTopic, RuleHit = off };

        return new InputGuardrailResult { Decision = InputGuardrailDecision.Allow };
    }

    private static Regex[] Compile(IEnumerable<string> patterns)
        => patterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p =>
            {
                try
                {
                    return new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                }
                catch (ArgumentException)
                {
                    // pattern invalido in configurazione → ignorato silenziosamente per non rompere l'avvio
                    return null!;
                }
            })
            .Where(r => r is not null)
            .ToArray();

    private static string? FirstMatch(Regex[] regexes, string text)
    {
        foreach (var r in regexes)
        {
            try
            {
                if (r.IsMatch(text)) return r.ToString();
            }
            catch (RegexMatchTimeoutException)
            {
                // pattern troppo costoso: lo trattiamo come miss per safety
            }
        }
        return null;
    }
}
