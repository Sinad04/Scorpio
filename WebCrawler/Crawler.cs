using System.Net;
using HtmlAgilityPack;
using RobotsTxtParser;

namespace WebCrawler;

public record CrawlerConfig (int FallBackDelayInSeconds, int MaxCrawlIterations);

public class Crawler
{
    private static readonly HttpClient Client = new();
    private readonly LinkFrontier _frontier = new();
    private readonly CrawlerDb _database = new();
    private readonly RobotsCache _robotsCache = new(Client);
    private readonly Dictionary<string, DateTime> _lastAccessedCache = new();
    private readonly CrawlerConfig _config = new(8, 5);
    
    private int _crawlTimes = 0;
    
    public async Task CrawlAsync(CancellationToken ctoken)
    {
        Client.DefaultRequestHeaders.Add("User-Agent", Constants.CrawlerUserAgent); // Scorpio is benign and identifies itself.
        
        while (!ctoken.IsCancellationRequested) 
        {
            if (_frontier.TryGetNextUrl(out var normalizedNextUrl))
            {
                if (_crawlTimes >= _config.MaxCrawlIterations) throw new OperationCanceledException("Maximum Crawl Iterations reached.");
                
                Console.WriteLine($"Crawling {normalizedNextUrl}");
                var baseUrl = Util.GetBaseUrl(normalizedNextUrl);
                var robots = await _robotsCache.TryGetRobotsAsync(baseUrl); // Retrieve Robots.txt either from Cache or via HTTP request.

                if (!robots.IsPathAllowed(Constants.CrawlerUserAgent, Util.GetPath(normalizedNextUrl))) 
                { Console.WriteLine($"Crawler not allowed on {normalizedNextUrl}."); continue; }
                
                // URL allowed according to respective Robots.txt
                await EnforceRequestDelayAsync(robots, baseUrl, ctoken);
                
                var page = await FetchPageAsync(normalizedNextUrl, ctoken);
                _lastAccessedCache[baseUrl] = DateTime.UtcNow;
                
                await _database.SavePageAsync(page);
                    
                var links = Util.ExtractLinks(normalizedNextUrl, page.Html);
                foreach (var link in links) AddLinkToFrontier(link);

                _crawlTimes++;
            }
            else
            {
                await Task.Delay(100, ctoken);
            }
        }
    }
    
    // Debug
    public void AddLinkToFrontier(string link)
    {
        var normalizedLink = Util.NormalizeUrl(link);
        _frontier.AddIfNew(normalizedLink);
    }

    private async Task EnforceRequestDelayAsync(Robots robots, string baseUrl, CancellationToken ctoken)
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
        await Task.Delay(delay, ctoken);
    }
    
    private async Task<CrawledPage> FetchPageAsync(string url, CancellationToken ctoken)
    {
        try
        {
            Console.WriteLine($"Fetching {url}");
            var httpResponse = await Client.GetAsync(url, ctoken);
            var html = await httpResponse.Content.ReadAsStringAsync(ctoken);
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