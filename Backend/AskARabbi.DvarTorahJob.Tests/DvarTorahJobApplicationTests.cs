using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.DvarTorahJob.Tests;

[TestClass]
public sealed class DvarTorahJobApplicationTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_WhenGenerationIsDisabled_DoesNotCreateCoordinator()
    {
        var wasCoordinatorCreated = false;
        var application = new DvarTorahJobApplication(
            () => false,
            _ =>
            {
                wasCoordinatorCreated = true;
                throw new InvalidOperationException("The coordinator must not be created while generation is disabled.");
            },
            () => "test-invocation");

        var result = await application.RunAsync();

        Assert.IsNull(result);
        Assert.IsFalse(wasCoordinatorCreated);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_WhenGenerationIsEnabled_PropagatesCoordinatorCreationFailure()
    {
        var expected = new InvalidOperationException("Expected failure");
        var application = new DvarTorahJobApplication(() => true, _ => Task.FromException<AskARabbiLIB.DvarTorah.WeeklyDvarTorahGenerationCoordinator>(expected), () => "test-invocation");

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => application.RunAsync());

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task RunAsync_WhenCanceledBeforeStart_DoesNotReadConfiguration()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var wasConfigurationRead = false;
        var application = new DvarTorahJobApplication(
            () =>
            {
                wasConfigurationRead = true;
                return false;
            },
            _ => throw new InvalidOperationException("The coordinator must not be created after cancellation."),
            () => "test-invocation");

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => application.RunAsync(cancellation.Token));

        Assert.IsFalse(wasConfigurationRead);
    }
}
