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
            Console.WriteLine("Cancellation requested. Shutting down..");

            cts.Cancel();
            e.Cancel = true; // Do not terminate immediately.
        };

        var linkFrontier = ReadSaveState();
        var crawler = new Crawler(linkFrontier?.Urls, linkFrontier?.Visited);
        
        crawler.AddLinkToFrontier("https://www.youtube.com/"); // put DEBUG seed link in here
        
        try
        {
            await crawler.CrawlAsync(ctoken);
        }
        catch (OperationCanceledException oce)
        {
            Console.WriteLine($"Crawler stopped due to: {oce.Message} Saving state...");
            // TODO Save frontier, visited set, etc.

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
        catch (Exception ex) when (ex is JsonException || ex is FileNotFoundException)
        {
            // TODO warn in console
            return null;
        }
    }
}