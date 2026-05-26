using Accanto.Application.Ai;
using FluentAssertions;

namespace Accanto.Tests;

public class AiPromptBuilderTests
{
    private readonly AiPromptBuilder _b = new();

    [Fact]
    public void Redacts_email_addresses()
    {
        var input = "Contatta il medico a mario.rossi@example.com per appuntamento.";
        _b.RedactPii(input).Should().Contain("[email]").And.NotContain("mario.rossi@example.com");
    }

    [Fact]
    public void Redacts_italian_codice_fiscale()
    {
        var input = "Il CF della mamma è RSSMRA70A01H501Z.";
        _b.RedactPii(input).Should().Contain("[cf]").And.NotContain("RSSMRA70A01H501Z");
    }

    [Fact]
    public void Redacts_phone_numbers_with_8_or_more_digits()
    {
        var input = "Telefono di Anna: +39 333 1234567.";
        _b.RedactPii(input).Should().Contain("[phone]");
    }

    [Fact]
    public void Does_not_redact_short_numbers()
    {
        var input = "Sono passati 7 giorni dall'ultima visita.";
        _b.RedactPii(input).Should().Contain("7 giorni");
    }

    [Fact]
    public void Resolves_italian_by_default_when_header_empty()
    {
        _b.ResolveLanguage(null).Should().Be("it");
        _b.ResolveLanguage("").Should().Be("it");
    }

    [Fact]
    public void Resolves_english_from_accept_language()
    {
        _b.ResolveLanguage("en-US,en;q=0.9").Should().Be("en");
    }

    [Fact]
    public void Resolves_italian_from_it_IT()
    {
        _b.ResolveLanguage("it-IT,it;q=0.9,en;q=0.8").Should().Be("it");
    }

    [Fact]
    public void Falls_back_to_italian_for_unknown_languages()
    {
        _b.ResolveLanguage("xx-YY").Should().Be("it");
    }

    [Fact]
    public void Prefers_italian_when_present_at_any_priority()
    {
        // Anche se l'inglese ha priorità maggiore, se "it" è in lista usiamo italiano.
        _b.ResolveLanguage("en-US,en;q=0.9,it;q=0.5").Should().Be("it");
    }

    [Fact]
    public void Returns_english_only_when_italian_absent()
    {
        _b.ResolveLanguage("fr-FR,fr;q=0.9,en;q=0.5").Should().Be("en");
    }

    [Fact]
    public void BuildSystemPrompt_includes_role_and_language()
    {
        var sys = _b.BuildSystemPrompt("it", "Aiuti il caregiver a riassumere.");
        sys.Should().Contain("italiano").And.Contain("Aiuti il caregiver");
    }

    [Fact]
    public void Disclaimer_is_non_empty_in_both_languages()
    {
        _b.GetDisclaimer("it").Should().NotBeNullOrWhiteSpace();
        _b.GetDisclaimer("en").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildUserPrompt_redacts_context()
    {
        var prompt = _b.BuildUserPrompt("Riassumi:", "Email: anna@example.com tel +39 333 1234567.");
        prompt.Should().Contain("[email]").And.Contain("[phone]");
    }
}
