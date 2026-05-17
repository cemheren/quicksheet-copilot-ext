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

## Use Cases

### Data generation

Instantly populate a grid with realistic sample data — great for prototypes, demos, or testing:

```
copilot: 10 user records with first name last name email city, 4, 10
copilot: 8 products with name SKU price stock quantity, 4, 8
copilot: 6 project tasks with title priority status due-date, 4, 6
```

### Technology research & comparison

Ask Copilot to populate a comparison table directly in your spreadsheet:

```
copilot: compare PostgreSQL MySQL SQLite on speed scalability ease column headers, 4, 4
copilot: compare React Vue Angular on performance learning-curve ecosystem maturity, 4, 4
copilot: list 5 message queues with name pros cons throughput, 4, 5
```

### Learning & quizzes

Build interactive study sheets or generate quiz material on any topic:

```
copilot: 5 Python beginner questions with question and answer in two columns, 2, 5
copilot: explain Big-O complexity for common sort algorithms as name and notation, 2, 7
copilot: 6 SQL exercises with task and sample query, 2, 6
```

### Meeting & planning templates

Generate structured agenda items, standup templates, or retro boards:

```
copilot: 8 sprint planning agenda items as task and duration, 2, 8
copilot: standup template for 5 team members with Yesterday Today Blocker columns, 4, 5
copilot: 6 retrospective items as category and insight, 2, 6
```

### Summarize your own data

Use `{A1::C10}` cell-range references to feed existing cell contents to Copilot:

```
copilot: extract 5 action items from {A1::A30}, 1, 5
copilot: summarize {B2::D20} in 3 key insights, 1, 3
copilot: identify patterns in this data {A1::F50}, 2, 6
```

### Developer cheat sheets

Keep quick reference material on your desktop alongside your work:

```
copilot: common git commands with command and what it does, 2, 12
copilot: 8 Python debugging tips as problem and fix, 2, 8
copilot: HTTP status codes 200-599 with code and meaning, 2, 10
```

### Financial quick tables

Generate salary benchmarks, budget templates, or investment summaries:

```
copilot: software engineer salary ranges by level as title and USD range, 2, 6
copilot: 8 personal budget categories with name and recommended percentage, 2, 8
copilot: 5 investment types with name risk-level typical-return, 3, 5
```

### Full dashboard template

A ready-made multi-section dashboard is in the QuickSheet repo at
[`examples/copilot-dashboard.csv`](https://github.com/cemheren/QuickSheet/blob/main/examples/copilot-dashboard.csv) — it demonstrates data generation, research, learning, planning, code help, and finance sections all in one grid.

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
