namespace ICloudSpike.Session;

/// <summary>Encrypts the session file at rest (AGENTS.md 2.4: credentials are never stored in clear).</summary>
internal interface ISecretProtector
{
    byte[] Protect(byte[] clearData);

    byte[] Unprotect(byte[] protectedData);
}
