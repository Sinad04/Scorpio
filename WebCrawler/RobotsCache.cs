using System.Collections.Concurrent;
using RobotsTxtParser;

namespace WebCrawler;

public class RobotsCache
{
    private readonly ConcurrentDictionary<string, Robots> _robotsCache = new();
    private readonly HttpClient _client;

    public RobotsCache(HttpClient client) { _client = client; }
    
    public async Task<Robots> TryGetRobotsAsync(string baseUrl)
    {
        Log.Info($"Querying robots cache for {baseUrl}");
        if (_robotsCache.TryGetValue(baseUrl, out var robots)) return robots;
        Log.Info($"{baseUrl} not found in robots cache. Attempting to retrieve robots.txt from web server.");
        var robotsString = await _client.GetStringAsync($"{baseUrl}/robots.txt"); // TODO handle unsuccessful request
        
        robots = new Robots(robotsString);
        _robotsCache.TryAdd(baseUrl, robots); // TODO maybe a way to avoid spamming robots.txt requests should caching fail. though idk if it ever would that badly
        return robots; 
    }
}

