using System.Net;
using RobotsTxtParser;
using System.Text.Json;

namespace WebCrawler;

public record CrawlerConfig (int FallBackDelayInSeconds, int MaxCrawlIterations, int BaseCooldownInSeconds);

public class Crawler
{
    private readonly HttpClient _client = new();
    private readonly RobotsCache _robotsCache;
    
    private readonly LinkFrontier _frontier;
    private readonly CrawlerDb _database = new();
    
    private readonly Dictionary<string, DateTime> _lastAccessedCache = new();
    private readonly Dictionary<string, TimeSpan> _domainCooldownCache = new();
    private readonly CrawlerConfig _config = new(8, 5, 30);
    
    private int _crawlTimes = 0;

    public Crawler(List<string>? urls, List<string>? visited)
    {
        _frontier = new LinkFrontier(urls, visited);
        _robotsCache = new RobotsCache(_client);
    }
    
    
    public async Task CrawlAsync(CancellationToken ctoken)
    {
        _client.DefaultRequestHeaders.Add("User-Agent", Constants.CrawlerUserAgent); // Scorpio is benign and identifies itself.
        
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

                // Check if the Crawler must not send requests to this domain at the moment due to an imposed cooldown (e.g. from 429) 
                Console.WriteLine($"Check if Crawler has cooldown on: {normalizedNextUrl}.");
                var cooldown = _domainCooldownCache.GetValueOrDefault(baseUrl, TimeSpan.Zero);
                if ((_lastAccessedCache.TryGetValue(baseUrl, out var lastAccessedTime)
                     && lastAccessedTime.Add(cooldown) > DateTime.UtcNow)) 
                { Console.WriteLine($"Crawler has cooldown {cooldown} on {normalizedNextUrl}. {cooldown - DateTime.UtcNow.Subtract(lastAccessedTime)} remaining."); continue; }
                
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
            var httpResponse = await _client.GetAsync(url, ctoken);
            var html = "";
            var baseUrl = Util.GetBaseUrl(url);
            
            if (httpResponse.IsSuccessStatusCode)
            {
                _domainCooldownCache.Remove(baseUrl);
                html = await httpResponse.Content.ReadAsStringAsync(ctoken);
                Console.WriteLine($"Successfully fetched {html.Substring(0, 50)}");
            }
            else if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine("Got 429 Too Many Requests");
                var retryAfter = httpResponse?.Headers?.RetryAfter?.Delta;
                
                if (retryAfter is not null)
                    _domainCooldownCache[baseUrl] = retryAfter.Value;
                else if (_domainCooldownCache.ContainsKey(baseUrl))
                    _domainCooldownCache[baseUrl] *= 2;
                else 
                    _domainCooldownCache[baseUrl] = TimeSpan.FromSeconds(_config.BaseCooldownInSeconds);
            }

            return new CrawledPage()
            {
                Url = url,
                Html = html,
                ResponseCode = httpResponse?.StatusCode,
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

    public string GetFrontierAsJsonString()
    {
        return JsonSerializer.Serialize(_frontier);
    }
    
}