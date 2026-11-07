using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeCapsule;

/// <summary>
/// Reads the tabs of each Firefox window from Firefox's own session store, so a
/// snapshot can reopen the pages instead of only the window.
/// </summary>
public static class Firefox
{
    public static bool IsFirefox(string className) =>
        className.Contains("firefox", StringComparison.OrdinalIgnoreCase);

    /// <summary>Attaches the tabs of each Firefox window to the matching snapshot window.</summary>
    public static void Attach(Manifest manifest)
    {
        var windows = manifest.Windows.Where(window => IsFirefox(window.Class)).ToList();
        if (windows.Count == 0) return;

        List<FirefoxWindow> store;
        try
        {
            store = ReadWindows();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException
            or IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            return; // no readable session store: keep the window-only snapshot
        }

        if (store.Count == 0) return;

        // Pair by the active tab's title first, then by size, so several Firefox
        // windows keep their own tabs.
        var used = new bool[store.Count];
        foreach (var window in windows)
        {
            var best = -1;
            var bestScore = -1;
            for (var i = 0; i < store.Count; i++)
            {
                if (used[i]) continue;
                var score = Score(window, store[i]);
                if (score <= bestScore) continue;
                bestScore = score;
                best = i;
            }

            if (best < 0) break;
            used[best] = true;
            window.Tabs = store[best].Tabs;
        }
    }

    /// <summary>The windows in the newest Firefox session store on this machine.</summary>
    public static List<FirefoxWindow> ReadWindows()
    {
        var path = FindStoreFile();
        if (path == null) return new();

        var json = DecompressMozLz4(File.ReadAllBytes(path));
        var store = JsonSerializer.Deserialize<StoreSession>(json, Options) ?? new();

        var windows = new List<FirefoxWindow>();
        foreach (var window in store.Windows)
        {
            var tabs = new List<TabEntry>();
            for (var i = 0; i < window.Tabs.Count; i++)
            {
                var entry = window.Tabs[i].Entries.LastOrDefault();
                var url = entry?.Url;
                if (string.IsNullOrWhiteSpace(url)) continue;

                var title = entry?.Title;
                tabs.Add(new TabEntry
                {
                    Url = url,
                    Title = string.IsNullOrWhiteSpace(title) ? url : title.Trim(),
                    Active = i == window.Selected,
                });
            }

            windows.Add(new FirefoxWindow(window.ScreenX, window.ScreenY, window.Width, window.Height, tabs));
        }

        return windows;
    }

    static int Score(WindowEntry window, FirefoxWindow store)
    {
        var title = window.Title;
        const string suffix = " — Mozilla Firefox";
        if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            title = title[..^suffix.Length];

        var score = 0;
        var active = store.ActiveTitle;
        if (title.Length > 0)
        {
            // The window title is usually the active tab, but Firefox sometimes keeps
            // the previous page's title, so any tab of that window counts too.
            if (title.Equals(active, StringComparison.OrdinalIgnoreCase)) score += 100;
            else if (store.Tabs.Any(tab => tab.Title.Equals(title, StringComparison.OrdinalIgnoreCase))) score += 80;
            else if (active.Length > 0 && title.Contains(active, StringComparison.OrdinalIgnoreCase)) score += 50;
            else if (store.Tabs.Any(tab => tab.Title.Contains(title, StringComparison.OrdinalIgnoreCase))) score += 40;
        }

        if (Math.Abs(window.Width - store.Width) <= 4) score += 10;
        if (Math.Abs(window.Height - store.Height) <= 4) score += 10;
        // Positions differ between Firefox's frame coordinates and wmctrl's, so this is only a hint.
        if (Math.Abs(window.X - store.X) <= 40 && Math.Abs(window.Y - store.Y) <= 80) score += 5;
        return score;
    }

    static string? FindStoreFile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(home, ".config");

        // Firefox keeps its profile in .mozilla on most systems, but also under
        // .config, snap and flatpak depending on how it was installed.
        string[] roots =
        {
            Path.Combine(config, "mozilla", "firefox"),
            Path.Combine(home, ".mozilla", "firefox"),
            Path.Combine(home, "snap", "firefox", "common", ".mozilla", "firefox"),
            Path.Combine(home, ".var", "app", "org.mozilla.firefox", ".mozilla", "firefox"),
        };

        string? newest = null;
        var newestTime = DateTime.MinValue;
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            foreach (var profile in Directory.EnumerateDirectories(root))
            {
                // recovery.jsonlz4 is written while Firefox runs; sessionstore.jsonlz4 when it closes.
                foreach (var name in new[] { "sessionstore-backups/recovery.jsonlz4", "sessionstore.jsonlz4" })
                {
                    var path = Path.Combine(profile, name);
                    if (!File.Exists(path)) continue;

                    var time = File.GetLastWriteTimeUtc(path);
                    if (time <= newestTime) continue;
                    newestTime = time;
                    newest = path;
                }
            }
        }

        return newest;
    }

    /// <summary>Firefox stores sessions as "mozLz4": a header followed by an LZ4 block.</summary>
    static byte[] DecompressMozLz4(byte[] data)
    {
        if (data.Length < 12 || data[0] != 'm' || data[1] != 'o' || data[2] != 'z' || data[3] != 'L'
            || data[4] != 'z' || data[5] != '4' || data[6] != '0' || data[7] != 0)
            throw new InvalidDataException("Not a Firefox session store.");

        var size = BitConverter.ToUInt32(data, 8);
        if (size > 64 * 1024 * 1024)
            throw new InvalidDataException("Firefox session store is unreasonably large.");

        var output = new byte[size];
        var src = 12;
        var dst = 0;
        while (src < data.Length)
        {
            var token = data[src++];

            var literals = token >> 4;
            if (literals == 15)
            {
                int extra;
                do { extra = data[src++]; literals += extra; } while (extra == 255);
            }

            if (literals > 0)
            {
                Buffer.BlockCopy(data, src, output, dst, literals);
                src += literals;
                dst += literals;
            }

            if (src >= data.Length) break; // the last sequence is literals only

            var offset = data[src] | (data[src + 1] << 8);
            src += 2;

            var match = token & 15;
            if (match == 15)
            {
                int extra;
                do { extra = data[src++]; match += extra; } while (extra == 255);
            }
            match += 4;

            var from = dst - offset;
            for (var i = 0; i < match; i++) output[dst++] = output[from + i];
        }

        return dst == size ? output : output[..dst];
    }

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    sealed class StoreSession
    {
        public List<StoreWindow> Windows { get; set; } = new();
    }

    sealed class StoreWindow
    {
        public int Selected { get; set; }
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<StoreTab> Tabs { get; set; } = new();
    }

    sealed class StoreTab
    {
        public List<StoreEntry> Entries { get; set; } = new();
    }

    sealed class StoreEntry
    {
        public string? Url { get; set; }
        public string? Title { get; set; }
    }
}

/// <summary>A window from Firefox's session store, with its tabs in tab-bar order.</summary>
public sealed record FirefoxWindow(int X, int Y, int Width, int Height, List<TabEntry> Tabs)
{
    public string ActiveTitle => Tabs.FirstOrDefault(tab => tab.Active)?.Title ?? "";
}
