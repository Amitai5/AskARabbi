namespace AskARabbiLIB.Usage;

/// <summary>Stops chat when its allowance or durable accounting is unavailable.</summary>
public sealed class ChatUsageException : Exception
{
    /// <summary>Initializes a safe, machine-readable chat admission failure.</summary>
    /// <param name="code">Stable quota, concurrency, or accounting failure code.</param>
    /// <param name="message">Safe user-facing explanation.</param>
    /// <param name="usage">Current allowance when available.</param>
    /// <param name="innerException">Underlying accounting failure, if any.</param>
    public ChatUsageException(string code, string message, BillingPeriodUsage? usage = null, Exception? innerException = null) : base(message, innerException)
    {
        Code = code;
        Usage = usage;
    }

    /// <summary>Gets the stable failure code.</summary>
    public string Code { get; }

    /// <summary>Gets the current allowance when available.</summary>
    public BillingPeriodUsage? Usage { get; }
}
