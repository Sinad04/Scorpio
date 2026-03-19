using System.Text.Json;
using WebCrawler;

class Program
{
    
    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        var ctoken = cts.Token;
        
        Console.CancelKeyPress += (sender, e) =>
        {
            Log.ImportantInfo("Cancellation requested. Shutting down..");

            cts.Cancel();
            e.Cancel = true; // Do not terminate immediately.
        };

        var linkFrontier = ReadSaveState();
        var crawler = new Crawler(linkFrontier?.Urls, linkFrontier?.Visited);
        
        crawler.AddLinkToFrontier("https://httpbin.org/status/429"); // put DEBUG seed link in here
        
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
                case FileNotFoundException: Log.ImportantInfo($"No save state found. Starting with empty frontier.."); break;
                case JsonException: Log.Warn("Could not read save state because the JSON was invalid. Starting with empty frontier.."); break;
            }
            return null;
        }
    }
}