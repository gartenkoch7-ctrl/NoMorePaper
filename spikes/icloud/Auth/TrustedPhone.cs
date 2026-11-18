namespace ICloudSpike.Auth;

/// <summary>A phone number Apple can send an SMS code to. Apple only reveals the last digits.</summary>
internal sealed record TrustedPhone(int Id, string MaskedNumber);
