using System.Net;
using System.Text;

namespace ShiftingGuru.Services.Email;

/// <summary>One label/value row in the information card.</summary>
public record EmailRow(string Label, string Value);

/// <summary>The content of an email, independent of how it's rendered.</summary>
public class EmailContent
{
    public string Heading { get; set; } = "";
    public string Intro { get; set; } = "";
    public IReadOnlyList<EmailRow> Rows { get; set; } = Array.Empty<EmailRow>();
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public string? Closing { get; set; }
}

/// <summary>
/// Builds the branded HTML and a plain-text alternative.
///
/// Deliberately table-based with inline styles: Outlook still doesn't support
/// flexbox or grid, and many clients strip &lt;style&gt; blocks. No JavaScript,
/// no external images, no web fonts.
/// </summary>
public class EmailTemplate
{
    private readonly AppOptions _app;

    public EmailTemplate(AppOptions app) => _app = app;

    public string RenderHtml(EmailContent content)
    {
        var html = new StringBuilder();

        html.Append("""
            <!DOCTYPE html>
            <html lang="en"><head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            </head>
            <body style="margin:0;padding:0;background-color:#f5f7f9;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f5f7f9;padding:24px 12px;">
            <tr><td align="center">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background-color:#ffffff;border:1px solid #e5eaef;">
            """);

        // Header
        html.Append("""
            <tr><td style="background-color:#0a1119;padding:20px 28px;">
            <span style="font-family:Arial,Helvetica,sans-serif;font-size:17px;font-weight:bold;color:#ffffff;letter-spacing:-0.3px;">ShiftingGuru</span>
            </td></tr>
            """);

        // Heading and intro
        html.Append($"""
            <tr><td style="padding:32px 28px 0 28px;">
            <h1 style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:22px;line-height:1.3;color:#0a1119;">{Escape(content.Heading)}</h1>
            <p style="margin:14px 0 0 0;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.6;color:#55697e;">{Escape(content.Intro)}</p>
            </td></tr>
            """);

        // Information card
        if (content.Rows.Count > 0)
        {
            html.Append("""
                <tr><td style="padding:24px 28px 0 28px;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f5f7f9;border:1px solid #e5eaef;">
                """);

            foreach (var row in content.Rows)
            {
                html.Append($"""
                    <tr>
                    <td style="padding:10px 16px;font-family:Arial,Helvetica,sans-serif;font-size:13px;color:#55697e;">{Escape(row.Label)}</td>
                    <td align="right" style="padding:10px 16px;font-family:Arial,Helvetica,sans-serif;font-size:14px;font-weight:bold;color:#0a1119;">{Escape(row.Value)}</td>
                    </tr>
                    """);
            }

            html.Append("</table></td></tr>");
        }

        // Call to action
        if (!string.IsNullOrWhiteSpace(content.CtaUrl) && !string.IsNullOrWhiteSpace(content.CtaLabel))
        {
            html.Append($"""
                <tr><td style="padding:28px 28px 0 28px;">
                <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                <td style="background-color:#0e9f6e;">
                <a href="{Escape(content.CtaUrl)}" style="display:inline-block;padding:14px 28px;font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:bold;color:#060a0f;text-decoration:none;">{Escape(content.CtaLabel)}</a>
                </td></tr></table>
                </td></tr>
                """);
        }

        if (!string.IsNullOrWhiteSpace(content.Closing))
        {
            html.Append($"""
                <tr><td style="padding:24px 28px 0 28px;">
                <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.6;color:#55697e;">{Escape(content.Closing)}</p>
                </td></tr>
                """);
        }

        // Footer
        html.Append($"""
            <tr><td style="padding:32px 28px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
            <td style="border-top:1px solid #e5eaef;padding-top:20px;">
            <p style="margin:0;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:1.6;color:#7e8fa1;">
            Questions? Reply to this email or write to {Escape(_app.SupportEmail)}.<br>
            ShiftingGuru connects customers with independent service providers.
            </p>
            </td></tr></table>
            </td></tr>
            </table></td></tr></table></body></html>
            """);

        return html.ToString();
    }

    public string RenderText(EmailContent content)
    {
        var text = new StringBuilder();

        text.AppendLine("ShiftingGuru").AppendLine();
        text.AppendLine(content.Heading).AppendLine();
        text.AppendLine(content.Intro).AppendLine();

        foreach (var row in content.Rows)
        {
            text.AppendLine($"{row.Label}: {row.Value}");
        }

        if (!string.IsNullOrWhiteSpace(content.CtaUrl))
        {
            text.AppendLine().AppendLine($"{content.CtaLabel}: {content.CtaUrl}");
        }

        if (!string.IsNullOrWhiteSpace(content.Closing))
        {
            text.AppendLine().AppendLine(content.Closing);
        }

        text.AppendLine().AppendLine($"Questions? Write to {_app.SupportEmail}.");

        return text.ToString();
    }

    /// <summary>Everything interpolated into the HTML goes through here.</summary>
    private static string Escape(string? value) => WebUtility.HtmlEncode(value ?? "");
}