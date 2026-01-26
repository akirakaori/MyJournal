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

    /// <summary>
    /// Sanitizes Quill HTML output by removing dangerous tags and attributes
    /// while preserving safe formatting tags.
    /// Allowed: p, br, strong, em, ul, ol, li, a, h1-h6, blockquote, code, pre, span, div
    /// Removes: script, style, iframe, on* attributes, javascript: links
    /// </summary>
    public static string SanitizeQuillHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Remove script and style tags with their content
        html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Remove dangerous tags
        html = Regex.Replace(html, @"<iframe[^>]*>.*?</iframe>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<object[^>]*>.*?</object>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<embed[^>]*>", "", RegexOptions.IgnoreCase);

        // Remove on* event attributes (onclick, onload, onerror, etc.)
        html = Regex.Replace(html, @"\s+on\w+\s*=\s*[""'][^""']*[""']", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"\s+on\w+\s*=\s*[^\s>]+", "", RegexOptions.IgnoreCase);

        // Remove javascript: protocol from hrefs
        html = Regex.Replace(html, @"<a[^>]*href\s*=\s*[""']javascript:[^""']*[""'][^>]*>", "<a>", RegexOptions.IgnoreCase);

        // Remove style attributes to prevent CSS injection
        html = Regex.Replace(html, @"\s+style\s*=\s*[""'][^""']*[""']", "", RegexOptions.IgnoreCase);

        // Allow only safe tags (remove all others)
        // Quill uses: p, br, strong, em, u, s, a, ol, ul, li, blockquote, code, pre, h1-h6, span for styling
        var allowedTags = new[] { "p", "br", "strong", "em", "u", "s", "a", "ol", "ul", "li", 
            "blockquote", "code", "pre", "h1", "h2", "h3", "h4", "h5", "h6", "span", "div" };
        
        // This regex removes tags NOT in the allowed list
        html = Regex.Replace(html, @"<(?!\/?)(?!" + string.Join("|", allowedTags) + @"\b)[^>]+>", "", RegexOptions.IgnoreCase);

        // Decode HTML entities for safety
        html = WebUtility.HtmlDecode(html);
        html = WebUtility.HtmlEncode(html);
        
        // Re-enable safe HTML tags by reversing the encoding for allowed tags
        foreach (var tag in allowedTags)
        {
            html = html.Replace($"&lt;{tag}&gt;", $"<{tag}>");
            html = html.Replace($"&lt;{tag} ", $"<{tag} ");
            html = html.Replace($"&lt;/{tag}&gt;", $"</{tag}>");
        }
        
        // Allow href and class attributes (commonly used by Quill)
        html = Regex.Replace(html, @"&quot;", "\"", RegexOptions.IgnoreCase);

        return html.Trim();
    }
}
