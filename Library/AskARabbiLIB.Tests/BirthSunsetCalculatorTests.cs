using AskARabbiLIB.Calendar;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
[TestCategory("Regression")]
public sealed class BirthSunsetCalculatorTests
{
    private static readonly CalendarLocation LosAngeles = new() { Kind = "zip", Id = "91302", Label = "Calabasas", TimeZone = "America/Los_Angeles", Latitude = 34.15778, Longitude = -118.63842 };

    [TestMethod]
    [DataRow(12, 17, 12, false)]
    [DataRow(12, 17, 18, true)]
    [DataRow(6, 17, 18, false)]
    [DataRow(6, 17, 22, true)]
    public void IsAfterSunset_WinterAndSummerBirths_UsesLocalClockAndDaylightSaving(int month, int day, int hour, bool expected)
    {
        var result = BirthSunsetCalculator.IsAfterSunset(new(2001, month, day, hour, 0, 0), LosAngeles);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(3, 8, 2, 30)]
    [DataRow(11, 1, 1, 30)]
    public void IsAfterSunset_InvalidOrAmbiguousDstClock_DoesNotGuess(int month, int day, int hour, int minute)
    {
        var result = BirthSunsetCalculator.IsAfterSunset(new(2026, month, day, hour, minute, 0), LosAngeles);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void IsAfterSunset_MissingInvalidOrPolarLocation_ReturnsUnknown()
    {
        var date = new DateTime(2001, 6, 21, 22, 0, 0);

        Assert.IsNull(BirthSunsetCalculator.IsAfterSunset(date, null));
        Assert.IsNull(BirthSunsetCalculator.IsAfterSunset(date, LosAngeles with { Latitude = null }));
        Assert.IsNull(BirthSunsetCalculator.IsAfterSunset(date, LosAngeles with { Longitude = double.NaN }));
        Assert.IsNull(BirthSunsetCalculator.IsAfterSunset(date, LosAngeles with { Latitude = 91 }));
        Assert.IsNull(BirthSunsetCalculator.IsAfterSunset(date, LosAngeles with { TimeZone = "Invalid/Zone" }));
        Assert.IsNull(BirthSunsetCalculator.IsAfterSunset(date, LosAngeles with { Latitude = 89, Longitude = 0, TimeZone = "UTC" }));
    }

    [TestMethod]
    public void IsAfterSunset_InternationalBirthplace_UsesItsTimeZone()
    {
        var jerusalem = LosAngeles with { Id = "281184", Kind = "city", Label = "Jerusalem", Latitude = 31.76904, Longitude = 35.21633, TimeZone = "Asia/Jerusalem" };

        Assert.IsFalse(BirthSunsetCalculator.IsAfterSunset(new(2001, 12, 17, 12, 0, 0), jerusalem));
        Assert.IsTrue(BirthSunsetCalculator.IsAfterSunset(new(2001, 12, 17, 20, 0, 0), jerusalem));
    }
}
