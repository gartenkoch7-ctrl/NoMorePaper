using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Classification;

/// <summary>The fields of a document as one account sees them; the Server maps it to the Contracts DTO.</summary>
/// <param name="DocumentDate">Date printed on the document.</param>
/// <param name="Summary">One or two sentences about the content.</param>
/// <param name="Tags">Ordered by name.</param>
/// <param name="PossibleDuplicateOf">The other document; <c>null</c> when there is none or the account may not see it.</param>
/// <param name="EditorIds">Members besides the owner who may edit the fields.</param>
/// <param name="CanEdit">The account may edit and confirm the fields.</param>
/// <param name="Source">How the document came in.</param>
/// <param name="Mail">"From the mail", for documents that came by mail.</param>
/// <param name="NeedsOwnerCheck">The owner was unknown on arrival; the owner keeps it or hands it over.</param>
public sealed record DocumentFieldsView(
    EntrySummary? Correspondent,
    EntrySummary? DocumentType,
    DateOnly? DocumentDate,
    Money? Amount,
    string? Summary,
    IReadOnlyList<EntrySummary> Tags,
    RecognitionState Recognition,
    Guid? PossibleDuplicateOf,
    IReadOnlyList<Guid> EditorIds,
    bool CanEdit,
    DocumentSource Source,
    MailOrigin? Mail,
    bool NeedsOwnerCheck);
