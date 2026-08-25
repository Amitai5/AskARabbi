using System.Globalization;
using System.Text;
using AskARabbiLIB.Profiles;
using Spectre.Console;

namespace AskARabbiPrototype;

internal static class UserProfileConsole
{
    private const string ExampleProfileFileName = "profile.example.json";

    internal static UserProfile? Prompt(string repositoryRoot, DateOnly currentDate)
    {
        var profileDirectory = GetProfileDirectory(repositoryRoot);
        Directory.CreateDirectory(profileDirectory);
        var choices = LoadSavedProfiles(profileDirectory, currentDate)
            .Select(profile => new ProfileChoice(ProfileChoiceKind.Saved, profile.Profile, profile.FileName))
            .ToList();
        choices.Add(new ProfileChoice(ProfileChoiceKind.Custom, null, null));
        choices.Add(new ProfileChoice(ProfileChoiceKind.Back, null, null));

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold cyan]A little context before we chat[/]"));
        AnsiConsole.MarkupLine("[grey]This helps AskARabbi choose clearer language and recognize potentially relevant community differences without assuming what you believe or practice.[/]");
        AnsiConsole.MarkupLine("[grey]Saved JSON profiles stay local and are ignored by Git. Azure receives your calculated age, not your exact date of birth.[/]");
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<ProfileChoice>()
                .Title("Choose a saved profile or enter context for this chat:")
                .PageSize(12)
                .MoreChoicesText("[grey](Move up and down to see more profiles)[/]")
                .UseConverter(profileChoice => FormatChoice(profileChoice, currentDate))
                .AddChoices(choices));

        return choice.Kind switch
        {
            ProfileChoiceKind.Saved => GetSavedProfile(choice),
            ProfileChoiceKind.Custom => PromptCustomProfile(profileDirectory, currentDate),
            ProfileChoiceKind.Back => null,
            _ => throw new InvalidOperationException($"Unsupported profile choice: {choice.Kind}."),
        };
    }

    internal static UserProfile Load(string repositoryRoot, string profileFileName, DateOnly currentDate)
    {
        var profilePath = ResolveProfilePath(GetProfileDirectory(repositoryRoot), profileFileName);
        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Profile '{Path.GetFileName(profilePath)}' was not found in Prototype/Profiles.", profilePath);
        }
        return UserProfileJsonSerializer.Deserialize(File.ReadAllText(profilePath), currentDate);
    }

    internal static string FormatSummary(UserProfile profile, DateOnly currentDate)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var religiousBackground = string.IsNullOrWhiteSpace(profile.ReligiousBackground) ? "religious background not specified" : profile.ReligiousBackground.Trim();
        return $"{profile.Name.Trim()}, age {profile.CalculateAge(currentDate)} — {profile.JewishHeritage.Trim()}; {religiousBackground}";
    }

    private static IReadOnlyList<SavedProfile> LoadSavedProfiles(string profileDirectory, DateOnly currentDate)
    {
        var profiles = new List<SavedProfile>();
        foreach (var profilePath in Directory.EnumerateFiles(profileDirectory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(profilePath);
            if (string.Equals(fileName, ExampleProfileFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            try
            {
                var profile = UserProfileJsonSerializer.Deserialize(File.ReadAllText(profilePath), currentDate);
                profiles.Add(new SavedProfile(fileName, profile));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or InvalidOperationException)
            {
                AnsiConsole.MarkupLine($"[yellow]Skipped profile {Markup.Escape(fileName)}: {Markup.Escape(exception.Message)}[/]");
            }
        }
        return profiles.OrderBy(profile => profile.Profile.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static UserProfile? PromptCustomProfile(string profileDirectory, DateOnly currentDate)
    {
        while (true)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold cyan]Create context for this chat[/]");
            var name = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Name[/]:")
                    .ValidationErrorMessage("[red]Enter a name containing no more than 120 characters.[/]")
                    .Validate(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 120)).Trim();
            var dateOfBirth = PromptDateOfBirth(currentDate);
            var bio = NormalizeOptional(AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Brief bio[/] [grey](optional — who you are, what you do, or what matters to you)[/]:")
                    .AllowEmpty()
                    .ValidationErrorMessage("[red]Bio cannot exceed 2,000 characters.[/]")
                    .Validate(value => value.Trim().Length <= 2_000)));
            var religiousBackground = PromptReligiousBackground();
            var jewishHeritage = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Jewish heritage/community background[/] [grey](required — e.g. Ashkenazi, Sephardi, Mizrahi, mixed, convert, or not sure)[/]:")
                    .ValidationErrorMessage("[red]Enter a background containing no more than 250 characters.[/]")
                    .Validate(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 250)).Trim();

            var profile = new UserProfile
            {
                Name = name,
                DateOfBirth = dateOfBirth,
                Bio = bio,
                ReligiousBackground = religiousBackground,
                JewishHeritage = jewishHeritage,
            };
            profile.Validate(currentDate);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Profile summary:[/] {Markup.Escape(FormatSummary(profile, currentDate))}");
            if (profile.Bio is not null)
            {
                AnsiConsole.MarkupLine($"[grey]Bio:[/] {Markup.Escape(profile.Bio)}");
            }
            if (!AnsiConsole.Confirm("Use this context for the chat?", true))
            {
                if (AnsiConsole.Confirm("Enter the profile again?", true))
                {
                    continue;
                }
                return null;
            }

            if (AnsiConsole.Confirm("Save this profile locally for future chats?", false))
            {
                SaveProfile(profileDirectory, profile, currentDate);
            }
            return profile;
        }
    }

    private static DateOnly PromptDateOfBirth(DateOnly currentDate)
    {
        while (true)
        {
            var value = AnsiConsole.Prompt(new TextPrompt<string>("[cyan]Date of birth[/] [grey](MM/DD/YYYY)[/]:")).Trim();
            if (!DateOnly.TryParseExact(value, ["M/d/yyyy", "MM/dd/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOfBirth))
            {
                AnsiConsole.MarkupLine("[red]Enter a valid date such as 12/17/2001.[/]");
                continue;
            }
            try
            {
                var validationProfile = new UserProfile { Name = "Validation", DateOfBirth = dateOfBirth, JewishHeritage = "Validation" };
                validationProfile.Validate(currentDate);
                AnsiConsole.MarkupLine($"[grey]Calculated age: {validationProfile.CalculateAge(currentDate)}[/]");
                return dateOfBirth;
            }
            catch (ArgumentException exception)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
            }
            catch (InvalidOperationException exception)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]");
            }
        }
    }

    private static string? PromptReligiousBackground()
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<ReligiousBackgroundChoice>()
                .Title("[cyan]Religious background or movement[/] [grey](optional)[/]:")
                .UseConverter(FormatReligiousBackgroundChoice)
                .AddChoices(Enum.GetValues<ReligiousBackgroundChoice>()));
        return choice switch
        {
            ReligiousBackgroundChoice.NotSpecified => null,
            ReligiousBackgroundChoice.Reform => "Reform",
            ReligiousBackgroundChoice.Conservative => "Conservative / Masorti",
            ReligiousBackgroundChoice.ModernOrthodox => "Modern Orthodox",
            ReligiousBackgroundChoice.Orthodox => "Orthodox",
            ReligiousBackgroundChoice.Haredi => "Haredi",
            ReligiousBackgroundChoice.Traditional => "Traditional",
            ReligiousBackgroundChoice.SecularCultural => "Secular / cultural",
            ReligiousBackgroundChoice.Reconstructionist => "Reconstructionist",
            ReligiousBackgroundChoice.Renewal => "Jewish Renewal",
            ReligiousBackgroundChoice.Custom => NormalizeOptional(AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Describe your religious background[/]:")
                    .ValidationErrorMessage("[red]Enter a description containing no more than 250 characters.[/]")
                    .Validate(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 250))),
            _ => throw new InvalidOperationException($"Unsupported religious-background choice: {choice}."),
        };
    }

    private static void SaveProfile(string profileDirectory, UserProfile profile, DateOnly currentDate)
    {
        var defaultName = CreateFileStem(profile.Name);
        var fileStem = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan]Profile file name[/] [grey](letters, numbers, dots, dashes, and underscores)[/]:")
                .DefaultValue(defaultName)
                .ValidationErrorMessage("[red]Enter a safe file name containing no more than 80 characters.[/]")
                .Validate(IsSafeFileStem)).Trim();
        var profilePath = ResolveProfilePath(profileDirectory, $"{fileStem}.json");
        if (File.Exists(profilePath) && !AnsiConsole.Confirm($"Overwrite {Markup.Escape(Path.GetFileName(profilePath))}?", false))
        {
            AnsiConsole.MarkupLine("[yellow]The profile was used for this chat but was not saved.[/]");
            return;
        }

        var json = UserProfileJsonSerializer.Serialize(profile, currentDate);
        File.WriteAllText(profilePath, $"{json}{Environment.NewLine}", new UTF8Encoding(false));
        AnsiConsole.MarkupLine($"[green]Saved locally as Prototype/Profiles/{Markup.Escape(Path.GetFileName(profilePath))}. Git ignores personal profile JSON files.[/]");
    }

    private static string GetProfileDirectory(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        return Path.Combine(repositoryRoot, "Prototype", "Profiles");
    }

    private static string ResolveProfilePath(string profileDirectory, string profileFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileFileName);
        var normalizedName = profileFileName.Trim();
        if (!normalizedName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            normalizedName += ".json";
        }
        if (!string.Equals(Path.GetFileName(normalizedName), normalizedName, StringComparison.Ordinal) || normalizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Profile must be a JSON file name inside Prototype/Profiles, not a path.", nameof(profileFileName));
        }

        var resolvedDirectory = Path.GetFullPath(profileDirectory);
        var resolvedPath = Path.GetFullPath(Path.Combine(resolvedDirectory, normalizedName));
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(resolvedPath), resolvedDirectory, pathComparison))
        {
            throw new ArgumentException("Profile must resolve inside Prototype/Profiles.", nameof(profileFileName));
        }

        return resolvedPath;
    }

    private static string FormatChoice(ProfileChoice choice, DateOnly currentDate) => choice.Kind switch
    {
        ProfileChoiceKind.Saved => FormatSavedChoice(choice, currentDate),
        ProfileChoiceKind.Custom => "[cyan]Enter a custom profile for this chat[/]",
        ProfileChoiceKind.Back => "[grey]Back to the main menu[/]",
        _ => choice.Kind.ToString(),
    };

    private static string FormatSavedChoice(ProfileChoice choice, DateOnly currentDate)
    {
        var profile = GetSavedProfile(choice);
        var fileName = choice.FileName ?? throw new InvalidOperationException("A saved profile choice is missing its file name.");
        return $"[green]{Markup.Escape(FormatSummary(profile, currentDate))}[/] [grey]({Markup.Escape(fileName)})[/]";
    }

    private static UserProfile GetSavedProfile(ProfileChoice choice)
    {
        return choice.Profile ?? throw new InvalidOperationException("A saved profile choice is missing its profile data.");
    }

    private static string FormatReligiousBackgroundChoice(ReligiousBackgroundChoice choice) => choice switch
    {
        ReligiousBackgroundChoice.NotSpecified => "Prefer not to say / not specified",
        ReligiousBackgroundChoice.Reform => "Reform",
        ReligiousBackgroundChoice.Conservative => "Conservative / Masorti",
        ReligiousBackgroundChoice.ModernOrthodox => "Modern Orthodox",
        ReligiousBackgroundChoice.Orthodox => "Orthodox",
        ReligiousBackgroundChoice.Haredi => "Haredi",
        ReligiousBackgroundChoice.Traditional => "Traditional",
        ReligiousBackgroundChoice.SecularCultural => "Secular / cultural",
        ReligiousBackgroundChoice.Reconstructionist => "Reconstructionist",
        ReligiousBackgroundChoice.Renewal => "Jewish Renewal",
        ReligiousBackgroundChoice.Custom => "Another or mixed description",
        _ => choice.ToString(),
    };

    private static string CreateFileStem(string name)
    {
        var builder = new StringBuilder();
        var previousWasSeparator = false;
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }
        var result = builder.ToString().Trim('-');
        return result.Length == 0 ? "profile" : result[..Math.Min(result.Length, 80)];
    }

    private static bool IsSafeFileStem(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is >= 1 and <= 80
            && trimmed is not "." and not ".."
            && trimmed.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private enum ProfileChoiceKind
    {
        Saved,
        Custom,
        Back,
    }

    private enum ReligiousBackgroundChoice
    {
        NotSpecified,
        Reform,
        Conservative,
        ModernOrthodox,
        Orthodox,
        Haredi,
        Traditional,
        SecularCultural,
        Reconstructionist,
        Renewal,
        Custom,
    }

    private sealed record ProfileChoice(ProfileChoiceKind Kind, UserProfile? Profile, string? FileName);

    private sealed record SavedProfile(string FileName, UserProfile Profile);
}
