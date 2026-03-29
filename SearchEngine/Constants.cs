namespace SearchEngine;

public static class Constants
{
    private const string InvertedIndexFilename = "ScorpioInvertedIndex.db3";
    private const string CorpusFilename = "ScorpioPageCorpus.db3";
    
    public static string CorpusPath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), 
            CorpusFilename);
    
    public static string InvertedIndexPath =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), 
            InvertedIndexFilename);
}