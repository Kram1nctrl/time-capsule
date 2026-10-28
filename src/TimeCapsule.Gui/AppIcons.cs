using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;

namespace TimeCapsule.Gui;

/// <summary>One row in the saved-session list.</summary>
public sealed record SessionItem(string Name, string Path, string Subtitle);

/// <summary>One row in the "apps that will reopen" list.</summary>
public sealed record AppRow(string Name, Bitmap? Icon, ProcessEntry Process);

/// <summary>One row in the "windows that will be put back" list.</summary>
public sealed record WindowRow(string Title, string Detail, WindowEntry Window);

/// <summary>Display name and icon for an executable, read from its .desktop entry.</summary>
static class AppIcons
{
    static readonly string[] Themes = { "hicolor", "Mint-Y", "Adwaita", "Papirus", "Yaru", "breeze" };
    static readonly string[] Sizes = { "128x128", "64x64", "48x48", "256x256", "32x32" };
    static readonly string[] Kinds = { "apps", "mimetypes" };

    static Dictionary<string, (string? Name, string? Icon)>? _entries;
    static readonly Dictionary<string, Bitmap?> Bitmaps = new();
    static readonly Dictionary<string, (string Name, Bitmap? Icon)> Results = new();

    public static (string Name, Bitmap? Icon) Describe(string exePath)
    {
        var key = Path.GetFileName(exePath);
        if (Results.TryGetValue(key, out var cached)) return cached;

        var entries = _entries ??= ReadDesktopEntries();
        (string? Name, string? Icon) entry = default;

        // Firefox runs as firefox-bin while its .desktop entry is called firefox.
        if (!entries.TryGetValue(key, out entry) && key.EndsWith("-bin"))
            entries.TryGetValue(key[..^4], out entry);

        var name = entry.Name ?? (key.EndsWith(".desktop") ? key[..^8] : key);
        var icon = Load(entry.Icon ?? "application-x-executable") ?? Load("application-x-executable");

        var result = (name, icon);
        Results[key] = result;
        return result;
    }

    static Dictionary<string, (string? Name, string? Icon)> ReadDesktopEntries()
    {
        var entries = new Dictionary<string, (string? Name, string? Icon)>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] dirs =
        {
            Path.Combine(home, ".local/share/applications"),
            "/usr/local/share/applications",
            "/usr/share/applications",
            "/var/lib/flatpak/exports/share/applications",
            Path.Combine(home, ".local/share/flatpak/exports/share/applications"),
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.desktop"))
            {
                try
                {
                    string? name = null, icon = null, exec = null;
                    var inEntry = false;

                    foreach (var line in File.ReadLines(file))
                    {
                        if (line.StartsWith('['))
                        {
                            inEntry = line == "[Desktop Entry]";
                            continue;
                        }

                        if (!inEntry) continue;
                        if (line.StartsWith("Name=")) name ??= line[5..];
                        else if (line.StartsWith("Icon=")) icon ??= line[5..];
                        else if (line.StartsWith("Exec=")) exec ??= line[5..];
                    }

                    var bin = ExecutableOf(exec);
                    if (bin != null && !entries.ContainsKey(bin)) entries[bin] = (name, icon);
                }
                catch
                {
                    // unreadable .desktop file: ignore it
                }
            }
        }

        return entries;
    }

    static string? ExecutableOf(string? exec)
    {
        if (exec == null) return null;

        var tokens = exec.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;
        if (tokens.Length > 0 && tokens[0] == "env") index = 1;

        for (; index < tokens.Length; index++)
        {
            if (tokens[index].Contains('=')) continue; // env VAR=value
            return Path.GetFileName(tokens[index].Trim('"'));
        }

        return null;
    }

    static Bitmap? Load(string icon)
    {
        if (Bitmaps.TryGetValue(icon, out var cached)) return cached;

        Bitmap? bitmap = null;
        var path = FindIcon(icon);
        if (path != null)
        {
            try { bitmap = new Bitmap(path); } catch { }
        }

        Bitmaps[icon] = bitmap;
        return bitmap;
    }

    static string? FindIcon(string icon)
    {
        if (Path.IsPathRooted(icon)) return File.Exists(icon) ? icon : null;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots =
        {
            "/usr/share/icons",
            "/usr/local/share/icons",
            Path.Combine(home, ".local/share/icons"),
            Path.Combine(home, ".icons"),
        };

        foreach (var root in roots)
        foreach (var size in Sizes)
        foreach (var kind in Kinds)
        foreach (var theme in Themes)
        {
            var path = Path.Combine(root, theme, size, kind, icon + ".png");
            if (File.Exists(path)) return path;
        }

        var pixmap = Path.Combine("/usr/share/pixmaps", icon + ".png");
        return File.Exists(pixmap) ? pixmap : null;
    }
}
