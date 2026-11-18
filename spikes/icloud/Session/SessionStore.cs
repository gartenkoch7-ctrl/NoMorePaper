using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json;

namespace ICloudSpike.Session;

/// <summary>Loads and saves the encrypted session file. The file lives outside the repository.</summary>
internal sealed class SessionStore
{
    private const string FileName = "session.dat";

    private readonly string _directory;
    private readonly ISecretProtector _protector;

    public SessionStore(string directory, ISecretProtector protector)
    {
        _directory = directory;
        _protector = protector;
    }

    public string FilePath => Path.Combine(_directory, FileName);

    /// <summary>Returns the stored session, or a fresh one if there is none or it cannot be read.</summary>
    public SessionData Load()
    {
        if (!File.Exists(FilePath))
        {
            return new SessionData();
        }

        try
        {
            var clear = _protector.Unprotect(File.ReadAllBytes(FilePath));
            try
            {
                return JsonSerializer.Deserialize<SessionData>(clear) ?? new SessionData();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(clear);
            }
        }
        catch (Exception exception) when (exception is JsonException or Win32Exception or IOException)
        {
            // Unreadable (other Windows user, damaged file): start over with a full sign-in.
            return new SessionData();
        }
    }

    public void Save(SessionData session)
    {
        Directory.CreateDirectory(_directory);
        var clear = JsonSerializer.SerializeToUtf8Bytes(session);
        try
        {
            // Write next to the target and move, so a crash never leaves half a session file.
            var temporary = FilePath + ".tmp";
            File.WriteAllBytes(temporary, _protector.Protect(clear));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
        }
    }

    public bool Delete()
    {
        if (!File.Exists(FilePath))
        {
            return false;
        }

        File.Delete(FilePath);
        return true;
    }
}
