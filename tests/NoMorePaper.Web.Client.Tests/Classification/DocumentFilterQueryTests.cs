using NoMorePaper.Contracts.Classification;
using NoMorePaper.Web.Client.Classification;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Classification;

public sealed class DocumentFilterQueryTests
{
    private static readonly Guid s_typeX = Guid.CreateVersion7();
    private static readonly Guid s_typeY = Guid.CreateVersion7();

    [Fact]
    public void Combine_BothOff_IsNull() => Assert.Null(DocumentFilterQuery.Combine(null, null));

    [Fact]
    public void Combine_OnlyAType_IsJustTheType() =>
        Assert.Equal(new DocumentFilterDto(DocumentTypeId: s_typeX), DocumentFilterQuery.Combine(null, s_typeX));

    [Fact]
    public void Combine_OnlyTheBar_KeepsItsFieldsAndClearsTheType()
    {
        var bar = new DocumentFilterDto(AmountFrom: 5m, IsPrivate: true, DocumentTypeId: s_typeX);

        var combined = DocumentFilterQuery.Combine(bar, null);

        Assert.Equal(new DocumentFilterDto(AmountFrom: 5m, IsPrivate: true), combined);
    }

    [Fact]
    public void Combine_TheChipWinsOverATypeOfTheBar()
    {
        var combined = DocumentFilterQuery.Combine(new DocumentFilterDto(DocumentTypeId: s_typeX), s_typeY);

        Assert.Equal(s_typeY, combined?.DocumentTypeId);
    }

    [Fact]
    public void Build_WithAType_HasTheTypeParameter() =>
        Assert.Contains($"&documentTypeId={s_typeX}", DocumentFilterQuery.Build(DocumentFilterQuery.Combine(null, s_typeX)), StringComparison.Ordinal);
}
