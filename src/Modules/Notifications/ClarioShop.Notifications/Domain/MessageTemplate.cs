using System.Text;

namespace ClarioShop.Notifications.Domain;

/// <summary>
/// A message with placeholders in braces, e.g. <c>Your order {OrderNumber} is on its way.</c> A placeholder with no
/// value is left as it is, so a mistake shows instead of silently disappearing.
/// </summary>
/// <param name="Subject">Email only; null for SMS.</param>
internal sealed record MessageTemplate(string? Subject, string Body)
{
    public RenderedMessage Render(IReadOnlyDictionary<string, string> values) =>
        new(Subject is null ? null : Fill(Subject, values), Fill(Body, values));

    private static string Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        var result = new StringBuilder(text);
        foreach (var (name, value) in values)
        {
            result.Replace($"{{{name}}}", value);
        }

        return result.ToString();
    }
}
