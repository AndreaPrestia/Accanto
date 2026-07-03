using Accanto.Application.SharedUpdates;
using FluentAssertions;

namespace Accanto.Tests;

public class SharedUpdateTemplateProviderTests
{
    private readonly StaticSharedUpdateTemplateProvider _provider = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("*")]
    [InlineData("xx-XX")] // cultura inesistente
    [InlineData("fr-FR")] // lingua non supportata
    public void Defaults_to_italian_when_no_supported_language(string? header)
    {
        var list = _provider.GetTemplates(header);

        list.Should().NotBeEmpty();
        list[0].Title.Should().Be("Giornata complicata");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("en-US,en;q=0.9")]
    public void Returns_english_when_english_is_preferred(string header)
    {
        var list = _provider.GetTemplates(header);

        list.Should().NotBeEmpty();
        list[0].Title.Should().Be("A tough day");
    }

    [Theory]
    [InlineData("es")]
    [InlineData("es-ES")]
    [InlineData("es-MX,es;q=0.8")]
    public void Returns_spanish_when_spanish_is_preferred(string header)
    {
        var list = _provider.GetTemplates(header);

        list.Should().NotBeEmpty();
        list[0].Title.Should().Be("Un día complicado");
    }

    [Fact]
    public void Picks_highest_q_among_supported_languages()
    {
        // EN q=0.9, ES q=1.0 → vince spagnolo, anche se EN viene prima nell'header.
        var list = _provider.GetTemplates("en;q=0.9,es;q=1.0");

        list[0].Title.Should().Be("Un día complicado");
    }

    [Fact]
    public void Skips_unsupported_languages_and_picks_next_supported()
    {
        // Francese non supportato → ricade su inglese.
        var list = _provider.GetTemplates("fr-FR,en;q=0.5");

        list[0].Title.Should().Be("A tough day");
    }

    [Fact]
    public void All_languages_return_same_number_of_templates()
    {
        var it = _provider.GetTemplates("it");
        var en = _provider.GetTemplates("en");
        var es = _provider.GetTemplates("es");

        it.Count.Should().Be(en.Count).And.Be(es.Count);
        it.Count.Should().Be(7, "abbiamo 3 template per momenti difficili + 4 template positivi");
    }

    [Fact]
    public void Italian_templates_include_positive_content()
    {
        var list = _provider.GetTemplates("it");

        var titles = list.Select(t => t.Title).ToList();
        titles.Should().Contain("Un piccolo miglioramento");
        titles.Should().Contain("Una giornata serena");
        titles.Should().Contain("Grazie per la vicinanza");
        titles.Should().Contain("Un piccolo traguardo");
    }
}
