using System.Net;
using RobotsTxtParser;
using System.Text.Json;
using HtmlAgilityPack;

namespace WebCrawler;

public record CrawlerConfig (int FallBackDelayInSeconds, int MaxCrawlIterations, int BaseCooldownInSeconds, Tuple<int, int> MaxRequestRate);

public class Crawler
{
    private readonly CrawlerConfig _config;
    private readonly HttpClient _client = new();
    private readonly RobotsCache _robotsCache;
    
    private readonly LinkFrontier _frontier;
    private readonly CrawlerDb _database = new();
    private readonly RequestRateMonitor _requestRateMonitor;
    
    private readonly Dictionary<string, DateTime> _lastAccessedCache = new();
    private readonly Dictionary<string, TimeSpan> _domainCooldownCache = new();
    
    
    private int _crawlTimes = 0;

    public Crawler(List<string>? urls, List<string>? visited, CrawlerConfig config)
    {
        _frontier = new LinkFrontier(urls, visited);
        _config = config;
        _requestRateMonitor =
            new RequestRateMonitor(TimeSpan.FromSeconds(_config.MaxRequestRate.Item1), _config.MaxRequestRate.Item2);
        _robotsCache = new RobotsCache(_client, _requestRateMonitor);
    }
    
    
    public async Task CrawlAsync(CancellationToken ctoken)
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.CrawlerUserAgent); // Scorpio is benign and identifies itself.
        _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,de;q=0.5");
        
        while (!ctoken.IsCancellationRequested)
        {
            if (_requestRateMonitor.IsRateExceeded()) throw new OperationCanceledException("Request Rate exceeded. Check if the configuration is too aggressive for the rate window. Otherwise this is very possibly from a bug in the code.");
            if (_frontier.TryGetNextUrl(out var normalizedNextUrl))
            {
                if (_crawlTimes >= _config.MaxCrawlIterations) throw new OperationCanceledException($"Maximum Crawl Iterations ({_config.MaxCrawlIterations}) reached.");
                
                Log.Info($"Crawling {normalizedNextUrl}.");
                var baseUrl = Util.GetBaseUrl(normalizedNextUrl);
                
                // Check if the Crawler must not send requests to this domain at the moment due to an imposed cooldown (e.g. from 429) 
                Log.Info($"Checking if Crawler has cooldown on: {baseUrl}.");
                var cooldown = _domainCooldownCache.GetValueOrDefault(baseUrl, TimeSpan.Zero);
                if ((_lastAccessedCache.TryGetValue(baseUrl, out var lastAccessedTime)
                     && lastAccessedTime.Add(cooldown) > DateTime.UtcNow)) 
                { Log.Info($"Crawler has cooldown {cooldown} on {baseUrl} with {cooldown - DateTime.UtcNow.Subtract(lastAccessedTime)} remaining. Skipping URL."); continue; }
                
                // Retrieve Robots.txt either from Cache or via HTTP request.
                var robots = await _robotsCache.TryGetRobotsAsync(baseUrl, ctoken); 
                if (!robots.IsPathAllowed(Constants.CrawlerUserAgent, Util.GetPath(normalizedNextUrl))) 
                { Log.Info($"Crawler not allowed on {normalizedNextUrl}. Skipping URL."); continue; }
                
                // URL allowed according to respective Robots.txt
                await EnforceRequestDelayAsync(robots, baseUrl, ctoken);
                
                var page = await FetchPageAsync(normalizedNextUrl, ctoken);
                _lastAccessedCache[baseUrl] = DateTime.UtcNow;
                _requestRateMonitor.RecordRequest();
                
                await _database.SavePageAsync(page);

                var links = Util.ExtractLinks(normalizedNextUrl, page.RawHtml);
                foreach (var link in links) AddLinkToFrontier(link);
                
                _crawlTimes++;
            }
            else
            {
                throw new OperationCanceledException("Link frontier empty. Please provide seed url.");
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
        
        var diff = now.Subtract(lastAccessed); // How much of the Crawl Delay has technically already passed since the last server response.
        
        var delay = flatDelay.Subtract(diff);
        
        delay = (delay > TimeSpan.Zero)
            ? delay.Add(TimeSpan.FromMilliseconds(Util.Random.Next(0, 2000))) // Adding a little variance to make it more human-like (only add to respect the lower bound).
            : TimeSpan.Zero;
        
        Log.Info($"Waiting {delay.TotalSeconds} seconds (from flat amount {flatDelay.TotalSeconds})");
        await Task.Delay(delay, ctoken);
    }
    
    private async Task<CrawledPage> FetchPageAsync(string url, CancellationToken ctoken)
    {
        try
        {
            Log.Info($"Attempting to fetch {url}.");
            var httpResponse = await _client.GetAsync(url, ctoken);
            var html = "";
            var baseUrl = Util.GetBaseUrl(url);
            
            if (httpResponse.IsSuccessStatusCode)
            {
                _domainCooldownCache.Remove(baseUrl);
                html = await httpResponse.Content.ReadAsStringAsync(ctoken);
                Log.Info($"Successfully fetched document. Content (truncated): {html.Substring(0, 80)}");
            }
            else if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Log.Warn($"Got 429 Too Many Requests. Imposing cooldown on domain {baseUrl}.");
                var retryAfter = httpResponse?.Headers?.RetryAfter?.Delta;

                if (retryAfter is not null)
                    _domainCooldownCache[baseUrl] = retryAfter.Value;
                else if (_domainCooldownCache.ContainsKey(baseUrl))
                    _domainCooldownCache[baseUrl] *= 2;
                else 
                    _domainCooldownCache[baseUrl] = TimeSpan.FromSeconds(_config.BaseCooldownInSeconds);

                Log.Info($"Will retry domain {baseUrl} after {_domainCooldownCache[baseUrl]} (at the earliest).");
            }

            return new CrawledPage()
            {
                Url = url,
                StatusCode = httpResponse?.StatusCode,
                Title = Util.ExtractTitle(html),
                RawHtml = html,
                TextContent = Util.ExtractText(html),
                CrawledAt = DateTime.UtcNow,
            };
        }
        catch (Exception e)
        {
            Log.Warn($"An error occurred while fetching {url}: {e.Message}");
            return new CrawledPage() { CrawledAt = DateTime.UtcNow }; // Empty table entry.
        }
    }

    public string GetFrontierAsJsonString()
    {
        return JsonSerializer.Serialize(_frontier);
    }
    
}