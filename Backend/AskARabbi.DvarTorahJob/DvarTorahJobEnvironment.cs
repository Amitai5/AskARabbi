using System.Globalization;

namespace AskARabbi.DvarTorahJob;

internal static class DvarTorahJobEnvironment
{
    internal static bool IsGenerationEnabled() => GetBoolean("DvarTorah__GenerationEnabled", false);

    internal static string GetRequired(string name)
    {
        return GetOptional(name) ?? throw new DvarTorahJobConfigurationException($"The required {name} environment variable is missing.");
    }

    internal static string? GetOptional(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    internal static bool GetBoolean(string name, bool defaultValue)
    {
        var value = GetOptional(name);
        if (value is null)
        {
            return defaultValue;
        }

        return bool.TryParse(value, out var result)
            ? result
            : throw new DvarTorahJobConfigurationException($"The {name} environment variable must be true or false.");
    }

    internal static int GetInteger(string name, int defaultValue)
    {
        var value = GetOptional(name);
        if (value is null)
        {
            return defaultValue;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new DvarTorahJobConfigurationException($"The {name} environment variable must be an integer.");
    }
}
