using System.Collections.Concurrent;

namespace WebCrawler;

/*
 *  Dedicated Data Structure to hold all discovered Urls, keeping track of which
 *  have and have not yet been visited.
 *
 *  TODO implement:
 *    - politeness (robots.txt, per-domain delays)
 *    - prioritization based on PageRank / freshness / depth
 *    - ...
 */

public class LinkFrontier
{
    // Store Urls that have been found and are yet to be visited.
    private readonly ConcurrentQueue<string> _urls = new();
    // Closest approximation to a "Concurrent Hash Set", to hold Urls that have been visited.
    private readonly ConcurrentDictionary<string, byte> _visited = new(); 

    // Adds the Url to the Frontier iff it has not been visited before.
    // Url should be normalized BEFORE it is passed to this method.
    public void AddIfNew(string url)
    {
        if (_visited.TryAdd(url, 0))
        { 
            _urls.Enqueue(url);
        }
    }

    // Attempt to grab a Url from the Queue. Returns false if there's nothing available.
    public bool TryGetNextUrl(out string? nextUrl)
    {
        return _urls.TryDequeue(out nextUrl);
    }
    
}