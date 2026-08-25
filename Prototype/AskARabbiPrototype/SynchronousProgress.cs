namespace AskARabbiPrototype;

internal sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> report;

    internal SynchronousProgress(Action<T> report)
    {
        ArgumentNullException.ThrowIfNull(report);
        this.report = report;
    }

    public void Report(T value) => report(value);
}
