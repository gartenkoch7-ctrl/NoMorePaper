using System.Text;
using System.Text.RegularExpressions;
using MimeKit.Text;

namespace NoMorePaper.Infrastructure.Inputs.Mail;

/// <summary>
/// Turns the HTML part of a mail into plain text for the LLM and the text PDF: blocks become lines, scripts
/// and styles are dropped, a link keeps its address after its text (the address tells phishing apart).
/// Nothing is fetched (ADR-0024). Pure logic.
/// </summary>
public static partial class HtmlText
{
    private static readonly HashSet<HtmlTagId> s_blocks =
    [
        HtmlTagId.P, HtmlTagId.Div, HtmlTagId.Br, HtmlTagId.TR, HtmlTagId.LI, HtmlTagId.Table, HtmlTagId.H1, HtmlTagId.H2,
        HtmlTagId.H3, HtmlTagId.H4, HtmlTagId.H5, HtmlTagId.H6, HtmlTagId.HR, HtmlTagId.BlockQuote, HtmlTagId.UL, HtmlTagId.OL,
        HtmlTagId.Title,
    ];

    public static string Convert(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var text = new StringBuilder(html.Length / 2);
        using var reader = new StringReader(html);
        var tokenizer = new HtmlTokenizer(reader);
        var hidden = 0;
        string? link = null;
        while (tokenizer.ReadNextToken(out var token))
        {
            switch (token)
            {
                case HtmlTagToken tag when tag.Id is HtmlTagId.Script or HtmlTagId.Style or HtmlTagId.Head:
                    hidden = Math.Max(0, hidden + (tag.IsEndTag ? -1 : tag.IsEmptyElement ? 0 : 1));
                    break;
                case HtmlTagToken tag when tag.Id == HtmlTagId.A:
                    if (tag.IsEndTag && link is not null)
                    {
                        text.Append(" (").Append(link).Append(')');
                        link = null;
                    }
                    else if (!tag.IsEndTag)
                    {
                        link = tag.Attributes.FirstOrDefault(attribute => attribute.Id == HtmlAttributeId.Href)?.Value?.Trim();
                        link = link is not null && link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? link : null;
                    }

                    break;
                case HtmlTagToken tag when s_blocks.Contains(tag.Id):
                    text.Append('\n');
                    break;
                case HtmlTagToken tag when tag.Id is HtmlTagId.TD or HtmlTagId.TH && !tag.IsEndTag:
                    text.Append("  ");
                    break;
                case HtmlDataToken data when hidden == 0 && token is not HtmlScriptDataToken:
                    text.Append(Collapse(data.Data));
                    break;
            }
        }

        // A paragraph ends and the next starts with a break; more than one empty line in a row says nothing.
        var lines = string.Join('\n', text.ToString().Split('\n').Select(line => line.Trim()));
        return BlankLines().Replace(lines, "\n\n").Trim();
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();

    // Line breaks and runs of white space in HTML source are one space when shown.
    private static string Collapse(string data)
    {
        var collapsed = new StringBuilder(data.Length);
        var space = false;
        foreach (var c in data)
        {
            // Zero-width characters pad the preview text of newsletters.
            if (char.IsWhiteSpace(c) || c is '​' or '‌' or '͏')
            {
                space = true;
                continue;
            }

            if (space)
            {
                collapsed.Append(' ');
            }

            space = false;
            collapsed.Append(c);
        }

        if (space)
        {
            collapsed.Append(' ');
        }

        return collapsed.ToString();
    }
}
