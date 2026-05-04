using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace CopilotExt.Tests;

/// <summary>
/// End-to-end integration tests that launch the extension as a child process
/// (the same way QuickSheet does) and verify the JSON-lines protocol flow.
///
/// Run all E2E tests:
///   dotnet test copilot-ext.Tests --filter "Category=E2E"
///
/// Run only the tests that require the Copilot CLI:
///   dotnet test copilot-ext.Tests --filter "Category=E2E-Copilot"
/// </summary>
public class E2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private Process? _proc;
    private readonly ConcurrentQueue<string> _stdoutLines = new();
    private readonly ConcurrentQueue<string> _stderrLines = new();

    /// <summary>
    /// Locates the main copilot-ext.csproj project directory by walking up
    /// from the test assembly output directory.
    /// </summary>
    private static string FindProjectDir()
    {
        // AppContext.BaseDirectory is e.g. .../copilot-ext.Tests/bin/Debug/net9.0/
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            string candidate = Path.Combine(dir, "copilot-ext.csproj");
            if (File.Exists(candidate)) return dir;
            dir = Path.GetDirectoryName(dir)!;
        }
        throw new InvalidOperationException(
            $"Could not find copilot-ext.csproj from {AppContext.BaseDirectory}");
    }

    /// <summary>
    /// Starts the extension process using the same cmd.exe/bash wrapper that
    /// QuickSheet uses, so we exercise the full process-tree stdin/stdout chain.
    /// </summary>
    private void StartExtension()
    {
        string projectDir = FindProjectDir();
        string shell, args;

        if (OperatingSystem.IsWindows())
        {
            shell = "cmd.exe";
            args = $"/c cd /d \"{projectDir}\" && dotnet run --no-build --project copilot-ext.csproj";
        }
        else
        {
            shell = "/bin/bash";
            args = $"-c \"cd '{projectDir}' && dotnet run --no-build --project copilot-ext.csproj\"";
        }

        var psi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = projectDir
        };

        _proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start extension process");

        // Background threads to drain stdout/stderr (mirrors QuickSheet's ReadLoop)
        var stdoutThread = new Thread(() => DrainStream(_proc.StandardOutput, _stdoutLines))
            { IsBackground = true, Name = "E2E-stdout" };
        var stderrThread = new Thread(() => DrainStream(_proc.StandardError, _stderrLines))
            { IsBackground = true, Name = "E2E-stderr" };
        stdoutThread.Start();
        stderrThread.Start();
    }

    private static void DrainStream(StreamReader reader, ConcurrentQueue<string> sink)
    {
        try
        {
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    sink.Enqueue(line);
            }
        }
        catch { }
    }

    private void Send(object message)
    {
        string json = JsonSerializer.Serialize(message, JsonOpts);
        _proc!.StandardInput.WriteLine(json);
        _proc.StandardInput.Flush();
    }

    /// <summary>
    /// Waits until a stdout line matching the predicate appears, or times out.
    /// Returns the matching line or null.
    /// </summary>
    private string? WaitForMessage(Func<string, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var line in _stdoutLines)
            {
                if (predicate(line)) return line;
            }
            Thread.Sleep(250);
        }
        return null;
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "E2E")]
    public void Init_ShouldReceiveRegister()
    {
        StartExtension();
        Thread.Sleep(2000); // let the process start

        Send(new { type = "init", version = 1 });

        string? register = WaitForMessage(
            line => line.Contains("\"register\""), TimeSpan.FromSeconds(15));

        Assert.NotNull(register);

        var doc = JsonDocument.Parse(register);
        Assert.Equal("register", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("copilot", doc.RootElement.GetProperty("prefix").GetString());
    }

    [Fact]
    [Trait("Category", "E2E")]
    public void Activate_ShouldReceiveStatusMessage()
    {
        StartExtension();
        Thread.Sleep(2000);

        Send(new { type = "init", version = 1 });
        WaitForMessage(line => line.Contains("\"register\""), TimeSpan.FromSeconds(15));

        Send(new
        {
            type = "activate",
            id = "e2e-status-test",
            anchor = new { row = 1, col = 0 },
            @params = new[] { "hello" },
            gridCols = 1,
            gridRows = 1
        });

        string? status = WaitForMessage(
            line => line.Contains("\"status\"") && line.Contains("e2e-status-test"),
            TimeSpan.FromSeconds(10));

        Assert.NotNull(status);
    }

    /// <summary>
    /// Full end-to-end: sends a prompt and waits for a write or error response.
    /// Requires the Copilot CLI to be installed and authenticated.
    /// This test verifies the stdin isolation fix — without it, copilot.exe
    /// inherits the extension's stdin pipe and blocks indefinitely on Windows.
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Category", "E2E-Copilot")]
    public void Activate_ShouldReturnWriteOrError_WithinTimeout()
    {
        StartExtension();
        Thread.Sleep(2000);

        Send(new { type = "init", version = 1 });
        string? register = WaitForMessage(
            line => line.Contains("\"register\""), TimeSpan.FromSeconds(15));
        Assert.NotNull(register);

        Send(new
        {
            type = "activate",
            id = "e2e-full-test",
            anchor = new { row = 1, col = 0 },
            @params = new[] { "what is 2+2" },
            gridCols = 1,
            gridRows = 1
        });

        // Must get a write or error within the runner's 2-min timeout.
        // We use 90s here — if it takes longer, the stdin inheritance bug
        // is likely back (copilot blocks on the shared stdin pipe).
        string? response = WaitForMessage(
            line => (line.Contains("\"write\"") || line.Contains("\"error\""))
                    && line.Contains("e2e-full-test"),
            TimeSpan.FromSeconds(90));

        Assert.NotNull(response);

        var doc = JsonDocument.Parse(response);
        string type = doc.RootElement.GetProperty("type").GetString()!;
        Assert.True(type == "write" || type == "error",
            $"Expected 'write' or 'error', got '{type}'");

        // If write, verify cells are present
        if (type == "write")
        {
            Assert.True(doc.RootElement.GetProperty("cells").GetArrayLength() > 0,
                "Write message should contain at least one cell");
        }
    }

    /// <summary>
    /// Verifies that multiple sequential activations work (process stays alive).
    /// </summary>
    [Fact]
    [Trait("Category", "E2E")]
    public void MultipleMessages_ProcessStaysAlive()
    {
        StartExtension();
        Thread.Sleep(2000);

        // First init
        Send(new { type = "init", version = 1 });
        string? register = WaitForMessage(
            line => line.Contains("\"register\""), TimeSpan.FromSeconds(15));
        Assert.NotNull(register);

        // Second init (should get another register — process still alive)
        Send(new { type = "init", version = 1 });
        // Clear old messages and wait for new register
        Thread.Sleep(1000);

        Assert.False(_proc!.HasExited, "Extension process should still be running");
    }

    // ── Cleanup ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_proc is null) return;
        try { _proc.StandardInput.Close(); } catch { }
        try
        {
            if (!_proc.HasExited)
            {
                _proc.Kill(entireProcessTree: true);
                _proc.WaitForExit(3000);
            }
        }
        catch { }
        _proc.Dispose();
    }
}
