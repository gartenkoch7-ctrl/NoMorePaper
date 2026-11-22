namespace NoMorePaper.Web.Client.Design;

/// <summary>Processing state of a document as shown to the user.</summary>
public enum StatusKind
{
    Waiting,
    Processing,
    Done,
    Failed,
    Recognized,
}
