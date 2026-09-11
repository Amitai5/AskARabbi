namespace AskARabbiLIB.ConversationSettings;

/// <summary>Controls reading typography and presentation without changing conversation content.</summary>
public sealed record ReadingPreferences
{
    public string TextSize { get; init; } = "default";
    public string LineSpacing { get; init; } = "default";
    public string Theme { get; init; } = "system";
    public bool FocusLongContent { get; init; }

    /// <summary>Rejects unsupported reading presets before persistence.</summary>
    public void Validate()
    {
        if (TextSize is not ("small" or "default" or "large" or "extra-large"))
        {
            throw new ArgumentException("Choose a supported text size.", nameof(TextSize));
        }
        if (LineSpacing is not ("compact" or "default" or "relaxed"))
        {
            throw new ArgumentException("Choose a supported line spacing.", nameof(LineSpacing));
        }
        if (Theme is not ("light" or "dark" or "system"))
        {
            throw new ArgumentException("Choose light, dark, or system theme.", nameof(Theme));
        }
    }
}
