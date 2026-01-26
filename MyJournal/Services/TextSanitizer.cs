using System.Net;
using System.Text.RegularExpressions;

namespace JournalMaui.Services;

/// <summary>
/// Helper class for text sanitization and conversion.
/// Provides pure, unit-testable methods for text processing.
/// </summary>
public static class TextSanitizer
{
    /// <summary>
    /// Converts HTML content to plain text by:
    /// 1. Removing HTML tags
    /// 2. Decoding HTML entities (&amp; -> &, &lt; -> <, etc.)
    /// 3. Preserving line breaks (converts br and block-level tags to newlines)
    /// 4. Collapsing extra whitespace while preserving paragraph structure
    /// </summary>
    /// <param name="html">The HTML string to convert</param>
    /// <returns>Plain text with preserved line breaks and decoded entities</returns>
    public static string ConvertHtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Replace common block-level tags and <br> with newlines
        var text = Regex.Replace(html, @"<br\s*/?>|</p>|</div>|</h[1-6]>|</li>", "\n", RegexOptions.IgnoreCase);
        
        // Remove all remaining HTML tags
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        
        // Decode HTML entities (&amp;, &lt;, &gt;, &quot;, &#39;, etc.)
        text = WebUtility.HtmlDecode(text);
        
        // Normalize whitespace: collapse multiple spaces/tabs but preserve newlines
        text = Regex.Replace(text, @"[ \t]+", " ");
        
        // Remove extra blank lines (more than 2 consecutive newlines)
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        
        return text.Trim();
    }

    /// <summary>
    /// Checks if a string contains HTML tags
    /// </summary>
    public static bool ContainsHtmlTags(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return Regex.IsMatch(text, @"<[^>]+>");
    }
}
