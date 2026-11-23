namespace NoMorePaper.Domain.Editing;

/// <summary>Why one document cannot be appended to another (<see cref="AppendRule.Check"/>).</summary>
public enum AppendRefusal
{
    /// <summary>A document cannot be appended to itself.</summary>
    Same,

    /// <summary>One of them lies in the trash.</summary>
    InTrash,

    /// <summary>The account may not edit one of them.</summary>
    NotEditable,

    /// <summary>One of them is not processed completely yet.</summary>
    NotDone,
}
