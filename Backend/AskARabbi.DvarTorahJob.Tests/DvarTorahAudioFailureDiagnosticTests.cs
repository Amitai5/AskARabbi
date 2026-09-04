using AskARabbiLIB.DvarTorah.Audio;
using Azure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.DvarTorahJob.Tests;

[TestClass]
public sealed class DvarTorahAudioFailureDiagnosticTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void FromException_SpeechAlignmentFailure_ExposesSafeStageWithoutRawDetails()
    {
        var exception = new DvarTorahAudioException("WordAlignmentFailed", "alignment", new InvalidDataException("Raw provider text must not be exposed"));

        var diagnostic = DvarTorahAudioFailureDiagnostic.FromException(exception);

        Assert.AreEqual("WordAlignmentFailed", diagnostic.FailureCode);
        Assert.AreEqual("alignment", diagnostic.Stage);
        Assert.AreEqual(nameof(InvalidDataException), diagnostic.InnerExceptionType);
        Assert.IsNull(diagnostic.ProviderErrorCode);
        Assert.IsFalse(diagnostic.ToString().Contains("Raw provider text", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FromException_AggregateWithBlobFailure_ExposesSafeStatusAndCode()
    {
        var exception = new AggregateException(new RequestFailedException(403, "Raw provider diagnostic", "AuthorizationPermissionMismatch", null), new IOException("Lease cleanup failed"));

        var diagnostic = DvarTorahAudioFailureDiagnostic.FromException(exception);

        Assert.AreEqual("storage", diagnostic.Stage);
        Assert.AreEqual(403, diagnostic.ProviderStatus);
        Assert.AreEqual("AuthorizationPermissionMismatch", diagnostic.ProviderErrorCode);
        Assert.IsFalse(diagnostic.ToString().Contains("Raw provider diagnostic", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("Bearer a-secret")]
    [DataRow("code\ncredential")]
    [TestCategory("Unit")]
    public void FromException_UnsafeProviderCode_DoesNotLogIt(string code)
    {
        var exception = new RequestFailedException(403, "Raw details", code, null);

        var diagnostic = DvarTorahAudioFailureDiagnostic.FromException(exception);

        Assert.IsNull(diagnostic.ProviderErrorCode);
        Assert.AreEqual(403, diagnostic.ProviderStatus);
    }
}
