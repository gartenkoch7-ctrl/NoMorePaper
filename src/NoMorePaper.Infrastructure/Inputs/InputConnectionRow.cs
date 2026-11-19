using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>
/// A row of <c>input_connections</c>. <see cref="AccountKey"/> is the account or <see cref="Guid.Empty"/>
/// for the household's own set-up, so the key works without nullable columns.
/// </summary>
internal sealed class InputConnectionRow
{
    public InputKind Kind { get; set; }

    public Guid AccountKey { get; set; }

    public Guid? AccountId { get; set; }

    /// <summary>JSON without secrets.</summary>
    public string Settings { get; set; } = "{}";

    /// <summary>Encrypted with Data Protection; never plain text.</summary>
    public byte[]? Secret { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
