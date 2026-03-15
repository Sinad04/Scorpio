using System.Net;
using HtmlAgilityPack;
using RobotsTxtParser;

namespace WebCrawler;

public class Crawler
{
    private static readonly HttpClient Client = new();
    private readonly LinkFrontier _frontier = new();
    private readonly CrawlerDb _database = new();
    private readonly RobotsCache _robotsCache = new(Client);
    
    // ========== DEBUG ==========
    private static int _crawlTimes = 10; // How many "crawl iterations" it does before it stops. In the early dev phase I don't really want it going on endlessly yet.
    // ========== ===== ==========
    
    public async Task CrawlAsync()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", Constants.CrawlerUserAgent); // Scorpio is benign and identifies itself.
        
        for (var i = 0; i < _crawlTimes; i++)
        {
            if (_frontier.TryGetNextUrl(out var url))
            {
                Console.WriteLine($"Crawling {url}");
                var robots = await _robotsCache.TryGetRobotsAsync(Util.GetBaseUrl(url)); // Retrieve Robots.txt either from Cache or via HTTP request.
                
                if (robots.IsPathAllowed(Constants.CrawlerUserAgent, Util.GetPath(url)))
                {
                    // URL allowed according to respective Robots.txt
                    Console.WriteLine($"crawler allowed on {url}");
                    await EnforceRequestDelayAsync(robots);
                    
                    var page = await FetchPageAsync(url);
                    
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

    private async Task EnforceRequestDelayAsync(Robots robots)
    {
        // TODO add a domain last visited cache to avoid unnecessary waiting 
        var flatDelay = robots.CrawlDelay(Constants.CrawlerUserAgent, TimeSpan.FromSeconds(5));
        
        // Adding a little variance to make it more human-like (only add to respect the lower bound):
        var delay = flatDelay.Add(TimeSpan.FromMilliseconds(Util.Random.Next(0, 2000)));
        Console.WriteLine($"Waiting {delay.TotalSeconds} seconds (from {flatDelay.TotalSeconds})");
        await Task.Delay(delay);
    }
    
    private async Task<CrawledPage> FetchPageAsync(string url)
    {
        try
        {
            Console.WriteLine($"Fetching {url}");
            var httpResponse = await Client.GetAsync(url);
            var html = await httpResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Fetched {html.Substring(0, 50)}");
            
            // if (httpResponse.StatusCode is HttpStatusCode.TooManyRequests) DelayPenalty(url); TODO implement reaction to 429
            
            return new CrawledPage()
            {
                Url = url,
                Html = html,
                ResponseCode = httpResponse.StatusCode,
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