using System.Web;
using HtmlAgilityPack;

namespace WebCrawler;

public class Crawler
{
    private static readonly HttpClient Client = new();
    private bool _crawling = true;
    private readonly LinkFrontier _frontier = new();
    private readonly CrawlerDb _database = new();

    public async Task CrawlAsync()
    {
        if (_frontier.TryGetNextUrl(out var url))
        {
            Console.WriteLine($"Crawling {url}");
            if (url is not null)
            {
                var page = await FetchPageAsync(url);
                Console.WriteLine($"Saving {page.Title} to DB");
                await _database.SavePageAsync(page);
                
                var links = ExtractLinks(url, page.Html);
                foreach (var link in links) AddLinkToFrontier(link);
            }
        }
        else
        {
            await Task.Delay(100);
        }
    }
    
    // Debug
    public void AddLinkToFrontier(string link)
    {
        var normalizedLink = Util.NormalizeUrl(link);
        _frontier.AddIfNew(normalizedLink);
    }


    private List<string> ExtractLinks(string baseUrl, string html)
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
    
    private async Task<CrawledPage> FetchPageAsync(string url)
    {
        try
        {
            Console.WriteLine($"Fetching {url}");
            var html = await Client.GetStringAsync(url);
            Console.WriteLine($"Fetched {html.Substring(0, 50)}");
            return new CrawledPage()
            {
                Url = url,
                Html = html,
                Content = html, // TODO extract plain text
                CrawledAt = DateTime.UtcNow,
            };
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null; //TODO error handling
        }
    }
    
}