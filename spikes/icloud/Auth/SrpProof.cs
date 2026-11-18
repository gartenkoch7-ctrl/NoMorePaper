using System.Numerics;

namespace ICloudSpike.Auth;

/// <summary>Result of the client-side SRP computation.</summary>
/// <param name="ClientProof">M1, sent to the server.</param>
/// <param name="ExpectedServerProof">M2; Apple's sign-in expects the client to send it as well.</param>
/// <param name="PremasterSecret">S; exposed only so the self-test can compare it with RFC 5054.</param>
internal sealed record SrpProof(byte[] ClientProof, byte[] ExpectedServerProof, BigInteger PremasterSecret);
