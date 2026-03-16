using System.Net;
using HtmlAgilityPack;
using RobotsTxtParser;

namespace WebCrawler;

public record CrawlerConfig (int FallBackDelayInSeconds);

public class Crawler
{
    private static readonly HttpClient Client = new();
    private readonly LinkFrontier _frontier = new();
    private readonly CrawlerDb _database = new();
    private readonly RobotsCache _robotsCache = new(Client);
    private readonly Dictionary<string, DateTime> _lastAccessedCache = new();
    private readonly CrawlerConfig _config = new(8);
    
    // ========== DEBUG ==========
    private static int _crawlTimes = 30; // How many "crawl iterations" it does before it stops. In the early dev phase I don't really want it going on endlessly yet.
    // ========== ===== ==========
    
    public async Task CrawlAsync()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", Constants.CrawlerUserAgent); // Scorpio is benign and identifies itself.
        
        for (var i = 0; i < _crawlTimes; i++)
        {
            if (_frontier.TryGetNextUrl(out var url))
            {
                Console.WriteLine($"Crawling {url}");
                var baseUrl = Util.GetBaseUrl(url);
                var robots = await _robotsCache.TryGetRobotsAsync(baseUrl); // Retrieve Robots.txt either from Cache or via HTTP request.
                
                if (robots.IsPathAllowed(Constants.CrawlerUserAgent, Util.GetPath(url)))
                {
                    // URL allowed according to respective Robots.txt
                    Console.WriteLine($"crawler allowed on {url}");
                    await EnforceRequestDelayAsync(robots, baseUrl);
                    var page = await FetchPageAsync(url);
                    _lastAccessedCache[baseUrl] = DateTime.UtcNow;
                    await _database.SavePageAsync(page);
                    
                    var links = Util.ExtractLinks(url, page.Html);
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

    private async Task EnforceRequestDelayAsync(Robots robots, string baseUrl)
    {
        var now = DateTime.UtcNow;
        
        var flatDelay = robots.CrawlDelay(Constants.CrawlerUserAgent, TimeSpan.FromSeconds(_config.FallBackDelayInSeconds)); // Robots.txt crawl delay or fallback value.
        
        var lastAccessed = _lastAccessedCache.GetValueOrDefault(baseUrl, now.Subtract(flatDelay)); // If it was never accessed before, pretend the last server response was exactly the flat Crawl Delay ago.
        Console.WriteLine($"Last accessed: {lastAccessed}");
        var diff = now.Subtract(lastAccessed); // How much of the Crawl Delay has technically already passed since the last server response.
        Console.WriteLine($"Difference: {diff}");
        var delay = flatDelay.Subtract(diff);
        
        delay = (delay > TimeSpan.Zero)
            ? delay.Add(TimeSpan.FromMilliseconds(Util.Random.Next(0, 2000))) // Adding a little variance to make it more human-like (only add to respect the lower bound).
            : TimeSpan.Zero;
        
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
                Content = Util.ExtractText(html),
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