using System.Runtime.InteropServices;
using AskARabbi.DvarTorahJob;

using var shutdown = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
Console.CancelKeyPress += cancelHandler;

PosixSignalRegistration? terminationRegistration = null;
if (!OperatingSystem.IsWindows())
{
    terminationRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        shutdown.Cancel();
    });
}

try
{
    var application = new DvarTorahJobApplication(DvarTorahJobEnvironment.IsGenerationEnabled, JobDependencyFactory.CreateCoordinatorAsync, () => Guid.NewGuid().ToString("N"));
    var result = await application.RunAsync(shutdown.Token).ConfigureAwait(false);
    if (result is null)
    {
        DvarTorahJobLog.GenerationDisabled();
    }
    else
    {
        DvarTorahJobLog.GenerationCompleted(result);
    }

    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    DvarTorahJobLog.GenerationCanceled();
    return 2;
}
catch (Exception exception)
{
    DvarTorahJobLog.GenerationFailed(exception);
    return 1;
}
finally
{
    terminationRegistration?.Dispose();
    Console.CancelKeyPress -= cancelHandler;
}
