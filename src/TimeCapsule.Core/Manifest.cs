using System.Text.Json;

namespace TimeCapsule;

/// <summary>One snapshot of the session: everything needed to bring it back.</summary>
public sealed class Manifest
{
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
    public string Host { get; set; } = "";
    public List<ProcessEntry> Processes { get; set; } = new();
    public List<WindowEntry> Windows { get; set; } = new();

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static Manifest Load(string path) =>
        JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"Not a time-capsule manifest: {path}");

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options) + Environment.NewLine);
}

public sealed class ProcessEntry
{
    public int Pid { get; set; }
    public string Exe { get; set; } = "";
    public string[] Args { get; set; } = Array.Empty<string>();
    public string? Cwd { get; set; }
}

public sealed class WindowEntry
{
    public int Pid { get; set; }
    public string Title { get; set; } = "";
    public string Class { get; set; } = "";
    public bool Maximized { get; set; }
    public int Desktop { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
