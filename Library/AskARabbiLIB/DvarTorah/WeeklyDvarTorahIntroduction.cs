namespace AskARabbiLIB.DvarTorah;

internal static class WeeklyDvarTorahIntroduction
{
    internal const string Text = "Welcome to AskARabbi's weekly D'var Torah. Let's explore this week's Torah reading and one idea to carry into our lives.";

    internal static string Prepend(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var content = body.TrimStart();
        while (content.StartsWith(Text, StringComparison.Ordinal))
        {
            content = content[Text.Length..].TrimStart();
        }

        return $"{Text}\n\n{content}";
    }
}
