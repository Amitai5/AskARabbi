using AskARabbi.Api.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbi.Api.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class SessionLifetimeOptionsTests
{
    [TestMethod]
    public void Validate_Defaults_UsesThirtyDayMaximumAndSevenDayInactivity()
    {
        var options = new SessionLifetimeOptions();

        options.Validate();

        Assert.AreEqual(30, options.MaximumLifetimeDays);
        Assert.AreEqual(7, options.InactivityTimeoutDays);
    }

    [TestMethod]
    [DataRow(0, 7)]
    [DataRow(-1, 7)]
    [DataRow(366, 7)]
    [DataRow(30, 0)]
    [DataRow(30, -1)]
    [DataRow(30, 31)]
    public void Validate_InvalidLimits_RejectsConfiguration(int maximumDays, int inactivityDays)
    {
        var options = new SessionLifetimeOptions { MaximumLifetimeDays = maximumDays, InactivityTimeoutDays = inactivityDays };

        Assert.ThrowsExactly<InvalidOperationException>(options.Validate);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(14, 3)]
    [DataRow(365, 365)]
    public void Validate_ConfiguredLimits_BindsAndAcceptsValidBoundaries(int maximumDays, int inactivityDays)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Session:MaximumLifetimeDays"] = maximumDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Session:InactivityTimeoutDays"] = inactivityDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }).Build();

        var options = configuration.GetSection(SessionLifetimeOptions.SectionName).Get<SessionLifetimeOptions>();

        Assert.IsNotNull(options);
        options.Validate();
        Assert.AreEqual(maximumDays, options.MaximumLifetimeDays);
        Assert.AreEqual(inactivityDays, options.InactivityTimeoutDays);
    }
}
