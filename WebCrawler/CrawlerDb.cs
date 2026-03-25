using System.Net;
using SQLite;

namespace WebCrawler;

public class CrawlerDb
{
    private SQLiteAsyncConnection? _database;

    private async Task Init()
    {
        if (_database is not null)
            return;

        _database = new SQLiteAsyncConnection(Constants.DatabasePath);

        await _database.CreateTableAsync<CrawledPage>();
    }

    public async Task<int> SavePageAsync(CrawledPage page)
    {
        await Init();

        var existing = await _database!.Table<CrawledPage>()
            .Where(p => p.Url == page.Url)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            page.Id = existing.Id;
            return await _database.UpdateAsync(page);
        }
        
        return await _database.InsertAsync(page);
    }

    public async Task<List<CrawledPage>> GetPagesAsync()
    {
        await Init();

        return await _database!.Table<CrawledPage>().ToListAsync();
    }

    public async Task<CrawledPage> GetPageByUrlAsync(string url)
    {
        await Init();

        return await _database!.Table<CrawledPage>()
            .Where(p => p.Url == url)
            .FirstOrDefaultAsync();
    }
}


 // ===== SQLite Table(s) ===== //

[Table("Pages")]
public class CrawledPage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Unique, MaxLength(500)]
    public string? Url { get; set; }
    
    public string? Title { get; set; }
    
    public HttpStatusCode? StatusCode { get; set; }
    
    public string? TextContent { get; set; }
    
    public string? RawHtml { get; set; }
    
    public DateTime CrawledAt { get; set; }
}
