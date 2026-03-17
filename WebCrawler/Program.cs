class Program
{
    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("Cancellation requested. Shutting down..");

            cts.Cancel();
            e.Cancel = true; // Do not terminate immediately.
        };

        var crawler = new WebCrawler.Crawler();
        crawler.AddLinkToFrontier(""); // put DEBUG seed link in here
        try
        {
            await crawler.CrawlAsync(token);
        }
        catch (OperationCanceledException oce)
        {
            Console.WriteLine($"Crawler stopped due to: {oce.Message} Saving state...");
            // TODO Save frontier, visited set, etc.
        }
    }
}