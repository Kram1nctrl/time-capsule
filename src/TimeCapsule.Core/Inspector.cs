namespace TimeCapsule;

public static class Inspector
{
    public static Manifest Capture(string name)
    {
        var manifest = new Manifest
        {
            Name = name,
            CreatedUtc = DateTimeOffset.UtcNow,
            Host = Environment.MachineName,
        };

        // Its own window would never be found again on restore.
        var windows = ReadWindows().Where(w => w.Pid != Environment.ProcessId).ToList();
        manifest.Windows.AddRange(windows);
        var windowPids = windows.Select(w => w.Pid).ToHashSet();

        var uid = UidOf("/proc/self/status");
        foreach (var dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid) || pid == Environment.ProcessId) continue;
            // Only interactive processes: those with a terminal or a window. Restoring
            // session daemons (pipewire, the WM, ...) would duplicate the live session.
            if (!windowPids.Contains(pid) && !HasTty(pid)) continue;

            var entry = ReadProcess(pid, uid);
            if (entry != null) manifest.Processes.Add(entry);
        }

        return manifest;
    }

    static ProcessEntry? ReadProcess(int pid, string? uid)
    {
        try
        {
            if (uid == null || UidOf($"/proc/{pid}/status") != uid) return null;

            var exe = LinkTarget($"/proc/{pid}/exe");
            if (exe == null) return null;

            // cmdline is NUL separated and empty for kernel threads.
            var argv = File.ReadAllText($"/proc/{pid}/cmdline")
                .Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (argv.Length == 0) return null;

            const string deleted = " (deleted)";
            if (exe.EndsWith(deleted)) exe = exe[..^deleted.Length];

            return new ProcessEntry
            {
                Pid = pid,
                Exe = exe,
                Args = argv[1..],
                Cwd = LinkTarget($"/proc/{pid}/cwd"),
            };
        }
        catch
        {
            return null; // process vanished or is not ours to inspect
        }
    }

    static IEnumerable<WindowEntry> ReadWindows()
    {
        var output = Shell.Run("wmctrl", "-lpGx") ?? "";

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // columns: id desktop pid x y w h class host title...
            var f = line.Split(' ', 10, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 10) continue;
            if (!int.TryParse(f[1], out var desktop) || !int.TryParse(f[3], out var x) ||
                !int.TryParse(f[4], out var y) || !int.TryParse(f[5], out var width) ||
                !int.TryParse(f[6], out var height)) continue;

            if (!int.TryParse(f[2], out var pid)) continue;

            // Sticky windows (desktop -1) are panels and desktops: part of the session, not of the snapshot.
            if (desktop < 0) continue;

            yield return new WindowEntry
            {
                Pid = pid,
                Title = f[9].Trim(),
                Class = f[7],
                Desktop = desktop,
                X = x,
                Y = y,
                Width = width,
                Height = height,
            };
        }
    }

    static bool HasTty(int pid)
    {
        try
        {
            // /proc/<pid>/stat field 7 (tty_nr), after the comm field which may contain spaces.
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
            return fields.Length > 4 && fields[4] != "0";
        }
        catch
        {
            return false;
        }
    }

    static string? UidOf(string statusPath) =>
        File.ReadLines(statusPath)
            .FirstOrDefault(line => line.StartsWith("Uid:"))?
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries) is { Length: > 1 } parts
                ? parts[1]
                : null;

    static string? LinkTarget(string path) =>
        Directory.Exists(path)
            ? new DirectoryInfo(path).ResolveLinkTarget(true)?.FullName
            : new FileInfo(path).ResolveLinkTarget(true)?.FullName;
}
