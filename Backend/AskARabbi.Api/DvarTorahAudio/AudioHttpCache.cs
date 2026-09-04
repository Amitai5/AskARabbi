using System.Globalization;
using System.Net.Http.Headers;

namespace AskARabbi.Api.DvarTorahAudio;

internal static class AudioHttpCache
{
    internal static void SetHeaders(HttpResponse response, string entityTag, DateTimeOffset lastModified, bool isVersioned)
    {
        response.Headers.ETag = entityTag;
        response.Headers.LastModified = lastModified.ToString("R", CultureInfo.InvariantCulture);
        response.Headers.CacheControl = isVersioned ? "private, max-age=86400, immutable" : "private, no-cache";
        response.Headers.XContentTypeOptions = "nosniff";
    }

    internal static bool IsNotModified(HttpRequest request, string entityTag, DateTimeOffset lastModified)
    {
        if (request.Headers.TryGetValue("If-None-Match", out var matches))
        {
            return matches.ToString().Split(',').Any(value => value.Trim() == "*" || TagsMatch(value.Trim(), entityTag, allowWeak: true));
        }

        return DateTimeOffset.TryParse(request.Headers.IfModifiedSince, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var modifiedSince)
            && lastModified.ToUnixTimeSeconds() <= modifiedSince.ToUnixTimeSeconds();
    }

    internal static bool AllowsRange(HttpRequest request, string entityTag, DateTimeOffset lastModified)
    {
        if (!request.Headers.TryGetValue("If-Range", out var ifRange))
        {
            return true;
        }

        if (!RangeConditionHeaderValue.TryParse(ifRange, out var condition))
        {
            return false;
        }

        return condition.EntityTag is not null
            ? TagsMatch(condition.EntityTag.ToString(), entityTag, allowWeak: false)
            : condition.Date is DateTimeOffset date && lastModified.ToUnixTimeSeconds() <= date.ToUnixTimeSeconds();
    }

    private static bool TagsMatch(string candidate, string entityTag, bool allowWeak)
    {
        return EntityTagHeaderValue.TryParse(candidate, out var parsedCandidate)
            && EntityTagHeaderValue.TryParse(entityTag, out var parsedTag)
            && (allowWeak || (!parsedCandidate.IsWeak && !parsedTag.IsWeak))
            && string.Equals(parsedCandidate.Tag, parsedTag.Tag, StringComparison.Ordinal);
    }
}
