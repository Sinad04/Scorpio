using System.Collections.Concurrent;

namespace WebCrawler;

/*
 *  Dedicated Data Structure to hold all discovered Urls, keeping track of which
 *  have and have not yet been visited.
 *
 *  TODO implement:
 *    - prioritization based on PageRank / freshness / depth
 *    - ...
 */

[Serializable]
public class LinkFrontier
{

    public LinkFrontier(List<string>? urls, List<string>? visited)
    {
        _urls = new ConcurrentQueue<string>(urls ?? new List<string>());
        _visited = new ConcurrentDictionary<string, byte>(visited?.ToDictionary(k => k, v => (byte) 0) ?? new Dictionary<string, byte>());
    }
    
    // Store Urls that have been found and are yet to be visited.
    private readonly ConcurrentQueue<string> _urls;
    // Closest approximation to a "Concurrent Hash Set", to hold Urls that have been visited.
    private readonly ConcurrentDictionary<string, byte> _visited;

    // Serializable snapshots of the concurrent data structures.
    public List<string> Urls => _urls.ToList();
    public List<string> Visited => _visited.Keys.ToList();
    
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
    public bool TryGetNextUrl(out string nextUrl)
    {
        return _urls.TryDequeue(out nextUrl);
    }
    
}