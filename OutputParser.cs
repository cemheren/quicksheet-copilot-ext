using System.Text.RegularExpressions;

namespace CopilotExt;

/// <summary>
/// Parses AI CLI output into structured cell writes.
/// Strips markdown fences, preamble text, and trailing fluff.
/// Normalizes TSV/CSV into grid-sized cell writes.
/// </summary>
static class OutputParser
{
    private static readonly Regex CodeFencePattern = new(@"^```\w*\s*$", RegexOptions.Compiled);

    public static List<CellWrite> Parse(string rawOutput, int gridCols, int gridRows)
    {
        var cells = new List<CellWrite>();
        if (string.IsNullOrWhiteSpace(rawOutput))
            return cells;

        var lines = rawOutput.Split('\n', StringSplitOptions.None);

        // Strip markdown code fences
        lines = StripCodeFences(lines);

        // Find data lines (lines containing delimiters)
        var dataLines = ExtractDataLines(lines, gridCols);

        // If we couldn't find structured data, treat whole output as single-cell
        if (dataLines.Count == 0)
        {
            string trimmed = rawOutput.Trim();
            if (trimmed.Length > 0)
                cells.Add(new CellWrite { Row = 0, Col = 0, Value = TruncateCell(trimmed) });
            return cells;
        }

        // Convert data lines to cell writes, respecting grid bounds
        for (int row = 0; row < Math.Min(dataLines.Count, gridRows); row++)
        {
            string[] columns = SplitLine(dataLines[row]);
            for (int col = 0; col < Math.Min(columns.Length, gridCols); col++)
            {
                string value = columns[col].Trim();
                if (!string.IsNullOrEmpty(value))
                    cells.Add(new CellWrite { Row = row, Col = col, Value = value });
            }
        }

        return cells;
    }

    private static string[] StripCodeFences(string[] lines)
    {
        var result = new List<string>();
        bool insideFence = false;

        foreach (var line in lines)
        {
            if (CodeFencePattern.IsMatch(line.Trim()))
            {
                insideFence = !insideFence;
                continue; // skip the fence line itself
            }
            result.Add(line);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Extracts lines that look like structured data (contain tab or comma delimiters).
    /// Skips preamble (non-data lines before first data line) and trailing fluff.
    /// </summary>
    private static List<string> ExtractDataLines(string[] lines, int expectedCols)
    {
        var dataLines = new List<string>();
        bool foundData = false;
        int trailingNonData = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (foundData) trailingNonData++;
                continue;
            }

            if (IsDataLine(line, expectedCols))
            {
                foundData = true;
                trailingNonData = 0;
                dataLines.Add(line);
            }
            else if (foundData && trailingNonData > 2)
            {
                // Too many non-data lines after data — stop (trailing fluff)
                break;
            }
            else if (!foundData)
            {
                // Preamble — skip
                continue;
            }
        }

        return dataLines;
    }

    /// <summary>
    /// Heuristic: a line is "data" if it contains tabs, or has the right number of comma-separated values.
    /// </summary>
    private static bool IsDataLine(string line, int expectedCols)
    {
        // Tab-separated is the strongest signal
        if (line.Contains('\t'))
            return true;

        // Comma-separated with approximately the right number of columns
        if (expectedCols > 1)
        {
            int commaCount = line.Count(c => c == ',');
            if (commaCount >= expectedCols - 1)
                return true;
        }

        // Single column: any non-empty line that doesn't look like prose
        if (expectedCols == 1 && !LooksLikeProseOrMarkdown(line))
            return true;

        return false;
    }

    private static bool LooksLikeProseOrMarkdown(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith('#')) return true;
        if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ")) return true;
        if (trimmed.StartsWith("Here")) return true;
        if (trimmed.StartsWith("Sure")) return true;
        if (trimmed.StartsWith("I ")) return true;
        if (trimmed.StartsWith("Let me")) return true;
        if (trimmed.StartsWith("Would you")) return true;
        if (trimmed.StartsWith("Do you")) return true;
        if (trimmed.EndsWith('?')) return true;
        return false;
    }

    private static string[] SplitLine(string line)
    {
        // Prefer tab-separated
        if (line.Contains('\t'))
            return line.Split('\t');

        // Fall back to comma-separated
        return line.Split(',');
    }

    private static string TruncateCell(string value, int maxLen = 500)
    {
        if (value.Length <= maxLen) return value;
        return value[..maxLen] + "…";
    }
}
