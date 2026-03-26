using System.Text.Json;
using WebCrawler;

class Program
{

    private static async Task Main(string[] args)
    {
        ParseCliArguments(args, out var urls, out var config);
        
        var cts = new CancellationTokenSource();
        var ctoken = cts.Token;

        Console.CancelKeyPress += (sender, e) =>
        { 
            Log.ImportantInfo("Cancellation requested. Shutting down..");

            cts.Cancel();
            e.Cancel = true; // Do not terminate immediately.
        };
        
        var linkFrontier = ReadSaveState();
        var crawler = new Crawler(linkFrontier?.Urls, linkFrontier?.Visited, config);

        foreach (var seedUrl in urls) Console.WriteLine(seedUrl);
        foreach (var seedUrl in urls) crawler.AddLinkToFrontier(seedUrl); 
        
        try
        {
            await crawler.CrawlAsync(ctoken);
        }
        catch (OperationCanceledException oce)
        {
            Log.ImportantInfo($"Crawler stopped due to: {oce.Message} Saving state...");
            File.WriteAllText(Constants.SavestatePath, crawler.GetFrontierAsJsonString());
        }
    }

    private static void ParseCliArguments(string[] args, out List<string> seedUrls, out CrawlerConfig config)
    {
        seedUrls = new();
        var delay = 5;
        var iterations = int.MaxValue;
        var cooldown = 10;
        var windowSize = 10;
        var maxRequestsInWindow = 20;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed":
                case "-s":
                    while (args.Length > ++i && !args[i].StartsWith('-')) { seedUrls.Add(args[i]); }
                    i--; break;
                case "--iterations":
                case "-i": if (i + 1 < args.Length) iterations = int.Parse(args[++i]); break;
                case "--delay":
                case "-d": if (i + 1 < args.Length) delay = int.Parse(args[++i]); break;
                case "--cooldown":
                case "-c": if (i + 1 < args.Length) cooldown = int.Parse(args[++i]); break;
                case "--quiet":
                case "-q": Log.MakeQuieter(); break;
                case "--rate":
                case "-r": if (i + 2 < args.Length && !args[i+1].StartsWith('-') && !args[i+2].StartsWith('-')) 
                    windowSize = int.Parse(args[++i]); maxRequestsInWindow = int.Parse(args[++i]); break;
                default:
                    Log.Error("Invalid argument(s)."); break;
            }
        }

        Console.WriteLine($"{windowSize}, {maxRequestsInWindow}");
        
        config = new CrawlerConfig(delay, iterations, cooldown, new Tuple<int, int>(windowSize, maxRequestsInWindow));
    }

    private static LinkFrontier? ReadSaveState()
    {
        try
        {
            var json = File.ReadAllText(Constants.SavestatePath);
            return JsonSerializer.Deserialize<LinkFrontier>(json);
        }
        catch (Exception ex) when (ex is FileNotFoundException || ex is JsonException)
        {
            switch (ex) 
            {
                case FileNotFoundException:
                    Log.ImportantInfo($"No save state found. Starting new frontier.."); break;
                case JsonException:
                    Log.Warn($"Could not read save state file because the JSON was invalid. Starting new frontier.."); break;
            }

            return null;
        }
    }
}