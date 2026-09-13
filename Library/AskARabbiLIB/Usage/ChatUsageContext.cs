using AskARabbiLIB.AI;

namespace AskARabbiLIB.Usage;

/// <summary>Routes singleton provider clients' usage to the explicitly opened async chat scope.</summary>
public sealed class ChatUsageContext : IAIUsageObserver
{
    // Instance-owned async state isolates simultaneous users without a static service locator.
    private readonly AsyncLocal<Scope?> current = new();

    /// <summary>Gets whether this chat has received any provider usage reports.</summary>
    public bool HasReportedUsage => current.Value?.HasReportedUsage == true;

    /// <summary>Gets successfully recorded tokens from all provider stages in this chat, including retrieval.</summary>
    public long TokensRecorded => current.Value?.TokensRecorded ?? 0;

    /// <summary>Gets the number of distinct provider responses successfully recorded in this chat.</summary>
    public int ProviderResponsesRecorded => current.Value?.ProviderResponsesRecorded ?? 0;

    /// <summary>Opens an accounting scope in the caller's execution context.</summary>
    /// <param name="lease">Durable chat admission lease.</param>
    /// <param name="usage">Monthly accounting service.</param>
    /// <returns>A scope that must be disposed before releasing the lease.</returns>
    public IDisposable Begin(ChatUsageLease lease, MonthlyUsageService usage)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(usage);
        if (current.Value is not null)
        {
            throw new InvalidOperationException("A chat accounting scope is already active.");
        }
        var scope = new Scope(this, lease, usage);
        current.Value = scope;
        return scope;
    }

    /// <inheritdoc/>
    public Task BeforeRequestAsync(CancellationToken cancellationToken = default) => current.Value?.BeforeRequestAsync(cancellationToken) ?? Task.CompletedTask;

    /// <inheritdoc/>
    public Task RecordAsync(string? responseId, AIUsage usage) => current.Value?.RecordAsync(responseId, usage) ?? Task.CompletedTask;

    private sealed class Scope(ChatUsageContext owner, ChatUsageLease lease, MonthlyUsageService service) : IDisposable
    {
        private readonly SemaphoreSlim accounting = new(1, 1);
        private readonly HashSet<string> responseIds = new(StringComparer.Ordinal);
        private long totalTokens;
        private bool disposed;
        private ChatUsageException? failure;
        internal bool HasReportedUsage { get; private set; }
        internal long TokensRecorded { get; private set; }
        internal int ProviderResponsesRecorded { get; private set; }

        internal async Task BeforeRequestAsync(CancellationToken cancellationToken)
        {
            if (disposed || failure is not null)
            {
                throw failure ?? new ChatUsageException("usage_unavailable", "The chat request has ended. Please try again.");
            }
            try
            {
                await service.EnsureAvailableAsync(lease, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not ChatUsageException and not OperationCanceledException)
            {
                throw new ChatUsageException("usage_unavailable", "Your chat allowance could not be checked. Please try again shortly.", innerException: exception);
            }
        }

        internal async Task RecordAsync(string? responseId, AIUsage usage)
        {
            ArgumentNullException.ThrowIfNull(usage);
            ArgumentOutOfRangeException.ThrowIfNegative(usage.InputTokens);
            ArgumentOutOfRangeException.ThrowIfNegative(usage.OutputTokens);
            ArgumentOutOfRangeException.ThrowIfNegative(usage.TotalTokens);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await accounting.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                if (disposed || failure is not null)
                {
                    throw failure ?? new ChatUsageException("usage_unavailable", "The chat request has ended.");
                }
                if (!string.IsNullOrWhiteSpace(responseId) && !responseIds.Add(responseId))
                {
                    return;
                }
                // Output tokens already include reasoning. Cached input still counts once.
                totalTokens = checked(totalTokens + Math.Max(usage.TotalTokens, (long)usage.InputTokens + usage.OutputTokens));
                HasReportedUsage = true;
                await service.RecordTokensAsync(lease, totalTokens, timeout.Token).ConfigureAwait(false);
                TokensRecorded = totalTokens;
                ProviderResponsesRecorded++;
            }
            catch (ChatUsageException exception)
            {
                failure = exception;
                throw;
            }
            catch (Exception exception)
            {
                failure = new ChatUsageException("usage_unavailable", "Chat usage could not be saved. Please try again shortly.", innerException: exception);
                throw failure;
            }
            finally
            {
                accounting.Release();
            }
        }

        public void Dispose()
        {
            disposed = true;
            owner.current.Value = null;
            accounting.Dispose();
        }
    }
}
