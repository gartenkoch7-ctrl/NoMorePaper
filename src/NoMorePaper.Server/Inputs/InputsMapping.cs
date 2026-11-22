using System.Text;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Server.Inputs;

/// <summary>Maps preferences, input states and "from the mail" to the Contracts DTOs; fixed words in kebab case. Pure logic.</summary>
internal static class InputsMapping
{
    public static string ToWord(this DocumentSource source) => source switch
    {
        DocumentSource.Upload => "upload",
        DocumentSource.Camera => "camera",
        DocumentSource.ScannerFolder => "scanner-folder",
        DocumentSource.Mail => "mail",
        DocumentSource.ICloud => "icloud",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown source."),
    };

    public static string ToWord(this InputKind kind) => kind switch
    {
        InputKind.ScannerFolder => "scanner-folder",
        InputKind.Mail => "mail",
        InputKind.ICloud => "icloud",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown input."),
    };

    /// <summary><c>SignInFailed</c> → <c>sign-in-failed</c>.</summary>
    public static string ToWord(this InputProblemKind problem)
    {
        var name = problem.ToString();
        var word = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                word.Append('-');
            }

            word.Append(char.ToLowerInvariant(name[i]));
        }

        return word.ToString();
    }

    public static InputStatusDto ToDto(this InputStatusEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var status = entry.Status;
        return new InputStatusDto(
            status.Kind.ToWord(),
            entry.AccountName,
            status.Problem is null ? "ok" : "problem",
            status.LastRunAt,
            status.LastSuccessAt,
            status.Problem?.ToWord(),
            status.NeedsAttention,
            status.ReceivedTotal);
    }

    public static MailOriginDto ToDto(this MailOrigin origin) => new(origin.SenderAddress, origin.Subject, origin.Note);

    public static AccountPreferencesDto ToDto(this AccountPreferences preferences) =>
        new(preferences.NewDocumentsPrivate, preferences.MailSenders);
}
