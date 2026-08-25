using System.Text.Json;
using AskARabbiLIB.Profiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AskARabbiLIB.Tests;

[TestClass]
public sealed class UserProfileTests
{
    [TestMethod]
    [DataRow("2026-12-16", 24)]
    [DataRow("2026-12-17", 25)]
    [DataRow("2027-01-01", 25)]
    [TestCategory("Unit")]
    public void CalculateAge_DatesAroundBirthday_ReturnsCompletedYears(string currentDateText, int expectedAge)
    {
        // Arrange
        var profile = CreateProfile();
        var currentDate = DateOnly.Parse(currentDateText);

        // Act
        var age = profile.CalculateAge(currentDate);

        // Assert
        Assert.AreEqual(expectedAge, age);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Deserialize_ValidProfile_NormalizesOptionalValues()
    {
        // Arrange
        const string json = """
            {
              "name": "  Amitai Erfanian  ",
              "dateOfBirth": "2001-12-17",
              "bio": "   ",
              "religiousBackground": "  Between traditions  ",
              "jewishHeritage": "  Mizrahi (Iranian)  "
            }
            """;

        // Act
        var profile = UserProfileJsonSerializer.Deserialize(json, new DateOnly(2026, 8, 23));

        // Assert
        Assert.AreEqual("Amitai Erfanian", profile.Name);
        Assert.IsNull(profile.Bio);
        Assert.AreEqual("Between traditions", profile.ReligiousBackground);
        Assert.AreEqual("Mizrahi (Iranian)", profile.JewishHeritage);
        Assert.AreEqual(24, profile.CalculateAge(new DateOnly(2026, 8, 23)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Deserialize_UnknownProperty_ThrowsJsonException()
    {
        // Arrange
        const string json = """
            {
              "name": "Example",
              "dateOfBirth": "2001-12-17",
              "jewishHeritage": "Mizrahi",
              "misspelledField": "value"
            }
            """;

        // Act and assert
        Assert.ThrowsExactly<JsonException>(() => UserProfileJsonSerializer.Deserialize(json, new DateOnly(2026, 8, 23)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_FutureDateOfBirth_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var profile = CreateProfile() with { DateOfBirth = new DateOnly(2027, 1, 1) };

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => profile.Validate(new DateOnly(2026, 8, 23)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_MissingJewishHeritage_ThrowsArgumentException()
    {
        // Arrange
        var profile = CreateProfile() with { JewishHeritage = " " };

        // Act and assert
        var exception = Assert.ThrowsExactly<ArgumentException>(() => profile.Validate(new DateOnly(2026, 8, 23)));
        StringAssert.Contains(exception.Message, nameof(UserProfile.JewishHeritage));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Validate_AgeAboveMaximum_ThrowsInvalidOperationException()
    {
        // Arrange
        var profile = CreateProfile() with { DateOfBirth = new DateOnly(1800, 1, 1) };

        // Act and assert
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => profile.Validate(new DateOnly(2026, 8, 23)));
        StringAssert.Contains(exception.Message, "greater than 130 years");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Serialize_ValidProfile_UsesCamelCaseIsoDateWithoutCalculatedAge()
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var json = UserProfileJsonSerializer.Serialize(profile, new DateOnly(2026, 8, 23));

        // Assert
        StringAssert.Contains(json, "\"dateOfBirth\": \"2001-12-17\"");
        StringAssert.Contains(json, "\"jewishHeritage\": \"Mizrahi (Iranian)\"");
        Assert.IsFalse(json.Contains("\"age\"", StringComparison.Ordinal));
    }

    private static UserProfile CreateProfile() => new()
    {
        Name = "Amitai Erfanian",
        DateOfBirth = new DateOnly(2001, 12, 17),
        Bio = null,
        ReligiousBackground = "Somewhere between Modern Orthodox and Conservative",
        JewishHeritage = "Mizrahi (Iranian)",
    };
}
