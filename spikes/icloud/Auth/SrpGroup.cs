using System.Globalization;
using System.Numerics;

namespace ICloudSpike.Auth;

/// <summary>SRP group parameters (safe prime N, generator g) from RFC 5054, Appendix A.</summary>
internal sealed class SrpGroup
{
    private SrpGroup(string modulusHex, int generator)
    {
        // The leading "0" keeps BigInteger.Parse from reading the top bit as a sign.
        N = BigInteger.Parse("0" + modulusHex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        G = new BigInteger(generator);
        ByteLength = modulusHex.Length / 2;
    }

    public BigInteger N { get; }

    public BigInteger G { get; }

    /// <summary>Length of N in bytes; the width used wherever the protocol pads values.</summary>
    public int ByteLength { get; }

    /// <summary>1024-bit group. Only used to check the math against the RFC 5054 test vectors.</summary>
    public static SrpGroup Rfc5054Bits1024 { get; } = new(
        "EEAF0AB9ADB38DD69C33F80AFA8FC5E86072618775FF3C0B9EA2314C9C256576D674DF7496EA81D3"
        + "383B4813D692C6E0E0D5D8E250B98BE48E495C1D6089DAD15DC7D7B46154D6B6CE8EF4AD69B15D49"
        + "82559B297BCF1885C529F566660E57EC68EDBC3C05726CC02FD4CBF4976EAA9AFD5138FE8376435B"
        + "9FC61D2FC0EB06E3",
        2);

    /// <summary>2048-bit group, the one the iCloud web sign-in uses.</summary>
    public static SrpGroup Rfc5054Bits2048 { get; } = new(
        "AC6BDB41324A9A9BF166DE5E1389582FAF72B6651987EE07FC3192943DB56050A37329CBB4A099ED"
        + "8193E0757767A13DD52312AB4B03310DCD7F48A9DA04FD50E8083969EDB767B0CF6095179A163AB3"
        + "661A05FBD5FAAAE82918A9962F0B93B855F97993EC975EEAA80D740ADBF4FF747359D041D5C33EA7"
        + "1D281E446B14773BCA97B43A23FB801676BD207A436C6481F1D2B9078717461A5B9D32E688F87748"
        + "544523B524B0D57D5EA77A2775D2ECFA032CFBDBF52FB3786160279004E57AE6AF874E7303CE5329"
        + "9CCC041C7BC308D82A5698F3A8D0C38271AE35F8E9DBFBB694B5C803D89F7AE435DE236D525F5475"
        + "9B65E372FCD68EF20FA7111F9E4AFF73",
        2);
}
