using System.Diagnostics;

namespace CopilotExt;

/// <summary>
/// Executes the GitHub Copilot CLI (via `copilot -p` or `gh copilot`).
/// Captures stdout and returns the raw output string.
/// </summary>
static class CopilotRunner
{
    private const int TimeoutMs = 120_000; // 2 minutes

    public static async Task<string?> RunAsync(string prompt)
    {
        // Try `copilot -p` first (standalone Copilot CLI non-interactive mode)
        var result = await TryRunAsync("copilot", new[] { "-p", prompt, "--allow-all-tools" });
        if (result != null) return result;

        // Fall back to `gh copilot explain` which accepts a prompt argument
        result = await TryRunAsync("gh", new[] { "copilot", "explain", prompt, "--allow-all-tools" });
        if (result != null) return result;

        return null;
    }

    private static async Task<string?> TryRunAsync(string command, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            using var cts = new CancellationTokenSource(TimeoutMs);

            string stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token);
            await proc.WaitForExitAsync(cts.Token);

            if (proc.ExitCode != 0) return null;
            if (string.IsNullOrWhiteSpace(stdout)) return null;

            return stdout;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}
