using ICloudSpike.Http;

namespace ICloudSpike.SelfTest;

/// <summary>Minimal assertions for the offline self-test (the spike has no test project).</summary>
internal static class Check
{
    public static void That(bool condition, string what)
    {
        if (!condition)
        {
            throw new InvalidOperationException(what);
        }
    }

    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{what}: expected '{expected}', got '{actual}'.");
        }
    }

    public static void Hex(string expectedHex, byte[] actual, string what) =>
        Equal(
            expectedHex.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant(),
            Convert.ToHexString(actual),
            what);

    public static void Throws(ICloudFailure expected, Action action, string what)
    {
        try
        {
            action();
        }
        catch (ICloudException exception)
        {
            Equal(expected, exception.Failure, what);
            return;
        }

        throw new InvalidOperationException($"{what}: expected a failure of kind {expected}, but nothing was thrown.");
    }
}
