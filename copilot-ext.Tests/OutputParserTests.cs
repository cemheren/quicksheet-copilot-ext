using CopilotExt;

namespace CopilotExt.Tests;

public class OutputParserTests
{
    // ── Empty / null input ──────────────────────────────────────────────

    [Fact]
    public void Parse_NullInput_ReturnsEmpty()
    {
        var result = OutputParser.Parse(null!, gridCols: 3, gridRows: 5);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var result = OutputParser.Parse("", gridCols: 3, gridRows: 5);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmpty()
    {
        var result = OutputParser.Parse("   \n  \n  ", gridCols: 3, gridRows: 5);
        Assert.Empty(result);
    }

    // ── Tab-separated parsing ───────────────────────────────────────────

    [Fact]
    public void Parse_TabSeparated_ReturnsCells()
    {
        var input = "Alice\t30\tNY\nBob\t25\tLA";
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 5);

        Assert.Contains(result, c => c.Row == 0 && c.Col == 0 && c.Value == "Alice");
        Assert.Contains(result, c => c.Row == 0 && c.Col == 1 && c.Value == "30");
        Assert.Contains(result, c => c.Row == 0 && c.Col == 2 && c.Value == "NY");
        Assert.Contains(result, c => c.Row == 1 && c.Col == 0 && c.Value == "Bob");
        Assert.Contains(result, c => c.Row == 1 && c.Col == 1 && c.Value == "25");
        Assert.Contains(result, c => c.Row == 1 && c.Col == 2 && c.Value == "LA");
    }

    [Fact]
    public void Parse_CommaSeparated_ReturnsCells()
    {
        var input = "Alice,30,NY\nBob,25,LA";
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 5);

        Assert.Contains(result, c => c.Row == 0 && c.Col == 0 && c.Value == "Alice");
        Assert.Contains(result, c => c.Row == 1 && c.Col == 2 && c.Value == "LA");
    }

    [Fact]
    public void Parse_TabsPreferredOverCommas()
    {
        // Line has both tabs and commas — should split on tabs
        var input = "a,b\tc,d";
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 5);

        Assert.Contains(result, c => c.Row == 0 && c.Col == 0 && c.Value == "a,b");
        Assert.Contains(result, c => c.Row == 0 && c.Col == 1 && c.Value == "c,d");
    }

    // ── Markdown code fence stripping ───────────────────────────────────

    [Fact]
    public void Parse_StripsSingleCodeFence()
    {
        var input = "```\nAlice\t30\nBob\t25\n```";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.Contains(result, c => c.Row == 0 && c.Col == 0 && c.Value == "Alice");
        Assert.Contains(result, c => c.Row == 1 && c.Col == 0 && c.Value == "Bob");
    }

    [Fact]
    public void Parse_StripsCodeFenceWithLanguageTag()
    {
        var input = "```csv\nAlice,30\nBob,25\n```";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.Contains(result, c => c.Row == 0 && c.Col == 0 && c.Value == "Alice");
        Assert.Contains(result, c => c.Row == 1 && c.Col == 1 && c.Value == "25");
    }

    [Fact]
    public void Parse_StripsMultipleCodeFences()
    {
        var input = "```\nAlice\t30\n```\n```\nBob\t25\n```";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.Contains(result, c => c.Value == "Alice");
        Assert.Contains(result, c => c.Value == "Bob");
    }

    // ── Preamble stripping ──────────────────────────────────────────────

    [Fact]
    public void Parse_SkipsPreambleBeforeData()
    {
        var input = "Here are the results:\n\nAlice\t30\nBob\t25";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Here"));
        Assert.Contains(result, c => c.Row == 0 && c.Col == 0 && c.Value == "Alice");
    }

    [Fact]
    public void Parse_SkipsVerbosePreamble()
    {
        var input = "Sure, here is the data you requested:\n\nI generated this from the available information.\n\nAlice\t30\tNY\nBob\t25\tLA";
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Sure"));
        Assert.DoesNotContain(result, c => c.Value.Contains("generated"));
        Assert.Contains(result, c => c.Value == "Alice");
    }

    [Fact]
    public void Parse_SkipsMultiLinePreambleWithBlanks()
    {
        var input = "I'd be happy to help!\n\nLet me create that for you.\n\nAlice\t30\nBob\t25";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("happy"));
        Assert.DoesNotContain(result, c => c.Value.Contains("Let me"));
        Assert.Contains(result, c => c.Value == "Alice");
    }

    // ── Trailing fluff / postamble stripping ────────────────────────────

    [Fact]
    public void Parse_IgnoresTrailingFluff()
    {
        var input = "Alice\t30\nBob\t25\n\n\n\nHope this helps!";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Hope"));
        Assert.Equal(4, result.Count); // 2 rows × 2 cols
    }

    [Fact]
    public void Parse_IgnoresFollowUpQuestions()
    {
        var input = "Alice\t30\nBob\t25\n\n\n\nWould you like me to add more rows?";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Would"));
    }

    [Fact]
    public void Parse_IgnoresPostambleDisclaimer()
    {
        var input = "Alice\t30\nBob\t25\n\n\n\n# Note\n- These are approximate values";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Note"));
        Assert.DoesNotContain(result, c => c.Value.Contains("approximate"));
    }

    // ── Mixed preamble + code fences ────────────────────────────────────

    [Fact]
    public void Parse_PreambleBeforeCodeFence()
    {
        var input = "Here are the results:\n\n```tsv\nAlice\t30\nBob\t25\n```";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Here"));
        Assert.Contains(result, c => c.Value == "Alice");
        Assert.Contains(result, c => c.Value == "Bob");
    }

    [Fact]
    public void Parse_PreambleAndPostambleAroundCodeFence()
    {
        var input = "Sure! Here you go:\n\n```\nAlice\t30\nBob\t25\n```\n\nLet me know if you need changes!";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Sure"));
        Assert.DoesNotContain(result, c => c.Value.Contains("Let me"));
        Assert.Contains(result, c => c.Value == "Alice");
    }

    // ── Grid bounds enforcement ─────────────────────────────────────────

    [Fact]
    public void Parse_ClipsColumnsToGridWidth()
    {
        var input = "a\tb\tc\td\te";
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Col >= 3);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Parse_ClipsRowsToGridHeight()
    {
        var input = "r0\nr1\nr2\nr3\nr4\nr5\nr6";
        var result = OutputParser.Parse(input, gridCols: 1, gridRows: 3);

        Assert.DoesNotContain(result, c => c.Row >= 3);
    }

    // ── Single-cell fallback ────────────────────────────────────────────

    [Fact]
    public void Parse_NonStructuredOutput_FallsBackToSingleCell()
    {
        var input = "42";
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 5);

        Assert.Single(result);
        Assert.Equal(0, result[0].Row);
        Assert.Equal(0, result[0].Col);
        Assert.Equal("42", result[0].Value);
    }

    // ── Cell truncation ─────────────────────────────────────────────────

    [Fact]
    public void Parse_TruncatesLongSingleCellValue()
    {
        var longValue = new string('x', 600);
        var result = OutputParser.Parse(longValue, gridCols: 3, gridRows: 5);

        Assert.Single(result);
        Assert.Equal(501, result[0].Value.Length); // 500 chars + "…"
        Assert.EndsWith("…", result[0].Value);
    }

    // ── Whitespace trimming ─────────────────────────────────────────────

    [Fact]
    public void Parse_TrimsCellValues()
    {
        var input = "  Alice  \t  30  \n  Bob  \t  25  ";
        var result = OutputParser.Parse(input, gridCols: 2, gridRows: 5);

        Assert.Contains(result, c => c.Value == "Alice");
        Assert.Contains(result, c => c.Value == "30");
    }

    [Fact]
    public void Parse_SkipsEmptyCellsAfterTrimming()
    {
        var input = "Alice\t\t30";
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 5);

        // Middle column is empty after trim, should be skipped
        Assert.DoesNotContain(result, c => c.Col == 1);
        Assert.Contains(result, c => c.Col == 0 && c.Value == "Alice");
        Assert.Contains(result, c => c.Col == 2 && c.Value == "30");
    }

    // ── Prose / markdown detection ──────────────────────────────────────

    [Fact]
    public void Parse_SingleColumn_SkipsMarkdownHeaders()
    {
        var input = "# Header\nActualData\n## Another Header";
        var result = OutputParser.Parse(input, gridCols: 1, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Header"));
        Assert.Contains(result, c => c.Value == "ActualData");
    }

    [Fact]
    public void Parse_SingleColumn_SkipsBulletPoints()
    {
        var input = "- first item\n* second item\nActualData";
        var result = OutputParser.Parse(input, gridCols: 1, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.StartsWith("-"));
        Assert.DoesNotContain(result, c => c.Value.StartsWith("*"));
        Assert.Contains(result, c => c.Value == "ActualData");
    }

    [Fact]
    public void Parse_SingleColumn_SkipsQuestions()
    {
        var input = "Do you want more?\nActualData\nWould you like details?";
        var result = OutputParser.Parse(input, gridCols: 1, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("?"));
        Assert.Contains(result, c => c.Value == "ActualData");
    }

    // ── Realistic end-to-end scenarios ──────────────────────────────────

    [Fact]
    public void Parse_RealisticCopilotResponse_WithPreambleAndPostamble()
    {
        var input = """
            Sure, here's a table of the top 3 programming languages:

            ```
            Python	1	General Purpose
            JavaScript	2	Web Development
            Rust	3	Systems Programming
            ```

            Let me know if you'd like more details or a different ranking!
            """;
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 5);

        Assert.DoesNotContain(result, c => c.Value.Contains("Sure"));
        Assert.DoesNotContain(result, c => c.Value.Contains("Let me"));
        Assert.Contains(result, c => c.Value == "Python");
        Assert.Contains(result, c => c.Value == "JavaScript");
        Assert.Contains(result, c => c.Value == "Rust");
    }

    [Fact]
    public void Parse_RealisticCopilotResponse_CommaSeparatedWithFluff()
    {
        var input = """
            Here are some sample cities and populations:

            Tokyo,37400068,Japan
            Delhi,30290936,India
            Shanghai,27058480,China

            Note: These are approximate 2023 metropolitan area populations.
            """;
        var result = OutputParser.Parse(input, gridCols: 3, gridRows: 10);

        Assert.DoesNotContain(result, c => c.Value.Contains("Here"));
        Assert.DoesNotContain(result, c => c.Value.Contains("Note"));
        Assert.Contains(result, c => c.Value == "Tokyo");
        Assert.Contains(result, c => c.Value == "Shanghai");
    }
}
