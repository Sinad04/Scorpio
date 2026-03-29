using SQLite;

namespace SearchEngine;

class SearchEngine
{
    public static List<IndexedPage> SearchFor(string searchInput)
    {
        var tokens = Tokenizer.Tokenize(searchInput);
        var indexer = new Indexer();
        indexer.BuildInvertedIndex();

        var pageIds = indexer.SearchQuery(tokens);

        Console.WriteLine($"Page IDs List: Length {pageIds.Count}");
        
        var resultPages = new List<IndexedPage>();
        using var connection = new SQLiteConnection(Constants.CorpusPath);
        foreach (var id in pageIds)
        {
            var pageQuery = connection.Query<IndexedPage>($"SELECT Url, Title, TextContent AS Description FROM Pages WHERE Id = {id}");
            pageQuery?.ForEach(page => page.Description = TruncateContent(page.Description));
            pageQuery?.ForEach(page => resultPages.Add(page));
        }

        return resultPages;
    }

    
    
    private static string TruncateContent(string content)
    {
        return content.Substring(0, (int) Math.Floor(content.Length * 0.2)) + "..."; // TODO better implementation
    }
    
    static void Main(string[] args)
    {
        var results = SearchFor("android");
        
        Console.WriteLine(results.Count);
        
        var i = 0;
        foreach (var page in results)
        {
            Console.WriteLine("========================================");
            Console.WriteLine($"RESULT {++i}: {page.Url}");
            Console.WriteLine($"TITLE: {page.Title}");
            Console.WriteLine($"DESCRIPTION: {page.Description}");
            Console.WriteLine("========================================");
        }
    }
}

public class IndexedPage
{
    public string Url { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
}