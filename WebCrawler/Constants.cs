namespace WebCrawler;

public static class Constants
{
    private const string DatabaseFilename = "Scorpio.db3";
    private const string SavestateFilename = "ScorpioSavestate.json";
    
    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), 
            DatabaseFilename);

    public static string SavestatePath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), 
            SavestateFilename);
    
    public const string CrawlerUserAgent = "ScorpioWebCrawler/indev (educational project; sinad04.prog@hotmail.com; Sinad04 on github)";
}