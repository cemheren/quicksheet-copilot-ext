using CopilotExt;

namespace CopilotExt.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void Build_SingleCell_ProducesConcisePrompt()
    {
        var result = PromptBuilder.Build("What is 2+2?", cols: 1, rows: 1);

        Assert.Contains("short", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("concise", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("What is 2+2?", result);
        Assert.DoesNotContain("TSV", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_MultiCell_ProducesTsvPrompt()
    {
        var result = PromptBuilder.Build("list cities", cols: 3, rows: 5);

        Assert.Contains("tab-separated", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5 rows", result);
        Assert.Contains("3 columns", result);
        Assert.Contains("list cities", result);
    }

    [Fact]
    public void Build_MultiCell_ForbidsMarkdown()
    {
        var result = PromptBuilder.Build("anything", cols: 2, rows: 2);

        Assert.Contains("NO markdown", result);
        Assert.Contains("NO code fences", result);
    }

    [Fact]
    public void Build_UserPromptIsEmbedded()
    {
        var prompt = "generate a multiplication table";
        var result = PromptBuilder.Build(prompt, cols: 5, rows: 5);

        Assert.Contains(prompt, result);
    }
}
