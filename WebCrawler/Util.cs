using System.Collections.Specialized;
using System.Web;
using System.Text;
using HtmlAgilityPack;

namespace WebCrawler;

/*
 *  Static class for Utility methods that can be globally used.
 */
public static class Util
{
    public static readonly Random Random = new(); // !! Not thread safe.
    
    // Given a Url as a string, return a string of it in normalized form.
    public static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
        {
            throw new ArgumentException($"Invalid Url: {url}");
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();

        // Remove default ports for http and https.
        // TODO Support for handling more schemes.
        var port = (scheme == "http" && uri.Port == 80) || 
                   (scheme == "https" && uri.Port == 443)
            ? "" : uri.Port.ToString();

        var path = NormalizePath(uri.AbsolutePath);
        
        var sortedQueryParameterString = NormalizeQueryParameters(HttpUtility.ParseQueryString(uri.Query));

        return scheme + "://" + host + port + path + "?" + sortedQueryParameterString; // Fragment intentionally not included.
    }

   
    private static string NormalizeQueryParameters(NameValueCollection unsortedQueryParameters)
    {
        // Normalize Query Parameters by sorting them using natural order.
        var queryParameters = unsortedQueryParameters.AllKeys
            .OrderBy(k => k)
            .SelectMany(k => unsortedQueryParameters
                .GetValues(k)!
                .OrderBy(v => v)
                .Select(v => $"{Uri.EscapeDataString(k ?? "")}={Uri.EscapeDataString(v)}"));

        return string.Join('&', queryParameters);
    }

    private static string NormalizePath(string path)
    {
        while (path.Contains("//"))
            path = path.Replace("//", "/");

        return string.IsNullOrEmpty(path) ? "/" : path; // Ensure root path.
    }
    
    public static string GetBaseUrl(string url)
    {
        var uri = new Uri(url);
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}";
    }
    
    public static string GetPath(string url)
    {
        var uri = new Uri(url);
        return NormalizePath(uri.AbsolutePath);
    }
    
    public static List<string> ExtractLinks(string baseUrl, string html)
    {
        var links = new List<string>();
        var doc = new HtmlDocument();
        
        doc.LoadHtml(html);

        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes is null) return links; // No links found.

        foreach (var node in anchorNodes)
        {
            string href = node.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(href)) continue;

            if (Uri.TryCreate(new Uri(baseUrl), href, out var absoluteUri))
            {
                if (absoluteUri is null) continue; // TODO better error handling
                if ((absoluteUri.Scheme == Uri.UriSchemeHttp) || (absoluteUri.Scheme == Uri.UriSchemeHttps))
                {
                    links.Add(absoluteUri.ToString());
                }
            }
        }

        return links;
    }

    public static string ExtractTitle(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        return titleNode?.InnerText?.Trim() ?? "";
    }
    
    public static string ExtractText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return ExtractTextFromNode(doc.DocumentNode);
    }
    
    private static string ExtractTextFromNode(HtmlNode node)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Text: return HttpUtility.HtmlDecode(node.InnerText.Trim());
            case HtmlNodeType.Element:
                if (node.Name == "script" || node.Name == "style") return ""; break;
        }

        var sb = new StringBuilder();
        foreach ( var child in node.ChildNodes )
        {
            var childText = ExtractTextFromNode(child);

            if (!string.IsNullOrWhiteSpace(childText))
            {
                if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1])) sb.Append(' ');
            }
            
            sb.Append(childText);
        }
        return sb.ToString().Trim();
    }
}