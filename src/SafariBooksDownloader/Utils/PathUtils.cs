using System.Text.RegularExpressions;

namespace SafariBooksDownloader.Utils;

internal static class PathUtils
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    public static string CleanFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "unknown";
        
        // Replace invalid characters with underscores
        var cleaned = fileName;
        foreach (var c in InvalidFileNameChars)
        {
            cleaned = cleaned.Replace(c, '_');
        }
        
        // Remove multiple consecutive underscores
        cleaned = Regex.Replace(cleaned, "_+", "_");
        
        // Trim underscores from start and end
        cleaned = cleaned.Trim('_', ' ', '.');
        
        // Ensure we don't have an empty string
        if (string.IsNullOrEmpty(cleaned)) return "unknown";
        
        // Limit length to reasonable size
        if (cleaned.Length > 100)
        {
            cleaned = cleaned[..100];
        }
        
        return cleaned;
    }

    public static string CleanId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "item";
        
        // Replace invalid XML ID characters with underscores
        var cleaned = Regex.Replace(id, @"[^a-zA-Z0-9_-]", "_");
        
        // Ensure it starts with a letter or underscore
        if (!char.IsLetter(cleaned[0]) && cleaned[0] != '_')
        {
            cleaned = "item_" + cleaned;
        }
        
        // Remove multiple consecutive underscores
        cleaned = Regex.Replace(cleaned, "_+", "_");
        
        // Trim underscores from end
        cleaned = cleaned.TrimEnd('_');
        
        // Ensure we don't have an empty string
        if (string.IsNullOrEmpty(cleaned)) return "item";
        
        return cleaned;
    }
}