using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TimeCapsule;

namespace TimeCapsule.Gui;

/// <summary>Turns the snapshot files on disk into rows for the session lists.</summary>
static class Sessions
{
    public static List<SessionItem> Load() => Storage.List().Select(Describe).ToList();

    public static SessionItem Describe(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        try
        {
            var manifest = Manifest.Load(path);
            var subtitle = $"Saved {When(manifest.CreatedUtc.LocalDateTime)} · "
                + $"{Apps(manifest.Processes.Count)}, {Windows(manifest.Windows.Count)}";
            return new SessionItem(name, path, subtitle);
        }
        catch
        {
            return new SessionItem(name, path, "File could not be read");
        }
    }

    public static string When(DateTime time) =>
        time.Date == DateTime.Today ? $"today at {time:HH:mm}"
        : time.Date == DateTime.Today.AddDays(-1) ? $"yesterday at {time:HH:mm}"
        : time.ToString("d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture);

    public static string Apps(int count) => count == 1 ? "1 app" : $"{count} apps";

    public static string Windows(int count) => count == 1 ? "1 window" : $"{count} windows";
}
