using System.Diagnostics;

namespace TimeCapsule;

public static class Launcher
{
    const int WindowWaitMs = 6000; // give apps time to map their windows before wmctrl can place them

    public static int Restore(Manifest manifest, bool dryRun, Action<string>? log = null)
    {
        var output = log ?? Console.WriteLine;
        var failed = 0;
        output($"Restoring '{manifest.Name}' ({Plural(manifest.Processes.Count, "process", "processes")}, {Plural(manifest.Windows.Count, "window", "windows")}) from {manifest.CreatedUtc:u}");

        foreach (var entry in manifest.Processes)
        {
            if (!File.Exists(entry.Exe))
            {
                output($"missing   {entry.Exe}");
                failed++;
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

        // Window ids change on relaunch, so windows are matched by title.
        // shortcut: duplicate titles all get placed at the same geometry, store a per-window match key if that matters.
        output($"waiting {WindowWaitMs / 1000}s for windows...");
        Thread.Sleep(WindowWaitMs);

        foreach (var window in manifest.Windows)
        {
            var geometry = $"0,{window.X},{window.Y},{window.Width},{window.Height}";
            var placed = Shell.Run("wmctrl", "-F", "-r", window.Title, "-e", geometry) != null;
            if (placed && window.Desktop >= 0)
                Shell.Run("wmctrl", "-F", "-r", window.Title, "-t", window.Desktop.ToString());

            output(placed ? $"placed    {window.Title}" : $"unplaced  {window.Title}");
            if (!placed) failed++;
        }

        return failed == 0 ? 0 : 1;
    }

    static string Plural(int count, string one, string many) => count == 1 ? $"1 {one}" : $"{count} {many}";
}
