using Microsoft.Net.Http.Headers;

namespace AskARabbi.Api.Tests;

// HttpClient's standard cookie container uses the wall clock. These tests must use the same fake clock as the API.
internal sealed class TestBrowserCookieHandler(TimeProvider clock) : DelegatingHandler
{
    private readonly Dictionary<(string Host, string Path, string Name), SetCookieHeaderValue> cookies = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("Test requests must have a URI.");
        lock (cookies)
        {
            var now = clock.GetUtcNow();
            var applicable = cookies.Where(entry => entry.Key.Host == uri.Host && uri.AbsolutePath.StartsWith(entry.Key.Path, StringComparison.Ordinal) &&
                (entry.Value.Expires is null || entry.Value.Expires > now) && (!entry.Value.Secure || uri.Scheme == Uri.UriSchemeHttps));
            var header = string.Join("; ", applicable.Select(entry => $"{entry.Value.Name}={entry.Value.Value}"));
            if (header.Length > 0)
            {
                request.Headers.Add("Cookie", header);
            }
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            lock (cookies)
            {
                foreach (var cookie in SetCookieHeaderValue.ParseList(values.ToList()))
                {
                    if (cookie.MaxAge is { } maxAge)
                    {
                        cookie.Expires = clock.GetUtcNow().Add(maxAge);
                    }
                    var path = cookie.Path.HasValue ? cookie.Path.ToString() : "/";
                    cookies[(uri.Host, path, cookie.Name.ToString())] = cookie;
                }
            }
        }
        return response;
    }
}
