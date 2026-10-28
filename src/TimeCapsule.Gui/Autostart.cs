using System;
using System.IO;
using TimeCapsule;

namespace TimeCapsule.Gui;

/// <summary>Keeps the widget coming back after a reboot, like a phone widget.</summary>
static class Autostart
{
    static string Entry => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "autostart", "time-capsule-widget.desktop");

    public static bool Enabled => File.Exists(Entry);

    public static void Enable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Entry)!);
        File.WriteAllText(Entry, $"""
            [Desktop Entry]
            Type=Application
            Name=Time Capsule widget
            Comment=Keep the Time Capsule widget on screen
            Exec={Command()} --widget
            Icon=time-capsule
            Terminal=false
            X-GNOME-Autostart-enabled=true
            """);
    }

    public static void Disable() => File.Delete(Entry);

    static string Command()
    {
        var exe = Environment.ProcessPath ?? "time-capsule";
        var args = Environment.GetCommandLineArgs();
        return Path.GetFileNameWithoutExtension(exe) == "dotnet"
            ? $"\"{exe}\" \"{args[0]}\"" // started as: dotnet time-capsule-gui.dll
            : $"\"{exe}\"";
    }
}
