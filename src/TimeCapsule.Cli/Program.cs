using TimeCapsule;

try
{
    return (args.FirstOrDefault() ?? "help") switch
    {
        "save" => Save(args),
        "list" => List(),
        "tabs" => Tabs(),
        "restore" => Restore(args),
        _ => Help(),
    };
}
catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"time-capsule: {ex.Message}");
    return 1;
}

int Save(string[] args)
{
    var path = Storage.NewPath(Storage.CleanTag(args.Length > 1 ? args[1] : "session"));
    var manifest = Inspector.Capture(Path.GetFileNameWithoutExtension(path));
    manifest.Save(path);
    Console.WriteLine($"Saved {manifest.Processes.Count} processes and {manifest.Windows.Count} windows to {path}");
    return 0;
}

int List()
{
    var files = Storage.List();
    if (files.Length == 0)
    {
        Console.WriteLine("No sessions yet.");
        return 0;
    }

    foreach (var file in files)
        Console.WriteLine($"{Path.GetFileNameWithoutExtension(file)}\t{File.GetLastWriteTime(file):yyyy-MM-dd HH:mm}");
    return 0;
}

int Tabs()
{
    var windows = Firefox.ReadWindows();
    if (windows.Count == 0)
    {
        Console.WriteLine("No Firefox session store found.");
        return 0;
    }

    foreach (var window in windows)
    {
        var count = window.Tabs.Count == 1 ? "1 tab" : $"{window.Tabs.Count} tabs";
        Console.WriteLine($"{window.Width}x{window.Height} at {window.X},{window.Y} - {count}");
        foreach (var tab in window.Tabs)
            Console.WriteLine($"  {(tab.Active ? "*" : " ")} {tab.Title}\t{tab.Url}");
    }

    return 0;
}

int Restore(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("usage: time-capsule restore <name|path> [--dry-run]");
        return 1;
    }

    var target = args[1];
    var matches = Storage.Matches(target);

    if (matches.Length == 0)
    {
        Console.Error.WriteLine($"time-capsule: no such session: {target}");
        return 1;
    }

    if (matches.Length > 1)
    {
        Console.Error.WriteLine($"time-capsule: '{target}' is ambiguous:");
        foreach (var match in matches) Console.Error.WriteLine($"  {Path.GetFileNameWithoutExtension(match)}");
        return 1;
    }

    return Launcher.Restore(Manifest.Load(matches[0]), args.Contains("--dry-run"));
}

int Help()
{
    Console.WriteLine("""
        time-capsule - capture a working session and bring it back after a reboot

        usage:
          time-capsule save [tag]                 snapshot processes, arguments, cwds and windows
          time-capsule list                       list saved snapshots
          time-capsule tabs                       show the tabs Firefox has open
          time-capsule restore <name|path>        relaunch a snapshot
          time-capsule restore <name> --dry-run   show what would be launched
          time-capsule help

        snapshots are JSON in ~/.local/share/time-capsule/sessions/
        """);
    return 0;
}
