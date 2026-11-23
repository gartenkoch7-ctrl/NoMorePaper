using MudBlazor;
using NoMorePaper.Web.Client.Documents;
using Xunit;

namespace NoMorePaper.Web.Client.Tests.Documents;

public sealed class DocumentSourceTextTests
{
    [Theory]
    [InlineData("upload", "NavUpload")]
    [InlineData("camera", "NavCamera")]
    [InlineData("scanner-folder", "InpKindScannerFolder")]
    [InlineData("mail", "InpKindMail")]
    [InlineData("icloud", "InpKindICloud")]
    public void Key_NamesTheWayOfEverySource(string source, string expected) =>
        Assert.Equal(expected, DocumentSourceText.Key(source));

    [Fact]
    public void Icon_IsDistinctPerSource()
    {
        string[] sources = ["upload", "camera", "scanner-folder", "mail", "icloud"];

        var icons = sources.Select(DocumentSourceText.Icon).ToList();

        Assert.Equal(sources.Length, icons.Distinct().Count());
        Assert.Equal(Icons.Material.Outlined.Mail, DocumentSourceText.Icon("mail"));
    }

    [Fact]
    public void UnknownSource_HasNoKey_AndTheDefaultIcon()
    {
        Assert.Null(DocumentSourceText.Key("fax"));
        Assert.Equal(Icons.Material.Outlined.Description, DocumentSourceText.Icon("fax"));
    }
}
