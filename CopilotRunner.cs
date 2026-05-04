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
        // Use a single timeout budget shared across all fallback attempts
        // so worst-case wait is always TimeoutMs, not TimeoutMs × N.
        using var cts = new CancellationTokenSource(TimeoutMs);

        // Try `copilot -p` first (standalone Copilot CLI non-interactive mode)
        var result = await TryRunAsync("copilot", new[] { "-p", prompt, "--allow-all-tools" }, cts.Token);
        if (result != null) return result;

        // Fall back to `gh copilot explain` which accepts a prompt argument
        result = await TryRunAsync("gh", new[] { "copilot", "explain", prompt, "--allow-all-tools" }, cts.Token);
        if (result != null) return result;

        return null;
    }

    private static async Task<string?> TryRunAsync(string command, string[] args, CancellationToken ct)
    {
        Process? proc = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            proc = Process.Start(psi);
            if (proc == null) return null;

            // Close stdin immediately so the child process (and any grandchildren
            // like MCP servers) cannot inherit and block on the extension's stdin
            // pipe, which carries the QuickSheet JSON-lines protocol.
            proc.StandardInput.Close();

            // Must drain both stdout and stderr concurrently. On Windows the
            // default pipe buffer is small (~4 KB); if the child process fills
            // stderr without a reader the write blocks, stdout stalls, and we
            // hit the timeout.
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);

            await Task.WhenAll(stdoutTask, stderrTask);
            string stdout = stdoutTask.Result;

            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0) return null;
            if (string.IsNullOrWhiteSpace(stdout)) return null;

            return stdout;
        }
        catch (OperationCanceledException)
        {
            KillSafe(proc);
            return null;
        }
        catch
        {
            KillSafe(proc);
            return null;
        }
        finally
        {
            proc?.Dispose();
        }
    }

    private static void KillSafe(Process? proc)
    {
        try { if (proc is { HasExited: false }) proc.Kill(entireProcessTree: true); }
        catch { }
    }
}
