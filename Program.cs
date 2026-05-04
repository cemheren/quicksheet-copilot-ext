using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopilotExt;

/// <summary>
/// Main entry point. Reads JSON-lines from stdin, dispatches to CopilotRunner,
/// writes structured cell output back via stdout.
/// </summary>
class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    static async Task Main()
    {
        using var reader = Console.In;
        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? type = GetMessageType(line);
            switch (type)
            {
                case "init":
                    SendRegister();
                    break;

                case "activate":
                    var msg = JsonSerializer.Deserialize<ActivateMessage>(line, JsonOpts);
                    if (msg != null)
                        _ = Task.Run(() => HandleActivate(msg));
                    break;

                case "deactivate":
                    // Future: cancel running processes
                    break;
            }
        }
    }

    static void SendRegister()
    {
        var msg = new { type = "register", prefix = "copilot", name = "copilot-ext", version = "0.1.0" };
        SendMessage(msg);
    }

    static async Task HandleActivate(ActivateMessage msg)
    {
        // TODO: move info logs to stdout or structured log channel once harness supports it
        Console.Error.WriteLine($"[Activate] START id={msg.Id} grid={msg.GridCols}x{msg.GridRows}");
        SendMessage(new { type = "status", id = msg.Id, message = "⏳ Asking Copilot..." });

        try
        {
            string userPrompt = string.Join(" ", msg.Params);
            string fullPrompt = PromptBuilder.Build(userPrompt, msg.GridCols, msg.GridRows);

            string? rawOutput = await CopilotRunner.RunAsync(fullPrompt);
            Console.Error.WriteLine($"[Activate] id={msg.Id} result={(rawOutput == null ? "null" : $"{rawOutput.Length} chars")}");

            if (rawOutput == null)
            {
                Console.Error.WriteLine($"[Activate] id={msg.Id} sending error (null result)");
                SendMessage(new { type = "error", id = msg.Id, message = "Copilot CLI failed or not found. Ensure 'copilot' or 'gh' is installed and authenticated." });
                return;
            }

            var cells = OutputParser.Parse(rawOutput, msg.GridCols, msg.GridRows);
            if (cells.Count == 0)
            {
                cells.Add(new CellWrite { Row = 0, Col = 0, Value = rawOutput.Trim() });
            }

            Console.Error.WriteLine($"[Activate] id={msg.Id} writing {cells.Count} cells");
            SendMessage(new WriteCellsMessage { Type = "write", Id = msg.Id, Cells = cells.ToArray() });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Activate] id={msg.Id} exception: {ex.GetType().Name}: {ex.Message}");
            SendMessage(new { type = "error", id = msg.Id, message = ex.Message });
        }
    }

    static string? GetMessageType(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("type", out var t))
                return t.GetString();
        }
        catch { }
        return null;
    }

    static readonly object _writeLock = new();

    static void SendMessage(object msg)
    {
        string json = JsonSerializer.Serialize(msg, JsonOpts);
        lock (_writeLock)
        {
            Console.WriteLine(json);
            Console.Out.Flush();
        }
    }
}

// ── Protocol message types ──────────────────────────────────────────────

class ActivateMessage
{
    public string Type { get; set; } = "";
    public string Id { get; set; } = "";
    public CellPosition Anchor { get; set; } = new();
    public string[] Params { get; set; } = [];
    public int GridCols { get; set; }
    public int GridRows { get; set; }
}

class CellPosition
{
    public int Row { get; set; }
    public int Col { get; set; }
}

class CellWrite
{
    [JsonPropertyName("r")]
    public int Row { get; set; }
    [JsonPropertyName("c")]
    public int Col { get; set; }
    [JsonPropertyName("v")]
    public string Value { get; set; } = "";
}

class WriteCellsMessage
{
    public string Type { get; set; } = "write";
    public string Id { get; set; } = "";
    public CellWrite[] Cells { get; set; } = [];
}
