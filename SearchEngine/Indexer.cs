using System.Text;
using WebCrawler;
using SQLite;

namespace SearchEngine;


public class Indexer
{

    private SQLiteConnection? _database;

    private void Init()
    {
        if (_database is not null)
            return;

        _database = new SQLiteConnection(Constants.InvertedIndexPath);

        _database.Execute(
            "CREATE TABLE IF NOT EXISTS InvertedIndex(Token TEXT NOT NULL, DocumentId INTEGER NOT NULL, TermFrequency INTEGER, Url TEXT, PRIMARY KEY (Token, DocumentId))");
    }
    
    public List<int> SearchQuery(List<string> queryTokens)
    {
        Init();

        foreach (var token in queryTokens) Console.WriteLine($"Token: {token}");
        
        var searchQueryResults = _database!.Query<SearchQueryResult>(
            $"SELECT DocumentId, SUM(TermFrequency) AS Relevance " + 
            $"FROM InvertedIndex " + 
            $"WHERE Token IN {MakeTokenSetForQuery(queryTokens)} " + 
            $"GROUP BY DocumentId " + 
            $"ORDER BY Relevance DESC " + 
            $"LIMIT 20");
        Console.WriteLine($"Search Query Results: {searchQueryResults.Count}");
        return searchQueryResults.Select(r => r.DocumentId).ToList();
    }

    private string MakeTokenSetForQuery(List<string> tokens)
    {
        var sb = new StringBuilder("(");
        var len = tokens.Count;
        
        for (var i = 0; i < len-1; i++)
        {
            sb.Append($"'{tokens[i]}', ");
        }
        sb.Append($"'{tokens[^1]}')");

        return sb.ToString().Trim();
    }
    
    public void BuildInvertedIndex()
    {
        Init();
        
        using var connection = new SQLiteConnection(Constants.CorpusPath);

        var pages = connection.Query<CrawledPage>("SELECT * FROM Pages LIMIT 80");

        foreach (var page in pages)
        { IndexDocument(page.Id, page.TextContent ?? "This page has no content.", page.Url); }
    }
    
    private void IndexDocument(int documentId, string text, string url)
    {
        Init();
        
        var tokens = Tokenizer.Tokenize(text);
        var tokenCounts = tokens.GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());
        
        foreach (var (token, count) in tokenCounts)
        {
            Console.WriteLine(token);
            _database!.Execute(
                $"INSERT INTO InvertedIndex " + 
                      $"VALUES ('{token}', '{documentId}', '{count}', '{url}') " +
                      $"ON CONFLICT(Token, DocumentId) DO UPDATE SET TermFrequency = {count}");
        }
    }
}

public class SearchQueryResult
{
    public int DocumentId { get; set; }
    public int Relevance { get; set; }
}