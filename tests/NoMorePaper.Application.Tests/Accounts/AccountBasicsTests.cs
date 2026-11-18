using NoMorePaper.Application.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>Input checks, hashing and secret generation.</summary>
public sealed class AccountBasicsTests
{
    [Fact]
    public void Input_IsTrimmed_AndThePasswordPassedOnUnchanged()
    {
        var result = AccountInput.Check("  anna ", " Anna M. ", " pass word ");

        Assert.Equal(new NewAccount("anna", "Anna M.", " pass word "), result.Value);
    }

    [Theory]
    [InlineData(null, "Anna", AccountProblem.UserNameInvalid)]
    [InlineData("   ", "Anna", AccountProblem.UserNameInvalid)]
    [InlineData("anna", "", AccountProblem.DisplayNameInvalid)]
    [InlineData("anna", "An\u0007na", AccountProblem.DisplayNameInvalid)]
    public void Input_RefusesEmptyNames_AndControlCharacters(string? userName, string displayName, AccountProblem expected)
    {
        var result = AccountInput.Check(userName, displayName, "x");

        Assert.Equal(AccountStatus.Invalid, result.Status);
        Assert.Equal([expected], result.Problems);
    }

    [Fact]
    public void Input_RefusesOverlongNames()
    {
        var result = AccountInput.Check(new string('a', AccountRules.MaxUserNameLength + 1), new string('b', AccountRules.MaxDisplayNameLength + 1), "x");

        Assert.Equal([AccountProblem.UserNameInvalid, AccountProblem.DisplayNameInvalid], result.Problems);
    }

    [Fact]
    public void Hash_IsSha256Hex_AndComparedOnlyWhenBothExist()
    {
        var hash = SecretHash.Of("abc");

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
        Assert.True(SecretHash.AreEqual(hash, SecretHash.Of("abc")));
        Assert.False(SecretHash.AreEqual(hash, SecretHash.Of("abd")));
        Assert.False(SecretHash.AreEqual(null, hash));
        Assert.False(SecretHash.AreEqual(hash, "short"));
    }

    [Theory]
    [InlineData("abcde-fghij-klmno-pqrst", "ABCDEFGHIJKLMNOPQRST")]
    [InlineData(" ABCDE FGHIJ\tKLMNO PQRST ", "ABCDEFGHIJKLMNOPQRST")]
    [InlineData(null, "")]
    public void EmergencyCode_IgnoresCaseSpacesAndDashes(string? typed, string expected) =>
        Assert.Equal(expected, SecretHash.NormalizeEmergencyCode(typed));

    [Fact]
    public void Secrets_AreRandom_UrlSafeTokens_AndBase32Codes()
    {
        var secrets = new RandomSecretGenerator();

        var tokens = Enumerable.Range(0, 20).Select(_ => secrets.NewToken()).ToList();
        var codes = Enumerable.Range(0, 20).Select(_ => secrets.NewEmergencyCode()).ToList();

        Assert.All(tokens, token => Assert.Matches("^[A-Za-z0-9_-]{43}$", token));
        Assert.All(codes, code => Assert.Matches("^[A-Z2-7]{20}$", code));
        Assert.Equal(20, tokens.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(20, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Expiry_IsCutToWholeSeconds_InUtc()
    {
        var now = new DateTimeOffset(2026, 11, 18, 13, 0, 0, 999, TimeSpan.FromHours(1)).AddTicks(5);

        Assert.Equal(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero), AccountRules.Expiry(now, AccountRules.InvitationLifetime));
    }

    [Fact]
    public void Results_CarryTheirValue_OrTheirFailure()
    {
        Assert.Equal(5, AccountResult.Success(5).Value);
        Assert.Equal(AccountStatus.NotFound, AccountResult.Fail<int>(AccountStatus.NotFound).Cast<string>().Status);
        Assert.Equal([AccountProblem.PasswordTooShort], AccountResult.Invalid<int>(AccountProblem.PasswordTooShort).Cast<string>().Problems);
        Assert.Throws<ArgumentOutOfRangeException>(() => AccountResult.Fail(AccountStatus.Succeeded));
        Assert.Throws<ArgumentException>(() => AccountResult.Invalid());
        Assert.Throws<InvalidOperationException>(() => AccountResult.Success(1).Cast<string>());
    }
}
