# quicksheet-copilot-ext

A [QuickSheet](https://github.com/cemheren/QuickSheet) extension that integrates GitHub Copilot AI into your spreadsheet cells.

## Usage

In QuickSheet, add an `ext:` cell to install this extension:

```
ext: github:cemheren/quicksheet-copilot-ext
```

Then use the `copilot:` prefix in any cell:

```
copilot: list 5 popular programming languages, 2, 5
```

This asks Copilot to generate a 2-column × 5-row grid and writes the structured output into the cells below.

### Syntax

```
copilot: <your prompt>, <columns>, <rows>
```

- **prompt**: Natural language request for Copilot
- **columns**: Number of columns in the output grid
- **rows**: Number of rows in the output grid

### Examples

```
copilot: generate random test data with name and age, 2, 10
copilot: categorize these items {A1::A20}, 1, 20
copilot: summarize this data {B1::E50}, 3, 1
```

Cell references like `{A1::C10}` are expanded by QuickSheet before being sent to the extension, so Copilot receives the actual cell contents as context.

## Requirements

- [GitHub Copilot CLI](https://docs.github.com/copilot/concepts/agents/about-copilot-cli) (`copilot`) installed and authenticated, OR
- [GitHub CLI](https://cli.github.com/) (`gh`) with Copilot extension
- .NET 9 SDK (for building the extension)

## How It Works

1. QuickSheet launches this extension as a child process
2. Extension registers the `copilot:` prefix
3. When a `copilot:` cell is activated, the extension:
   - Injects a system prompt demanding TSV-formatted output
   - Runs the Copilot CLI with the user's prompt
   - Parses the output, stripping markdown fences and preamble
   - Writes structured cell data back to QuickSheet

## Building

```bash
dotnet build quicksheet-copilot-ext.sln
```

## Testing

### Unit tests

```bash
dotnet test quicksheet-copilot-ext.sln
```

### E2E integration tests

The project includes end-to-end tests that launch the extension as a child process
(using the same `cmd.exe`/`bash` wrapper that QuickSheet uses) and verify the
JSON-lines protocol over stdin/stdout. These are useful for diagnosing
communication issues between QuickSheet and this extension, especially
platform-specific problems like pipe buffering or stdin handle inheritance.

```bash
# Run the fast E2E tests (no Copilot CLI needed — tests init/register handshake only)
dotnet test copilot-ext.Tests --filter "Category=E2E&Category!=E2E-Copilot"

# Run the full E2E test that calls the Copilot CLI (requires `copilot` installed & authenticated)
dotnet test copilot-ext.Tests --filter "Category=E2E-Copilot"

# Run everything (unit + E2E)
dotnet test quicksheet-copilot-ext.sln
```

## License

MIT
