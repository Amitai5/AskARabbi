using AskARabbiLIB.DvarTorah.Audio;
using Azure;

namespace AskARabbi.DvarTorahJob;

internal sealed record DvarTorahAudioFailureDiagnostic(string FailureCode, string? Stage, string ExceptionType, string? InnerExceptionType, int? ProviderStatus, string? ProviderErrorCode, string? StackTrace)
{
    internal static DvarTorahAudioFailureDiagnostic FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var pending = new Queue<Exception>();
        pending.Enqueue(exception);
        DvarTorahAudioException? audioFailure = null;
        RequestFailedException? providerFailure = null;
        for (var visited = 0; pending.Count > 0 && visited < 16; visited++)
        {
            var current = pending.Dequeue();
            audioFailure ??= current as DvarTorahAudioException;
            providerFailure ??= current as RequestFailedException;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.Take(16 - visited))
                {
                    pending.Enqueue(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Enqueue(current.InnerException);
            }
        }

        return new DvarTorahAudioFailureDiagnostic(
            audioFailure?.FailureCode ?? exception.GetType().Name,
            audioFailure?.Stage ?? (providerFailure is not null ? "storage" : null),
            exception.GetType().Name,
            exception.InnerException?.GetType().Name,
            providerFailure?.Status,
            GetSafeProviderCode(providerFailure?.ErrorCode),
            exception.StackTrace);
    }

    private static string? GetSafeProviderCode(string? value) => value is { Length: > 0 and <= 120 } && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.') ? value : null;
}
