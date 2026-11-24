using NoMorePaper.Domain.Editing;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// The plan of one edit and how far the Worker got with it, stored as JSON in <c>document_edits.data</c>
/// (<see cref="EditDataJson"/>). Which parts are set depends on the <see cref="EditKind"/>.
/// </summary>
/// <param name="EditId">
/// Version 7 id of this edit: its page images and staged files lie below <c>edits/&lt;id&gt;/&lt;EditId&gt;/</c>, so a
/// refused or later edit never touches them.
/// </param>
public sealed record EditData(Guid EditId)
{
    /// <summary>The new pages (<see cref="EditKind.Pages"/>).</summary>
    public IReadOnlyList<PageStep>? Steps { get; init; }

    /// <summary>The document whose pages go to the end (<see cref="EditKind.Append"/>).</summary>
    public Guid? AppendedId { get; init; }

    /// <summary>One page list per new document (<see cref="EditKind.Split"/>).</summary>
    public IReadOnlyList<IReadOnlyList<PageStep>>? Parts { get; init; }

    /// <summary>The documents a split has made so far, in order; a retried split goes on after them.</summary>
    public IReadOnlyList<Guid> Created { get; init; } = [];

    /// <summary>
    /// Set in the transaction that stores the new version: the files still to move. A Worker that finds
    /// it only finishes the moves (<see cref="EditFinisher"/>), however often it is interrupted.
    /// </summary>
    public IReadOnlyList<FinishMove>? Finish { get; init; }

    /// <summary>Whether the new version is stored and only its files are left to move.</summary>
    public bool IsCommitted => Finish is not null;
}
