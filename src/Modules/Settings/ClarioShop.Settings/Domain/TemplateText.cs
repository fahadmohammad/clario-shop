namespace ClarioShop.Settings.Domain;

/// <summary>Checks the text of a notification template before it is saved.</summary>
internal static class TemplateText
{
    /// <summary>
    /// What is wrong with the placeholders in <paramref name="text"/>, or null when nothing is. Braces are only for
    /// placeholders, and each must be one of <paramref name="placeholders"/>, spelled exactly (they are filled in
    /// as written, so <c>{ordernumber}</c> would reach the shopper as it is).
    /// </summary>
    public static string? FindProblem(string text, IReadOnlyList<string> placeholders)
    {
        var position = 0;
        while (position < text.Length)
        {
            var brace = text.IndexOfAny(['{', '}'], position);
            if (brace < 0)
            {
                return null;
            }

            var close = text[brace] == '{' ? text.IndexOfAny(['{', '}'], brace + 1) : -1;
            if (close < 0 || text[close] != '}')
            {
                return $"Braces are only for placeholders such as {{{placeholders[0]}}}; this one at position {brace + 1} is not closed or not opened.";
            }

            var name = text[(brace + 1)..close];
            if (!placeholders.Contains(name, StringComparer.Ordinal))
            {
                return $"{{{name}}} is not a placeholder for this message. Use {string.Join(", ", placeholders.Select(placeholder => $"{{{placeholder}}}"))}.";
            }

            position = close + 1;
        }

        return null;
    }
}
