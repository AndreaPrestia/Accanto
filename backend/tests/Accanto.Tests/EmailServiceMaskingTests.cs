using Accanto.Infrastructure.Email;
using FluentAssertions;

namespace Accanto.Tests;

public class EmailServiceMaskingTests
{
    [Theory]
    [InlineData("mario.rossi@example.com", "m***@example.com")]
    [InlineData("a@b.it", "a***@b.it")]
    [InlineData("UPPER@Domain.Com", "U***@Domain.Com")]
    public void MaskEmail_keeps_only_first_char_and_domain(string input, string expected)
    {
        EmailService.MaskEmail(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskEmail_handles_empty_input(string? input)
    {
        EmailService.MaskEmail(input).Should().Be("(vuota)");
    }

    [Theory]
    [InlineData("no-at-sign")]
    [InlineData("@nolocal.com")]
    public void MaskEmail_never_leaks_local_part_when_malformed(string input)
    {
        EmailService.MaskEmail(input).Should().Be("***");
    }

    [Fact]
    public void MaskEmail_does_not_contain_full_local_part()
    {
        var masked = EmailService.MaskEmail("sensitive.name@example.com");
        masked.Should().NotContain("sensitive.name");
        masked.Should().NotContain("ensitive");
    }
}
