using System.Text.RegularExpressions;

namespace AskARabbiLIB.DvarTorah.Audio;

/// <summary>Provides safe narration diagnostics without exposing provider credentials, SSML, or article text.</summary>
public sealed class DvarTorahAudioException : InvalidOperationException
{
    /// <summary>Initializes a safe stage failure while retaining the original exception for diagnostics.</summary>
    /// <param name="failureCode">Stable alphanumeric failure code.</param>
    /// <param name="stage">Stable operation stage.</param>
    /// <param name="innerException">Original failure, if one exists.</param>
    public DvarTorahAudioException(string failureCode, string stage, Exception? innerException = null) : base($"Dvar Torah narration failed in {stage} ({failureCode}).", innerException)
    {
        if (string.IsNullOrWhiteSpace(failureCode) || !Regex.IsMatch(failureCode, "^[A-Za-z0-9_]{1,100}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("A safe narration failure code is required.", nameof(failureCode));
        }
        if (string.IsNullOrWhiteSpace(stage) || !Regex.IsMatch(stage, "^[A-Za-z0-9_]{1,40}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("A safe narration stage is required.", nameof(stage));
        }
        FailureCode = failureCode;
        Stage = stage;
    }

    public string FailureCode { get; }
    public string Stage { get; }
}
