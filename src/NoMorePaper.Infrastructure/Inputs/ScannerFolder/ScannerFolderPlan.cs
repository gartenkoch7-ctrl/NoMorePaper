namespace NoMorePaper.Infrastructure.Inputs.ScannerFolder;

/// <summary>What one run of the scanner folder reads, as <see cref="AccountFolders"/> worked it out from the accounts.</summary>
/// <param name="Folders">
/// The root (owner <c>null</c>: unknown, so the administrator gets it for the owner check) and every account's
/// subfolder that exists, with its account.
/// </param>
/// <param name="UnknownFolders">Subfolders without an account (removed account, made by hand); not read.</param>
/// <param name="Failures">Account subfolders that could not be created.</param>
public sealed record ScannerFolderPlan(IReadOnlyList<(string Path, Guid? OwnerId)> Folders, int UnknownFolders, int Failures);
