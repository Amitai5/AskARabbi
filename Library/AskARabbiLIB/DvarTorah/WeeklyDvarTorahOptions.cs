namespace AskARabbiLIB.DvarTorah;

/// <summary>Configures weekly Dvar Torah calendar and orchestration behavior.</summary>
public sealed class WeeklyDvarTorahOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "DvarTorah";

    /// <summary>Gets whether publications follow the Israel rather than Diaspora reading cycle.</summary>
    public bool InIsrael { get; init; }

    /// <summary>Gets the generation lease duration in minutes.</summary>
    public int GenerationLeaseMinutes { get; init; } = 30;

    /// <summary>Gets the validated generation lease duration.</summary>
    public TimeSpan GenerationLeaseDuration => TimeSpan.FromMinutes(GenerationLeaseMinutes);

    /// <summary>Validates the orchestration configuration.</summary>
    public void Validate()
    {
        if (GenerationLeaseMinutes is < 1 or > 120)
        {
            throw new InvalidOperationException($"{SectionName}:{nameof(GenerationLeaseMinutes)} must be between 1 and 120.");
        }
    }
}
