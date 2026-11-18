using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>Predictable secrets, so tests can check what reached the store.</summary>
internal sealed class FixedSecrets : ISecretGenerator
{
    public const string Token = "fixed-token";

    private int _codes;

    public string NewToken() => Token;

    public string NewEmergencyCode() => $"CODE{++_codes:D16}";
}
