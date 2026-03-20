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
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed":
                case "-s": if (i + 1 < args.Length) seedUrls.Add(args[++i]); break;
                case "--iterations":
                case "-i": if (i + 1 < args.Length) iterations = int.Parse(args[++i]); break;
                case "--delay":
                case "-d": if (i + 1 < args.Length) delay = int.Parse(args[++i]); break;
                case "--cooldown":
                case "-c": if (i + 1 < args.Length) cooldown = int.Parse(args[++i]); break;
                case "--quiet":
                case "-q": Log.MakeQuieter(); break;     
            }
        }
        
        config = new CrawlerConfig(delay, iterations, cooldown);
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