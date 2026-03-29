using System.Text.RegularExpressions;

namespace SearchEngine;

public class Tokenizer
{
    public static List<string> Tokenize(string document)
    {
        var matches = Regex.Matches(document, @"\b[\w-]+\b");
        return matches.Select(m => m.Value.ToLowerInvariant()).ToList();
    }
    
    
}