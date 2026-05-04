using System.Diagnostics;
using System.Text;

namespace CopilotExt;

/// <summary>
/// Executes the GitHub Copilot CLI via `copilot -p`.
/// Captures stdout and returns the raw output string.
/// Logs diagnostics to stderr so they don't interfere with the JSON-lines protocol on stdout.
/// </summary>
static class CopilotRunner
{
    private const int TimeoutMs = 120_000; // 2 minutes

    public static async Task<string?> RunAsync(string prompt)
    {
        return await TryRunAsync("copilot", new[] { "-p", prompt, "--allow-all-tools" });
    }

    private static async Task<string?> TryRunAsync(string command, string[] args)
    {
        Log($"Starting: {command} {string.Join(" ", args)}");

        Process? proc = null;
        var stdoutBuf = new StringBuilder();

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

            proc = Process.Start(psi);
            if (proc == null)
            {
                Log($"Failed to start process: {command}");
                return null;
            }

            Log($"Process started (PID {proc.Id})");

            using var cts = new CancellationTokenSource(TimeoutMs);

            // Read incrementally so we capture partial output even if the process times out
            var stdoutTask = ReadStreamAsync(proc.StandardOutput, stdoutBuf, cts.Token);
            var stderrBuf = new StringBuilder();
            var stderrTask = ReadStreamAsync(proc.StandardError, stderrBuf, cts.Token);

            await Task.WhenAll(stdoutTask, stderrTask);
            await proc.WaitForExitAsync(cts.Token);

            string stdout = stdoutBuf.ToString();
            string stderr = stderrBuf.ToString();

            Log($"Process exited with code {proc.ExitCode}");

            if (!string.IsNullOrWhiteSpace(stderr))
                Log($"stderr: {stderr.Trim()}");

            if (!string.IsNullOrWhiteSpace(stdout))
                Log($"stdout ({stdout.Length} chars): {Truncate(stdout.Trim(), 200)}");

            if (proc.ExitCode != 0)
            {
                Log($"Non-zero exit code {proc.ExitCode}, returning null");
                return null;
            }
            if (string.IsNullOrWhiteSpace(stdout))
            {
                Log("Empty stdout, returning null");
                return null;
            }

            return stdout;
        }
        catch (OperationCanceledException)
        {
            Log($"Process timed out after {TimeoutMs}ms");
            TryKillProcess(proc);

            string captured = stdoutBuf.ToString();
            if (!string.IsNullOrWhiteSpace(captured))
            {
                Log($"Returning partial stdout despite timeout ({captured.Length} chars): {Truncate(captured.Trim(), 200)}");
                return captured;
            }
            Log("No stdout captured before timeout");
            return null;
        }
        catch (Exception ex)
        {
            Log($"Exception: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        finally
        {
            proc?.Dispose();
        }
    }

    private static async Task ReadStreamAsync(System.IO.StreamReader reader, StringBuilder buffer, CancellationToken ct)
    {
        var buf = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buf, ct)) > 0)
        {
            buffer.Append(buf, 0, read);
        }
    }

    private static void TryKillProcess(Process? proc)
    {
        if (proc == null) return;
        try
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
                Log("Process killed");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to kill process: {ex.Message}");
        }
    }

    // TODO: move info logs to stdout or structured log channel once harness supports it
    private static void Log(string message)
    {
        Console.Error.WriteLine($"[CopilotRunner] {message}");
    }

    private static string Truncate(string value, int maxLen)
    {
        if (value.Length <= maxLen) return value;
        return value[..maxLen] + "…";
    }
}
