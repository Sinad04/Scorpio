namespace WebCrawler;

public static class Constants
{
    private const string DatabaseFilename = "Scorpio.db3";

    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), 
            DatabaseFilename);

    public const string CrawlerUserAgent = "ScorpioWebCrawler/indev (educational project; sinad04.prog@hotmail.com; Sinad04 on github)";
}