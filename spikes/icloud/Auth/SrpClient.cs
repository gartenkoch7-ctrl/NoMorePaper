using System.Numerics;
using System.Security.Cryptography;
using ICloudSpike.Http;

namespace ICloudSpike.Auth;

/// <summary>Client side of one SRP-6a exchange. One instance per sign-in attempt.</summary>
internal sealed class SrpClient
{
    private readonly SrpGroup _group;
    private readonly HashAlgorithmName _algorithm;
    private readonly BigInteger _privateKey;
    private readonly BigInteger _publicKey;

    public SrpClient(SrpGroup group, HashAlgorithmName algorithm, ReadOnlySpan<byte> privateKey)
    {
        _group = group;
        _algorithm = algorithm;
        _privateKey = SrpMath.ToInteger(privateKey);
        _publicKey = BigInteger.ModPow(group.G, _privateKey, group.N);
    }

    /// <summary>A = g^a mod N in its shortest big-endian form.</summary>
    public byte[] PublicKeyBytes => SrpMath.ToMinimalBytes(_publicKey);

    /// <param name="proofUsername">Account name that goes into M1.</param>
    /// <param name="exponentUsername">Username inside x. Empty for Apple, the real one for RFC 5054.</param>
    /// <param name="password">Password bytes; for Apple the PBKDF2-derived key.</param>
    /// <param name="salt">Salt from the server.</param>
    /// <param name="serverPublicKey">B from the server, big-endian.</param>
    public SrpProof CreateProof(
        string proofUsername, string exponentUsername, byte[] password, byte[] salt, byte[] serverPublicKey)
    {
        var serverPublic = SrpMath.ToInteger(serverPublicKey);

        // SRP-6a safety checks: a server sending B = 0 (mod N) or forcing u = 0 would make the
        // session key independent of the password.
        if ((serverPublic % _group.N).IsZero)
        {
            throw new ICloudException(ICloudFailure.ProtocolChanged, "SRP: server sent an invalid public value.");
        }

        var scrambler = SrpMath.ComputeScrambler(_group, _algorithm, _publicKey, serverPublic);
        if (scrambler.IsZero)
        {
            throw new ICloudException(ICloudFailure.ProtocolChanged, "SRP: scrambling parameter is zero.");
        }

        var multiplier = SrpMath.ComputeMultiplier(_group, _algorithm);
        var exponent = SrpMath.ComputePrivateExponent(_algorithm, salt, exponentUsername, password);
        var secret = SrpMath.ComputeClientSecret(_group, multiplier, exponent, _privateKey, scrambler, serverPublic);
        var sessionKey = SrpMath.ComputeSessionKey(_algorithm, secret);
        var clientProof = SrpMath.ComputeClientProof(
            _group, _algorithm, proofUsername, salt, _publicKey, serverPublic, sessionKey);
        var serverProof = SrpMath.ComputeServerProof(_algorithm, _publicKey, clientProof, sessionKey);
        return new SrpProof(clientProof, serverProof, secret);
    }
}
