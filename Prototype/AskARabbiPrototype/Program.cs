namespace AskARabbiPrototype;

internal static class Program
{
    private static async Task<int> Main(string[] arguments)
    {
        using var cancellationSource = new CancellationTokenSource();
        System.Console.CancelKeyPress += (_, eventArguments) =>
        {
            eventArguments.Cancel = true;
            cancellationSource.Cancel();
        };

        var application = new ConsoleApplication(cancellationSource.Token);
        return await application.RunAsync(arguments).ConfigureAwait(false);
    }
}
