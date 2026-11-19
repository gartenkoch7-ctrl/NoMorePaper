namespace NoMorePaper.Application.Security;

/// <summary>A stored secret cannot be decrypted (other purpose, damaged, key ring lost); it has to be entered again.</summary>
public sealed class SecretUnreadableException : Exception
{
    public SecretUnreadableException()
        : base("The stored secret cannot be read.")
    {
    }

    public SecretUnreadableException(string message)
        : base(message)
    {
    }

    public SecretUnreadableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
