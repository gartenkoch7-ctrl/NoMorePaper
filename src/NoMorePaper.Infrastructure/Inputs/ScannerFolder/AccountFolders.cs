using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Infrastructure.Inputs.ScannerFolder;

/// <summary>
/// The subfolders of the scanner folder: one per account, named after its user name (<see cref="FolderNameOf"/>).
/// Worked out anew from the existing accounts at every run, so a removed account's folder is never read again,
/// also after a restart. One rule for every subfolder without an account (removed account, made by hand):
/// it is not read and its files stay. Hidden folders and the error folder are left alone.
/// </summary>
/// <remarks>
/// A folder carries its account's id in a hidden file (<see cref="OwnerMarkName"/>), so a new account with the
/// user name of a removed one never gets the removed account's scans: a folder marked for another id counts
/// as a folder without account. A folder from before the mark gets it on its first run, if its name is an
/// account's.
/// </remarks>
public sealed class AccountFolders(IAccountStore accounts, ILogger<AccountFolders> logger)
{
    /// <summary>Files of an unknown type go here, in the root of the scanner folder.</summary>
    public const string ErrorFolderName = "Fehler";

    /// <summary>The hidden file in an account folder that holds the account's id; never taken in (hidden).</summary>
    public const string OwnerMarkName = ".nomorepaper-owner";

    /// <summary>
    /// The account's folder: its user name in lower case if that is a safe folder name on every client
    /// (letters a–z, digits, <c>.</c>, <c>_</c>, <c>-</c>; starting and ending with a letter or digit, so never
    /// hidden, never <c>..</c>, never cut by Windows), otherwise the account id.
    /// </summary>
    public static string FolderNameOf(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var name = account.UserName.ToLowerInvariant();
        return IsSafe(name) && !string.Equals(name, ErrorFolderName, StringComparison.OrdinalIgnoreCase)
            ? name
            : IdFolder(account.Id);
    }

    /// <summary>Creates the missing account folders and lists what this run reads.</summary>
    /// <param name="root">The scanner folder; it exists.</param>
    public async Task<ScannerFolderPlan> PrepareAsync(string root, CancellationToken cancellationToken)
    {
        var owners = Assign(await accounts.ListAsync(cancellationToken).ConfigureAwait(false));
        var failures = 0;
        foreach (var (name, accountId) in owners)
        {
            var folder = Path.Combine(root, name);
            if (Directory.Exists(folder))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(folder);
                WriteMark(folder, accountId);
                logger.AccountFolderCreated(accountId);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Without its mark the folder is adopted by the next run, as long as the account exists.
                logger.AccountFolderFailed(accountId, exception.GetType().Name);
                failures++;
            }
        }

        List<(string Path, Guid? OwnerId)> folders = [(root, null)];
        var unknown = 0;
        foreach (var folder in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(folder);
            if (name.StartsWith('.') || string.Equals(name, ErrorFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Ordinal: on the Linux volume "Anna" and "anna" are two folders, and only one is the account's.
            if (owners.TryGetValue(name, out var accountId) && IsOwnedBy(folder, accountId, ref failures))
            {
                folders.Add((folder, accountId));
            }
            else
            {
                unknown++;
            }
        }

        if (unknown > 0)
        {
            logger.UnknownFolders(unknown);
        }

        return new ScannerFolderPlan(folders, unknown, failures);
    }

    // The folder's mark decides; a folder without one (made before the mark existed, or its writing failed)
    // takes the mark of the account its name belongs to now.
    private bool IsOwnedBy(string folder, Guid accountId, ref int failures)
    {
        var mark = Path.Combine(folder, OwnerMarkName);
        try
        {
            if (File.Exists(mark))
            {
                var owner = File.ReadAllText(mark).Trim();
                if (Guid.TryParseExact(owner, "D", out var ownerId) && ownerId == accountId)
                {
                    return true;
                }

                logger.ForeignFolder(accountId);
                return false;
            }

            WriteMark(folder, accountId);
            logger.AccountFolderMarked(accountId);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Unclear whose folder it is: not read this time, its files stay.
            logger.AccountFolderFailed(accountId, exception.GetType().Name);
            failures++;
            return false;
        }
    }

    private static void WriteMark(string folder, Guid accountId) =>
        File.WriteAllText(Path.Combine(folder, OwnerMarkName), IdFolder(accountId));

    // Two accounts must never share a folder: where a user name equals another account's id, both fall back
    // to their own ids. Should even that clash (a name made of a third account's id), the later account gets
    // no folder rather than another account's documents.
    private static Dictionary<string, Guid> Assign(IReadOnlyList<Account> all)
    {
        var preferred = all.Select(account => (Account: account, Name: FolderNameOf(account))).ToList();
        var shared = preferred.GroupBy(entry => entry.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var owners = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var (account, name) in preferred.OrderBy(entry => shared.Contains(entry.Name)))
        {
            owners.TryAdd(shared.Contains(name) ? IdFolder(account.Id) : name, account.Id);
        }

        return owners;
    }

    private static string IdFolder(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    private static bool IsSafe(string name) =>
        name.Length > 0
        && IsLetterOrDigit(name[0])
        && IsLetterOrDigit(name[^1])
        && name.All(c => IsLetterOrDigit(c) || c is '.' or '_' or '-');

    private static bool IsLetterOrDigit(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
