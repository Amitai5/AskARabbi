using AskARabbiLIB.Calendar;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class HebrewCalendarServiceTests
{
    private readonly HebrewCalendarService service = new();

    [TestMethod]
    [TestCategory("Unit")]
    public void ConvertToHebrew_KnownBirthDate_ReturnsNumericAndFormattedDate()
    {
        // Act
        var result = service.ConvertToHebrew(new DateTime(2001, 12, 17, 10, 30, 0, DateTimeKind.Unspecified));

        // Assert
        Assert.AreEqual(new DateOnly(2001, 12, 17), result.GregorianDate);
        Assert.AreEqual(5762, result.HebrewYear);
        Assert.AreEqual(4, result.HebrewMonth);
        Assert.AreEqual(2, result.HebrewDay);
        StringAssert.Contains(result.EnglishText, "5762");
        StringAssert.Contains(result.HebrewText, "טבת");
        Assert.IsFalse(result.WasAdvancedAfterSunset);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ConvertToHebrew_AfterSunset_AdvancesOneHebrewDay()
    {
        // Act
        var result = service.ConvertToHebrew(new DateTime(2001, 12, 17, 20, 0, 0, DateTimeKind.Unspecified), true);

        // Assert
        Assert.AreEqual(3, result.HebrewDay);
        Assert.IsTrue(result.WasAdvancedAfterSunset);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public void FindParashahForWeek_CommonThursdayYear_UsesCorrectKislevClassification()
    {
        // Act
        var result = service.FindParashahForWeek(new DateTime(2014, 12, 24), false);

        // Assert
        Assert.AreEqual(new DateOnly(2014, 12, 27), result.ShabbatDate);
        Assert.AreEqual("Vayigash", result.Parashah);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FindParashahForWeek_IsraelAndDiasporaDivergence_ReturnsExpectedReadings()
    {
        // Act
        var diaspora = service.FindParashahForWeek(new DateTime(2022, 5, 28), false);
        var israel = service.FindParashahForWeek(new DateTime(2022, 5, 28), true);

        // Assert
        Assert.AreEqual("Bechukosai", diaspora.Parashah);
        Assert.AreEqual("Bamidbar", israel.Parashah);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FindHebrewAnniversaryParashah_ThirteenthBirthday_ReturnsBarMitzvahWeek()
    {
        // Act
        var result = service.FindHebrewAnniversaryParashah(new DateTime(2001, 12, 17), 13, false);

        // Assert
        Assert.AreEqual(new DateOnly(2014, 12, 27), result.ShabbatDate);
        Assert.AreEqual("Vayigash", result.Parashah);
        StringAssert.Contains(result.CalculationNote, "13th Hebrew birthday");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FindHebrewAnniversaryParashah_InvalidAge_ThrowsArgumentOutOfRangeException()
    {
        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.FindHebrewAnniversaryParashah(new DateTime(2001, 12, 17), 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.FindHebrewAnniversaryParashah(new DateTime(2001, 12, 17), 131));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FindParashahForWeek_MultiCenturyCalendarPatterns_SupportsEveryValidYearType()
    {
        // Act
        var results = Enumerable.Range(1900, 300)
            .SelectMany(year => new[] { service.FindParashahForWeek(new DateTime(year, 6, 1), false), service.FindParashahForWeek(new DateTime(year, 6, 1), true) })
            .ToArray();

        // Assert
        Assert.HasCount(600, results);
        Assert.IsTrue(results.Any(result => result.Parashah is not null));
        Assert.IsTrue(results.Any(result => result.InIsrael));
        Assert.IsTrue(results.Any(result => !result.InIsrael));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FindParashahForWeek_FestivalShabbat_ReturnsFestivalInsteadOfRegularPortion()
    {
        // Act
        var result = service.FindParashahForWeek(new DateTime(2026, 9, 12), false);

        // Assert
        Assert.IsNull(result.Parashah);
        Assert.IsNotNull(result.Holiday);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void FindHebrewAnniversaryParashah_AdarTransitions_MapsMonthsDeterministically()
    {
        // Arrange
        var calendar = new HebrewCalendar();
        var commonAdar = calendar.ToDateTime(5783, 6, 14, 12, 0, 0, 0);
        var commonNisan = calendar.ToDateTime(5783, 7, 14, 12, 0, 0, 0);
        var leapAdarOne = calendar.ToDateTime(5784, 6, 14, 12, 0, 0, 0);
        var leapAdarTwo = calendar.ToDateTime(5784, 7, 14, 12, 0, 0, 0);
        var leapNisan = calendar.ToDateTime(5784, 8, 14, 12, 0, 0, 0);

        // Act
        var mapped = new[]
        {
            service.FindHebrewAnniversaryParashah(commonAdar, 1),
            service.FindHebrewAnniversaryParashah(commonNisan, 1),
            service.FindHebrewAnniversaryParashah(leapAdarOne, 1),
            service.FindHebrewAnniversaryParashah(leapAdarTwo, 1),
            service.FindHebrewAnniversaryParashah(leapNisan, 1),
            service.FindHebrewAnniversaryParashah(leapNisan, 19),
        };

        // Assert
        Assert.HasCount(6, mapped);
        Assert.IsTrue(mapped.All(value => value.ShabbatDate.DayOfWeek == DayOfWeek.Saturday));
        StringAssert.Contains(mapped[0].CalculationNote, "Adar II");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CalendarOperations_UnsupportedDates_ThrowArgumentOutOfRangeException()
    {
        // Arrange
        var unsupported = new DateTime(1500, 1, 1);

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.ConvertToHebrew(unsupported));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => service.FindParashahForWeek(unsupported));
    }
}
