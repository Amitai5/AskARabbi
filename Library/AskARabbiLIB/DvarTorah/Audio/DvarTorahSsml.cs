using System.Text;
using System.Text.RegularExpressions;

namespace AskARabbiLIB.DvarTorah.Audio;

internal sealed partial class DvarTorahSsml
{
    private readonly int[] displayPositions;
    internal string Text { get; }

    internal DvarTorahSsml(NarrationChunk chunk, string voice)
    {
        var text = new StringBuilder();
        var positions = new List<int>();
        void AddMarkup(string markup)
        {
            text.Append(markup);
            positions.AddRange(Enumerable.Repeat(-1, markup.Length));
        }
        void AddContent(int start, int length)
        {
            for (var index = start; index < start + length; index++)
            {
                var escaped = chunk.Text[index] switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", '"' => "&quot;", '\'' => "&apos;", _ => chunk.Text[index].ToString() };
                text.Append(escaped);
                positions.AddRange(Enumerable.Repeat(chunk.DisplayOffset + index, escaped.Length));
            }
        }

        AddMarkup($"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'><voice name='{voice}'>");
        var cursor = 0;
        foreach (Match hebrew in HebrewRunPattern().Matches(chunk.Text))
        {
            if (hebrew.Index > cursor)
            {
                AddMarkup("<lang xml:lang='en-US'>");
                AddContent(cursor, hebrew.Index - cursor);
                AddMarkup("</lang>");
            }
            AddMarkup("<lang xml:lang='he-IL'>");
            AddContent(hebrew.Index, hebrew.Length);
            AddMarkup("</lang>");
            cursor = hebrew.Index + hebrew.Length;
        }
        if (cursor < chunk.Text.Length)
        {
            AddMarkup("<lang xml:lang='en-US'>");
            AddContent(cursor, chunk.Text.Length - cursor);
            AddMarkup("</lang>");
        }
        AddMarkup("</voice></speak>");
        Text = text.ToString();
        displayPositions = positions.ToArray();
    }

    internal int GetDisplayOffset(uint ssmlOffset) => ssmlOffset < displayPositions.Length ? displayPositions[ssmlOffset] : -1;

    [GeneratedRegex(@"[\u0590-\u05ff]+(?:[ \t\u200e\u200f,.;:!?״׳'’“”\-–—]+[\u0590-\u05ff]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex HebrewRunPattern();
}
