namespace ICloudSpike.Terminal;

/// <summary>The dialogue with the user. An interface so the self-test can script the answers.</summary>
internal interface IUserConsole
{
    void WriteLine(string text);

    /// <summary>Shows the prompt and returns the trimmed answer; empty if the user just pressed Enter.</summary>
    string ReadLine(string prompt);

    /// <summary>Reads a secret without showing it. The caller clears the returned array after use.</summary>
    char[] ReadSecret(string prompt);
}
