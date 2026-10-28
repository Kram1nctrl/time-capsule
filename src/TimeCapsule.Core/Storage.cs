namespace TimeCapsule;

/// <summary>Where snapshots live and how their names resolve.</summary>
public static class Storage
{
    public static string SessionsDir { get; } = Path.Combine(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
        "time-capsule", "sessions");

    /// <summary>Saved snapshots, newest first.</summary>
    public static string[] List() =>
        Directory.Exists(SessionsDir)
            ? Directory.GetFiles(SessionsDir, "*.json").OrderByDescending(f => f).ToArray()
            : Array.Empty<string>();

    /// <summary>A tag reduced to file-name-safe characters.</summary>
    public static string CleanTag(string tag)
    {
        var clean = new string(tag.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return clean.Length == 0 ? "session" : clean;
    }

    /// <summary>Path for a new snapshot: tag-date, with a time suffix if that day is taken.</summary>
    public static string NewPath(string tag)
    {
        Directory.CreateDirectory(SessionsDir);
        var path = Path.Combine(SessionsDir, $"{tag}-{DateTime.Now:yyyy-MM-dd}.json");
        return File.Exists(path)
            ? Path.Combine(SessionsDir, $"{tag}-{DateTime.Now:yyyy-MM-dd-HHmmss}.json")
            : path;
    }

    /// <summary>Files a user-supplied name or path can mean: exact file, exact name, or a unique prefix.</summary>
    public static string[] Matches(string target)
    {
        if (File.Exists(target)) return new[] { target };

        var exact = Path.Combine(SessionsDir, target.EndsWith(".json") ? target : target + ".json");
        if (File.Exists(exact)) return new[] { exact };

        return Directory.Exists(SessionsDir)
            ? Directory.GetFiles(SessionsDir, target + "*.json")
            : Array.Empty<string>();
    }
}
