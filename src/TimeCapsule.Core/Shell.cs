using System.Diagnostics;

namespace TimeCapsule;

public static class Shell
{
    /// <summary>Runs a command and returns its stdout, or null when the command is missing or fails.</summary>
    public static string? Run(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process == null) return null;

            var stdout = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? stdout : null;
        }
        catch
        {
            return null;
        }
    }
}
