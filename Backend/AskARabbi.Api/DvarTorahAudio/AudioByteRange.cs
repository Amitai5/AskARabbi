using System.Net.Http.Headers;

namespace AskARabbi.Api.DvarTorahAudio;

internal sealed record AudioByteRange(long Offset, long Length, bool IsPartial)
{
    internal static bool TryCreate(string? header, long totalLength, out AudioByteRange range)
    {
        range = new AudioByteRange(0, totalLength, false);
        if (string.IsNullOrEmpty(header))
        {
            return true;
        }

        if (!RangeHeaderValue.TryParse(header, out var requested) || !string.Equals(requested.Unit, "bytes", StringComparison.OrdinalIgnoreCase) || requested.Ranges.Count != 1 || totalLength <= 0)
        {
            return false;
        }

        // A single range covers normal browser seeking without multipart response amplification.
        var part = requested.Ranges.Single();
        if (part.From is long start)
        {
            if (start >= totalLength)
            {
                return false;
            }

            var end = Math.Min(part.To ?? totalLength - 1, totalLength - 1);
            range = new AudioByteRange(start, end - start + 1, true);
            return true;
        }

        if (part.To is not long suffixLength || suffixLength == 0)
        {
            return false;
        }

        var length = Math.Min(suffixLength, totalLength);
        range = new AudioByteRange(totalLength - length, length, true);
        return true;
    }
}
