using ICloudSpike.Session;

namespace ICloudSpike.SelfTest;

/// <summary>Stand-in for DPAPI on systems without it. Not encryption; self-test only.</summary>
internal sealed class ReversingProtector : ISecretProtector
{
    public byte[] Protect(byte[] clearData) => [.. clearData.Reverse()];

    public byte[] Unprotect(byte[] protectedData) => [.. protectedData.Reverse()];
}
