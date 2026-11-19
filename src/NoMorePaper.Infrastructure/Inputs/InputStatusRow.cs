using NoMorePaper.Application.Inputs;

namespace NoMorePaper.Infrastructure.Inputs;

/// <summary>A row of <c>input_status</c>; <see cref="AccountKey"/> as in <see cref="InputConnectionRow"/>.</summary>
internal sealed class InputStatusRow
{
    public InputKind Kind { get; set; }

    public Guid AccountKey { get; set; }

    public Guid? AccountId { get; set; }

    public DateTimeOffset? LastRunAt { get; set; }

    public DateTimeOffset? LastSuccessAt { get; set; }

    public InputProblemKind? Problem { get; set; }

    public bool NeedsAttention { get; set; }

    public long ReceivedTotal { get; set; }
}
