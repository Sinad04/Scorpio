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
        var queriedBaseUrl = baseUrl;
        var redirects = 0;
        
        while (robots is null && redirects <= 3)
        {
            using var robotsResponseMessage = await _client.GetAsync($"{queriedBaseUrl}/robots.txt", ctoken);

            if (robotsResponseMessage.IsSuccessStatusCode)
            {
                Log.Info($"{queriedBaseUrl} has robots.txt.");
                var robotsString = await robotsResponseMessage.Content.ReadAsStringAsync(ctoken);
                robots = new Robots(robotsString);
            } 
            else
            {
                switch (robotsResponseMessage.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        Log.Info($"No robots.txt found at {baseUrl}. Checking for redirect.");

                        var redirectedBaseUrlString = await CheckForRedirectedBaseUrlAsync(baseUrl, ctoken);
                        
                        if (string.IsNullOrWhiteSpace(redirectedBaseUrlString) && redirectedBaseUrlString != baseUrl) 
                        { 
                            queriedBaseUrl = redirectedBaseUrlString; 
                            redirects++; 
                            continue;
                        }
                        Log.Warn($"No robots.txt found for {baseUrl}. Assuming Crawler allowed everywhere.");
                        robots ??= new Robots(""); // If no robots.txt is provided by the domain, then assume Crawler is allowed everywhere.
                        break;
                    
                    case HttpStatusCode.TooManyRequests:
                        return new Robots(""); // If client is temporarily rate-limited, do not cache.
                }
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

    private async Task<string> CheckForRedirectedBaseUrlAsync(string baseUrl, CancellationToken ctoken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, baseUrl);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ctoken);
        
        var redirectedBaseUrl = response?.RequestMessage?.RequestUri;
        return Util.NormalizeUrl(redirectedBaseUrl?.ToString() ?? baseUrl);
        
    }
}

