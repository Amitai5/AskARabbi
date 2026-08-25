using System.Text.Json.Serialization;

namespace AskARabbiLIB.Models;

/// <summary>Identifies the supported reuse terms for one source edition.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceLicenseCategory>))]
public enum SourceLicenseCategory
{
    [JsonStringEnumMemberName("publicDomain")]
    PublicDomain,

    [JsonStringEnumMemberName("cc0")]
    Cc0,

    [JsonStringEnumMemberName("ccBy")]
    CcBy,

    [JsonStringEnumMemberName("ccBySa")]
    CcBySa,
}
