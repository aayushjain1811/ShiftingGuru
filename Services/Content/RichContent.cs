using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;

namespace ShiftingGuru.Services.Content;

/// <summary>
/// Turns what an admin writes in the rich text editor into HTML that is safe
/// to put on a public page.
///
/// Safety matters here: without cleaning, anyone with admin access (or a
/// stolen admin session) could save a script into a city page and run it in
/// every visitor's browser. The sanitizer keeps only simple formatting tags -
/// paragraphs, headings, bold, italic, lists and links - and removes
/// everything else, including all styles, classes and scripts.
///
/// Older content written as plain text (blank lines, "## " headings,
/// "- " tips) still works: it is converted to the same HTML.
/// </summary>
public static class RichContent
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static readonly Regex HeadingOne = new(@"<(/?)h1\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SmallHeading = new(@"<(/?)h[4-6]\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EmptyParagraph = new(@"<p>(\s|&nbsp;|<br\s*/?>)*</p>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Safe HTML for any stored content, old plain text or new rich text.</summary>
    public static string ToSafeHtml(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";
        return IsHtml(content) ? Sanitize(content) : FromPlainText(content);
    }

    public static bool IsHtml(string content) => content.TrimStart().StartsWith('<');

    public static string Sanitize(string html)
    {
        // The page already has its own H1, so a pasted H1 becomes a section
        // heading, and very small headings are brought up to H3.
        html = HeadingOne.Replace(html, "<$1h2");
        html = SmallHeading.Replace(html, "<$1h3");

        var clean = Sanitizer.Sanitize(html);
        return EmptyParagraph.Replace(clean, "").Trim();
    }

    /// <summary>The older plain-text format, converted to HTML.</summary>
    public static string FromPlainText(string text)
    {
        var normalised = text.Replace("\r\n", "\n").Trim();
        var blocks = normalised
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        // Forgiving: no blank lines at all means every line is its own block.
        if (blocks.Count == 1 && blocks[0].Contains('\n'))
        {
            blocks = blocks[0]
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var html = new StringBuilder();

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (block.StartsWith("## "))
            {
                html.Append("<h3>").Append(Encode(block[3..].Trim())).Append("</h3>");
            }
            else if (lines.All(l => l.StartsWith("- ")))
            {
                html.Append("<ul>");
                foreach (var line in lines)
                {
                    html.Append("<li>").Append(Encode(line[2..].Trim())).Append("</li>");
                }
                html.Append("</ul>");
            }
            else
            {
                html.Append("<p>").Append(Encode(string.Join(" ", lines))).Append("</p>");
            }
        }

        return html.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[] { "p", "br", "h2", "h3", "strong", "b", "em", "i", "u", "ul", "ol", "li", "a", "blockquote" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        // Links keep only their address. No styles, classes or ids, so pasted
        // text always takes the website's own design.
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("mailto");

        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedClasses.Clear();
        sanitizer.AllowedAtRules.Clear();

        // A pasted <span> or <div> is removed, but the words inside are kept.
        sanitizer.KeepChildNodes = true;

        return sanitizer;
    }
}