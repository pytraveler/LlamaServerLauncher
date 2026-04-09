using System.Text;

namespace LlamaServerLauncher.Models;

public static class CommandLineParser
{
    public static string NormalizeSpecialCharacters(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        return input
            .Replace("\\t", " ")
            .Replace("\\n", " ")
            .Replace("\\r", " ")
            .Replace("\t", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");
    }

    public static List<string> ParseArguments(string args)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(args))
            return result;

        var sb = new StringBuilder();
        bool inQuotes = false;
        char? quoteChar = null;

        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];

            if (!inQuotes && (c == '"' || c == '\''))
            {
                inQuotes = true;
                quoteChar = c;
            }
            else if (inQuotes && c == quoteChar)
            {
                inQuotes = false;
                quoteChar = null;
            }
            else if (!inQuotes && c == ' ')
            {
                if (sb.Length > 0)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
            result.Add(sb.ToString());

        return result;
    }

    public static Dictionary<string, string?> GetArgumentValues(List<string> args)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];

            if (arg.StartsWith("-"))
            {
                if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
                {
                    result[arg] = args[i + 1];
                    i++;
                }
                else
                {
                    result[arg] = null;
                }
            }
        }

        return result;
    }

    public static HashSet<string> GetArgumentFlags(List<string> args)
    {
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var arg in args)
        {
            if (arg.StartsWith("-"))
            {
                flags.Add(arg);
            }
        }
        
        return flags;
    }
}