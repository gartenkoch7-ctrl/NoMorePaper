using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using ICloudSpike.Auth;
using ICloudSpike.Http;

namespace ICloudSpike.SelfTest;

/// <summary>Checks the SRP and password math against published and independently computed values.</summary>
internal static class SrpSelfTests
{
    // Shared inputs of the two "Apple variant" vectors (computed with an independent reference
    // script using only Python's hashlib and pow).
    private const string VectorUser = "spike@example.com";
    private const string VectorPassword = "Tr0ub4dor&3-äöü";
    private const string VectorSalt = "A1B2C3D4E5F60718293A4B5C6D7E8F90";
    private const int VectorIterations = 1000;

    public static IEnumerable<(string Name, Func<Task> Run)> All =>
    [
        ("SRP: Gruppen aus RFC 5054 sind sichere Primzahlen", Sync(GroupsAreSafePrimes)),
        ("SRP: Testvektoren aus RFC 5054 Anhang B", Sync(Rfc5054TestVector)),
        ("Passwortschlüssel: PBKDF2-Vektor aus RFC 7914", Sync(Pbkdf2TestVector)),
        ("SRP: Apple-Variante mit Protokoll s2k", Sync(() => AppleVariant(
            ApplePasswordKey.ProtocolS2k,
            "f7d102e108fa692581537191c7128b3687d7c1a2f4725b343a1530ae08326e64",
            "e56ca869cff720c8b473a5145017d1487dbf4c3145af0ecbd5e6b7dbd4cf991c",
            "52a685ccaa472fa71458aa59027e37a62aef7b912b41f41080069a20e79412e5"))),
        ("SRP: Apple-Variante mit Protokoll s2k_fo", Sync(() => AppleVariant(
            ApplePasswordKey.ProtocolS2kFo,
            "c070b40cbf3fe9859e07d8e5ad4b983894f15a1f97c61eee358143661261d647",
            "a89755a66c8a880d7dba3c21630d80eeb286beedd2c12781c423682debcf4f03",
            "b22fb2306050db6b33cc5633e5dac60ed6dcbc28a6aec8e5f416ab91b0580dab"))),
        ("SRP: ungültige Serverwerte werden abgelehnt", Sync(InvalidServerValuesAreRejected)),
    ];

    private static Func<Task> Sync(Action action) => () =>
    {
        action();
        return Task.CompletedTask;
    };

    /// <summary>
    /// A mistyped digit in a 2048-bit constant would go unnoticed otherwise; it is practically
    /// impossible for a typo to produce another safe prime.
    /// </summary>
    private static void GroupsAreSafePrimes()
    {
        foreach (var group in (SrpGroup[])[SrpGroup.Rfc5054Bits1024, SrpGroup.Rfc5054Bits2048])
        {
            Check.Equal(group.ByteLength * 8, (int)group.N.GetBitLength(), "bit length of N");
            Check.That(IsProbablePrime(group.N), "N is not prime");
            Check.That(IsProbablePrime((group.N - 1) / 2), "(N - 1) / 2 is not prime");
        }
    }

    private static void Rfc5054TestVector()
    {
        var group = SrpGroup.Rfc5054Bits1024;
        var hash = HashAlgorithmName.SHA1;
        var salt = Convert.FromHexString("BEB25379D1A8581EB5A727673A2441EE");
        var password = Encoding.ASCII.GetBytes("password123");
        var clientPrivate = Convert.FromHexString("60975527035CF2AD1989806F0407210BC81EDC04E2762A56AFD529DDDA2D4393");
        var serverPrivate = SrpMath.ToInteger(
            Convert.FromHexString("E487CB59D31AC550471E81F00F6928E01DDA08E974A004F49E61F5D105284D20"));

        var multiplier = SrpMath.ComputeMultiplier(group, hash);
        Check.Hex("7556AA045AEF2CDD07ABAF0F665C3E818913186F", SrpMath.ToMinimalBytes(multiplier), "k");
        var exponent = SrpMath.ComputePrivateExponent(hash, salt, "alice", password);
        Check.Hex("94B7555AABE9127CC58CCF4993DB6CF84D16C124", SrpMath.ToMinimalBytes(exponent), "x");

        var client = new SrpClient(group, hash, clientPrivate);
        Check.Hex(
            "61D5E490F6F1B79547B0704C436F523DD0E560F0C64115BB72557EC44352E8903211C04692272D8B2D1A5358A2CF1B6E"
            + "0BFCF99F921530EC8E39356179EAE45E42BA92AEACED825171E1E8B9AF6D9C03E1327F44BE087EF06530E69F66615261"
            + "EEF54073CA11CF5858F0EDFDFE15EFEAB349EF5D76988A3672FAC47B0769447B",
            client.PublicKeyBytes,
            "A");

        // B is derived the way a server does it, so the vector also covers the verifier.
        var verifier = SrpMath.ComputeVerifier(group, exponent);
        var serverPublic = ((multiplier * verifier) + BigInteger.ModPow(group.G, serverPrivate, group.N)) % group.N;
        var scrambler = SrpMath.ComputeScrambler(group, hash, SrpMath.ToInteger(client.PublicKeyBytes), serverPublic);
        Check.Hex("CE38B9593487DA98554ED47D70A7AE5F462EF019", SrpMath.ToMinimalBytes(scrambler), "u");

        var proof = client.CreateProof("alice", "alice", password, salt, SrpMath.ToMinimalBytes(serverPublic));
        Check.Hex(
            "B0DC82BABCF30674AE450C0287745E7990A3381F63B387AAF271A10D233861E359B48220F7C4693C9AE12B0A6F67809F"
            + "0876E2D013800D6C41BB59B6D5979B5C00A172B4A2A5903A0BDCAF8A709585EB2AFAFA8F3499B200210DCC1F10EB3394"
            + "3CD67FC88A2F39A4BE5BEC4EC0A3212DC346D7E474B29EDE8A469FFECA686E5A",
            SrpMath.ToMinimalBytes(proof.PremasterSecret),
            "premaster secret");
    }

    private static void Pbkdf2TestVector()
    {
        // RFC 7914, section 11: PBKDF2-HMAC-SHA-256 with P = "passwd", S = "salt", c = 1, dkLen = 64.
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.ASCII.GetBytes("passwd"), Encoding.ASCII.GetBytes("salt"), 1, HashAlgorithmName.SHA256, 64);
        Check.Hex(
            "55AC046E56E3089FEC1691C22544B605F94185216DDE0465E68B9D57C20DACBC"
            + "49CA9CCCF179B645991664B39D77EF317C71B845B1E30BD509112041D3A19783",
            derived,
            "PBKDF2");
    }

    private static void AppleVariant(string protocol, string expectedKey, string expectedM1, string expectedM2)
    {
        var group = SrpGroup.Rfc5054Bits2048;
        var hash = HashAlgorithmName.SHA256;
        var salt = Convert.FromHexString(VectorSalt);
        var key = ApplePasswordKey.Derive(Encoding.UTF8.GetBytes(VectorPassword), protocol, salt, VectorIterations);
        Check.Hex(expectedKey, key, "password key");

        var clientPrivate = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var serverPrivate = SrpMath.ToInteger(Enumerable.Range(101, 32).Select(value => (byte)value).ToArray());
        var exponent = SrpMath.ComputePrivateExponent(hash, salt, string.Empty, key);
        var verifier = SrpMath.ComputeVerifier(group, exponent);
        var multiplier = SrpMath.ComputeMultiplier(group, hash);
        var serverPublic = ((multiplier * verifier) + BigInteger.ModPow(group.G, serverPrivate, group.N)) % group.N;

        var client = new SrpClient(group, hash, clientPrivate);
        var proof = client.CreateProof(VectorUser, string.Empty, key, salt, SrpMath.ToMinimalBytes(serverPublic));
        Check.Hex(expectedM1, proof.ClientProof, "M1");
        Check.Hex(expectedM2, proof.ExpectedServerProof, "M2");

        // Both sides must arrive at the same secret, otherwise the server-side model is wrong.
        var clientPublic = SrpMath.ToInteger(client.PublicKeyBytes);
        var scrambler = SrpMath.ComputeScrambler(group, hash, clientPublic, serverPublic);
        var serverSecret = SrpMath.ComputeServerSecret(group, verifier, serverPrivate, scrambler, clientPublic);
        Check.Equal(serverSecret, proof.PremasterSecret, "client and server secret");
    }

    private static void InvalidServerValuesAreRejected()
    {
        var group = SrpGroup.Rfc5054Bits2048;
        var client = new SrpClient(group, HashAlgorithmName.SHA256, [1, 2, 3, 4]);
        byte[] salt = [1, 2, 3];
        byte[] key = [4, 5, 6];
        Check.Throws(
            ICloudFailure.ProtocolChanged,
            () => client.CreateProof("user", string.Empty, key, salt, [0]),
            "B = 0");
        Check.Throws(
            ICloudFailure.ProtocolChanged,
            () => client.CreateProof("user", string.Empty, key, salt, SrpMath.ToMinimalBytes(group.N * 2)),
            "B = 2N");
        Check.Throws(
            ICloudFailure.ProtocolChanged,
            () => ApplePasswordKey.Derive(key, "s3k", salt, 1000),
            "unknown password protocol");
        Check.Throws(
            ICloudFailure.ProtocolChanged,
            () => ApplePasswordKey.Derive(key, ApplePasswordKey.ProtocolS2k, salt, 0),
            "iteration count 0");
    }

    /// <summary>Miller–Rabin with fixed small bases; enough to catch a mistyped constant.</summary>
    private static bool IsProbablePrime(BigInteger candidate)
    {
        var oddPart = candidate - 1;
        var twos = 0;
        while (oddPart.IsEven)
        {
            oddPart >>= 1;
            twos++;
        }

        foreach (var witness in (int[])[2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37])
        {
            var value = BigInteger.ModPow(witness, oddPart, candidate);
            if (value.IsOne || value == candidate - 1)
            {
                continue;
            }

            var composite = true;
            for (var round = 1; round < twos && composite; round++)
            {
                value = BigInteger.ModPow(value, 2, candidate);
                composite = value != candidate - 1;
            }

            if (composite)
            {
                return false;
            }
        }

        return true;
    }
}
