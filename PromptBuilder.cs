namespace CopilotExt;

/// <summary>
/// Builds the system prompt that instructs the AI to output structured TSV data.
/// </summary>
static class PromptBuilder
{
    public static string Build(string userPrompt, int cols, int rows)
    {
        // For single-cell output, don't force TSV — just ask for a concise answer
        if (cols == 1 && rows == 1)
        {
            return $"""
                Reply with ONLY a short, concise answer. No explanation, no markdown, no code fences.
                Keep it under 100 characters if possible.

                {userPrompt}
                """;
        }

        return $"""
            CRITICAL FORMATTING RULES — FOLLOW EXACTLY:
            - Output ONLY tab-separated values (TSV), one row per line
            - Output exactly {rows} rows and {cols} columns
            - NO markdown, NO code fences, NO preamble, NO explanation, NO follow-up questions
            - Do NOT wrap output in ```
            - Do NOT add headers unless the user asked for them
            - If you cannot fulfill the request, output a single line with an error description
            - Start your response with the first data row IMMEDIATELY — no text before it

            User request: {userPrompt}
            """;
    }
}
