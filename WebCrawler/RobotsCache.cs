using System.Collections.Concurrent;
using System.Net;
using RobotsTxtParser;

namespace WebCrawler;

public class RobotsCache
{
    private readonly ConcurrentDictionary<string, Robots> _robotsCache = new();
    private readonly HttpClient _client;
    
    public RobotsCache(HttpClient client) { _client = client; }
    
    public async Task<Robots> TryGetRobotsAsync(string baseUrl, CancellationToken ctoken)
    {
        Log.Info($"Querying robots cache for {baseUrl}");
        if (_robotsCache.TryGetValue(baseUrl, out var cachedRobots)) return cachedRobots;
        Log.Info($"{baseUrl} not found in robots cache. Attempting to retrieve robots.txt from web server.");

        Robots? robots = null;
        
        var robotsResponseMessage = await _client.GetAsync($"{baseUrl}/robots.txt", ctoken);

        if (robotsResponseMessage.IsSuccessStatusCode)
        {
            var robotsString = await robotsResponseMessage.Content.ReadAsStringAsync(ctoken);
            robots = new Robots(robotsString);
        } 
        else
        {
            switch (robotsResponseMessage.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    Log.ImportantInfo($"No robots.txt found at {baseUrl}. Assuming every path is allowed.");
                    robots = new Robots(""); // If no robots.txt is provided by the domain, then assume Crawler is allowed everywhere.
                    break;
                case HttpStatusCode.TooManyRequests:
                    return new Robots("User-agent: *\r\nDisallow: \\"); // If client is temporarily rate-limited, do not cache.
            }
        }
        if (robots is null)
        {
            Log.Warn($"Could not acquire robots.txt for {baseUrl}. Assuming Crawler not allowed on domain.");
            robots = new Robots("User-agent: *\r\nDisallow: \\"); // If robots.txt couldn't be acquired, to be safe, assume for this domain that Crawler isn't allowed.
        }
        
        _robotsCache.TryAdd(baseUrl, robots);
        return robots;
    }
}

