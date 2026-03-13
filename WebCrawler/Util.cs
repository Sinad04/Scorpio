using System.Collections.Specialized;
using System.Web;

namespace WebCrawler;

/*
 *  Static class for Utility methods that can be globally used.
 */
public static class Util
{
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
                .Select(v => $"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}"));

        return string.Join('&', queryParameters);
    }

    private static string NormalizePath(string path)
    {
        // Decoding then re-encoding normalizes percent-encoding.
        path = Uri.UnescapeDataString(path);
        path = Uri.EscapeDataString(path);

        while (path.Contains("//"))
            path = path.Replace("//", "/");

        return string.IsNullOrEmpty(path) ? "/" : path; // Ensure root path.
    }
}