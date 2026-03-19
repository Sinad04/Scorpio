namespace WebCrawler;

public static class Log
{
    private static bool _verbose = true;

    public static void MakeQuieter() { _verbose = false; }
    
    public static void Info(string message)
    {
        if (_verbose) Console.WriteLine($"[INFO]: {message}");
    }
    
    public static void ImportantInfo(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[INFO]: {message}");
        Console.ForegroundColor = previous;
    }

    public static void Warn(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine($"[WARNING]: {message}");
        Console.ForegroundColor = previous;
    }
    
    public static void Error(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[ERROR]: {message}");
        Console.ForegroundColor = previous;
        throw new OperationCanceledException("An error has occurred.");
    }
}