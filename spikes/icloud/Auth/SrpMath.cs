using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace ICloudSpike.Auth;

/// <summary>
/// SRP-6a building blocks (RFC 2945 / RFC 5054). Pure functions so they can be checked against
/// published test vectors without any network access.
/// </summary>
/// <remarks>
/// Byte encodings follow what the open-source iCloud clients send successfully: k and u use values
/// padded to the length of N (RFC 5054); the proofs M1/M2 and the session key use the shortest
/// big-endian form of A, B and S. Salt and inner hash are used as raw bytes (RFC 2945).
/// </remarks>
internal static class SrpMath
{
    public static BigInteger ToInteger(ReadOnlySpan<byte> bigEndian) =>
        new(bigEndian, isUnsigned: true, isBigEndian: true);

    public static byte[] ToMinimalBytes(BigInteger value) =>
        value.ToByteArray(isUnsigned: true, isBigEndian: true);

    public static byte[] ToPaddedBytes(BigInteger value, int length)
    {
        var minimal = ToMinimalBytes(value);
        if (minimal.Length >= length)
        {
            return minimal;
        }

        var padded = new byte[length];
        minimal.CopyTo(padded, length - minimal.Length);
        return padded;
    }

    public static byte[] Hash(HashAlgorithmName algorithm, params ReadOnlySpan<byte[]> parts)
    {
        using var hash = IncrementalHash.CreateHash(algorithm);
        foreach (var part in parts)
        {
            hash.AppendData(part);
        }

        return hash.GetHashAndReset();
    }

    /// <summary>k = H(N | PAD(g))</summary>
    public static BigInteger ComputeMultiplier(SrpGroup group, HashAlgorithmName algorithm) =>
        ToInteger(Hash(algorithm, ToMinimalBytes(group.N), ToPaddedBytes(group.G, group.ByteLength)));

    /// <summary>u = H(PAD(A) | PAD(B))</summary>
    public static BigInteger ComputeScrambler(
        SrpGroup group, HashAlgorithmName algorithm, BigInteger clientPublic, BigInteger serverPublic) =>
        ToInteger(Hash(
            algorithm,
            ToPaddedBytes(clientPublic, group.ByteLength),
            ToPaddedBytes(serverPublic, group.ByteLength)));

    /// <summary>x = H(salt | H(username ":" password)). Apple leaves the username empty here.</summary>
    public static BigInteger ComputePrivateExponent(
        HashAlgorithmName algorithm, byte[] salt, string username, byte[] password)
    {
        byte[] identity = [.. Encoding.UTF8.GetBytes(username), (byte)':', .. password];
        var inner = Hash(algorithm, identity);
        CryptographicOperations.ZeroMemory(identity);
        return ToInteger(Hash(algorithm, salt, inner));
    }

    /// <summary>v = g^x mod N. Only the server side (self-test) needs it.</summary>
    public static BigInteger ComputeVerifier(SrpGroup group, BigInteger privateExponent) =>
        BigInteger.ModPow(group.G, privateExponent, group.N);

    /// <summary>S = (B - k * g^x) ^ (a + u * x) mod N</summary>
    public static BigInteger ComputeClientSecret(
        SrpGroup group,
        BigInteger multiplier,
        BigInteger privateExponent,
        BigInteger clientPrivate,
        BigInteger scrambler,
        BigInteger serverPublic)
    {
        var verifier = ComputeVerifier(group, privateExponent);

        // BigInteger keeps the sign of the dividend, so the base is lifted into [0, N) first.
        var baseValue = (serverPublic - (multiplier * verifier)) % group.N;
        if (baseValue.Sign < 0)
        {
            baseValue += group.N;
        }

        return BigInteger.ModPow(baseValue, clientPrivate + (scrambler * privateExponent), group.N);
    }

    /// <summary>S = (A * v^u) ^ b mod N. Only the server side (self-test) needs it.</summary>
    public static BigInteger ComputeServerSecret(
        SrpGroup group,
        BigInteger verifier,
        BigInteger serverPrivate,
        BigInteger scrambler,
        BigInteger clientPublic) =>
        BigInteger.ModPow(
            clientPublic * BigInteger.ModPow(verifier, scrambler, group.N) % group.N,
            serverPrivate,
            group.N);

    /// <summary>K = H(S)</summary>
    public static byte[] ComputeSessionKey(HashAlgorithmName algorithm, BigInteger secret) =>
        Hash(algorithm, ToMinimalBytes(secret));

    /// <summary>M1 = H(H(N) xor H(PAD(g)) | H(username) | salt | A | B | K)</summary>
    public static byte[] ComputeClientProof(
        SrpGroup group,
        HashAlgorithmName algorithm,
        string username,
        byte[] salt,
        BigInteger clientPublic,
        BigInteger serverPublic,
        byte[] sessionKey)
    {
        var groupHash = Hash(algorithm, ToMinimalBytes(group.N));
        var generatorHash = Hash(algorithm, ToPaddedBytes(group.G, group.ByteLength));
        for (var i = 0; i < groupHash.Length; i++)
        {
            groupHash[i] ^= generatorHash[i];
        }

        return Hash(
            algorithm,
            groupHash,
            Hash(algorithm, Encoding.UTF8.GetBytes(username)),
            salt,
            ToMinimalBytes(clientPublic),
            ToMinimalBytes(serverPublic),
            sessionKey);
    }

    /// <summary>M2 = H(A | M1 | K)</summary>
    public static byte[] ComputeServerProof(
        HashAlgorithmName algorithm, BigInteger clientPublic, byte[] clientProof, byte[] sessionKey) =>
        Hash(algorithm, ToMinimalBytes(clientPublic), clientProof, sessionKey);
}
