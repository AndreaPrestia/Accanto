using Accanto.Infrastructure.Security;
using FluentAssertions;

namespace Accanto.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_then_Verify_succeeds_with_correct_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("SuperSecreta123");
        hasher.Verify("SuperSecreta123", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_fails_with_wrong_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("SuperSecreta123");
        hasher.Verify("Sbagliata", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_produces_different_outputs_for_same_password()
    {
        var hasher = new PasswordHasher();
        hasher.Hash("password1234").Should().NotBe(hasher.Hash("password1234"));
    }

    [Fact]
    public void Verify_returns_false_for_malformed_hash()
    {
        var hasher = new PasswordHasher();
        hasher.Verify("anything", "not-a-real-hash").Should().BeFalse();
    }
}
