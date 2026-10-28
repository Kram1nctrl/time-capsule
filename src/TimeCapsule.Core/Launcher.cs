using System.Diagnostics;

namespace TimeCapsule;

public static class Launcher
{
    const int WindowWaitMs = 10000; // apps get this long to map their windows before wmctrl can place them

    public static int Restore(Manifest manifest, bool dryRun, Action<string>? log = null)
    {
        var output = log ?? Console.WriteLine;
        var failed = 0;
        output($"Restoring '{manifest.Name}' ({Plural(manifest.Processes.Count, "process", "processes")}, {Plural(manifest.Windows.Count, "window", "windows")}) from {manifest.CreatedUtc:u}");

        var openClasses = OpenWindows().Select(w => w.Class).ToHashSet();

        foreach (var entry in manifest.Processes)
        {
            if (!File.Exists(entry.Exe))
            {
                output($"missing   {entry.Exe}");
                failed++;
                continue;
            }

            // Already on screen: another launch would only open a duplicate or a tab.
            var classes = manifest.Windows.Where(w => w.Pid == entry.Pid).Select(w => w.Class).ToHashSet();
            if (classes.Overlaps(openClasses))
            {
                output($"running   {entry.Exe}");
                continue;
            }

            var cwd = entry.Cwd != null && Directory.Exists(entry.Cwd) ? entry.Cwd : null;
            if (entry.Cwd != null && cwd == null)
                output($"cwd gone  {entry.Exe} ({entry.Cwd})");

            if (dryRun)
            {
                output($"would run {entry.Exe} {string.Join(' ', entry.Args)}"
                    + (cwd == null ? "" : $"  [in {cwd}]"));
                continue;
            }

            try
            {
                var psi = new ProcessStartInfo(entry.Exe) { UseShellExecute = false };
                foreach (var arg in entry.Args) psi.ArgumentList.Add(arg);
                if (cwd != null) psi.WorkingDirectory = cwd;
                Process.Start(psi);
                output($"started   {entry.Exe}");
            }
            catch (Exception ex)
            {
                output($"failed    {entry.Exe}: {ex.Message}");
                failed++;
            }
        }

        if (dryRun || manifest.Windows.Count == 0) return failed == 0 ? 0 : 1;

        // Window ids change on relaunch and titles change with the page/document,
        // so pair each saved window with the first open window of the same class.
        // shortcut: several windows of one class are paired in list order; add a stable per-window key if that matters.
        output("waiting for windows...");
        var remaining = new List<WindowEntry>(manifest.Windows);
        var used = new HashSet<string>();
        var until = DateTime.UtcNow.AddSeconds(WindowWaitMs);

        while (remaining.Count > 0 && DateTime.UtcNow < until)
        {
            foreach (var open in OpenWindows())
            {
                if (used.Contains(open.Id)) continue;

                var index = remaining.FindIndex(saved => saved.Class == open.Class || saved.Title == open.Title);
                if (index < 0) continue;

                var saved = remaining[index];
                remaining.RemoveAt(index);
                used.Add(open.Id);

                if (Place(open.Id, saved))
                {
                    output($"placed    {saved.Title}");
                }
                else
                {
                    output($"failed    {saved.Title}: the window manager refused the move");
                    failed++;
                }
            }

            if (remaining.Count == 0) break;
            Thread.Sleep(300);
        }

        foreach (var window in remaining)
        {
            output($"missing   {window.Title} ({window.Class})");
            failed++;
        }

        return failed == 0 ? 0 : 1;
    }

    static bool Place(string id, WindowEntry window)
    {
        // A maximized window ignores geometry requests, so put the state back instead.
        if (window.Maximized &&
            Shell.Run("wmctrl", "-i", "-r", id, "-b", "add,maximized_vert,maximized_horz") != null)
        {
            if (window.Desktop >= 0)
                Shell.Run("wmctrl", "-i", "-r", id, "-t", window.Desktop.ToString());
            return true;
        }

        Shell.Run("wmctrl", "-i", "-r", id, "-b", "remove,maximized_vert,maximized_horz");
        Thread.Sleep(80);

        // wmctrl reports a window's geometry with its frame extents added, while -e
        // positions the frame: subtracting them twice keeps the client where it was.
        var (left, top) = FrameOffset(id);
        var x = window.X - left * 2;
        var y = window.Y - top * 2;

        if (Shell.Run("wmctrl", "-i", "-r", id, "-e", $"0,{x},{y},{window.Width},{window.Height}") == null)
            return false;
        if (window.Desktop >= 0)
            Shell.Run("wmctrl", "-i", "-r", id, "-t", window.Desktop.ToString());
        return true;
    }

    static (int Left, int Top) FrameOffset(string id)
    {
        // _NET_FRAME_EXTENTS(CARDINAL) = left, right, top, bottom
        var props = Shell.Run("xprop", "-id", id, "_NET_FRAME_EXTENTS") ?? "";
        var equals = props.IndexOf('=');
        if (equals < 0) return (0, 0);

        var parts = props[(equals + 1)..].Split(',', StringSplitOptions.TrimEntries);
        return parts.Length >= 3 && int.TryParse(parts[0], out var left) && int.TryParse(parts[2], out var top)
            ? (left, top)
            : (0, 0);
    }

    static List<(string Id, string Class, string Title)> OpenWindows()
    {
        var output = Shell.Run("wmctrl", "-lpx") ?? "";
        var windows = new List<(string Id, string Class, string Title)>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // columns: id desktop pid class host title...
            var f = line.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 6) continue;
            windows.Add((f[0], f[3], f[5].Trim()));
        }
        return windows;
    }

    static string Plural(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";
}
