namespace NoMorePaper.Infrastructure.Composition;

/// <summary>Whether the key ring is encrypted, and with which certificate (SHA-1 thumbprint, not a secret).</summary>
internal sealed record KeyRingProtection(string? Thumbprint);
