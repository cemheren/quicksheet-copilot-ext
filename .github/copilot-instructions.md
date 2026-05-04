# Copilot Instructions — quicksheet-copilot-ext

## Build & Test

```bash
# Build (always use the solution file — the repo has multiple projects)
dotnet build quicksheet-copilot-ext.sln

# Run all tests (xUnit)
dotnet test quicksheet-copilot-ext.sln

# Run a single test
dotnet test copilot-ext.Tests --filter "FullyQualifiedName~Parse_TabSeparated_ReturnsCells"
```

## Architecture

This is a [QuickSheet](https://github.com/cemheren/QuickSheet) extension that pipes user prompts through the GitHub Copilot CLI and writes structured cell data back into the spreadsheet. It runs as a **child process** that communicates with QuickSheet via **JSON-lines over stdin/stdout**.

### Message flow

1. **Program.cs** — Entry point. Reads JSON-lines from stdin, dispatches by message `type`:
   - `init` → replies with a `register` message (declares the `copilot:` prefix)
   - `activate` → hands off to `HandleActivate` on a background `Task`
   - `deactivate` → (stub for future cancellation)

2. **PromptBuilder** — Wraps the user prompt with system instructions. Single-cell requests (`1×1`) get a "concise answer" prompt; multi-cell requests get strict TSV formatting rules with exact row/column counts.

3. **CopilotRunner** — Executes the Copilot CLI as a child process. Tries `copilot -p` first, falls back to `gh copilot explain`. Has a 2-minute timeout.

4. **OutputParser** — Parses raw AI output into `CellWrite` objects. Strips markdown code fences, preamble ("Here are the results…"), and trailing fluff. Splits on tabs (preferred) or commas. Clips output to the requested grid dimensions. Falls back to a single-cell result when no structured data is detected.

### Protocol types

All protocol message types (`ActivateMessage`, `CellWrite`, `WriteCellsMessage`, etc.) live in **Program.cs** alongside the main loop. `CellWrite` uses `[JsonPropertyName]` attributes for compact wire format (`r`, `c`, `v`).

## Conventions

- **Namespace**: `CopilotExt` (set via `<RootNamespace>` in the csproj).
- **Internal visibility**: The main project exposes internals to `copilot-ext.Tests` via `InternalsVisibleTo`, so all non-public types/methods are testable without being public.
- **Static classes for stateless logic**: `OutputParser`, `PromptBuilder`, and `CopilotRunner` are all `static class` — they hold no instance state.
- **JSON serialization**: Uses `System.Text.Json` with camelCase naming policy and `WhenWritingNull` ignore. Stdout writes are synchronized with a lock to prevent interleaved output from concurrent `activate` handlers.
- **Extension manifest**: `quicksheet-extension.json` declares the extension metadata and the `dotnet run` entry point used by QuickSheet's extension loader.
