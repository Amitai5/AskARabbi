using AskARabbiLIB.Files;
using AskARabbiLIB.Models;
using AskARabbiLIB.Retrieval;
using AskARabbiLIB.Search;
using Microsoft.Extensions.Configuration;

namespace AskARabbiPrototype;

internal sealed class ApplicationState : IDisposable
{
    private int disposeState;

    internal required ManifestLocation Location { get; init; }

    internal required DocumentManifest Manifest { get; init; }

    internal required DocumentSourceCatalog SourceCatalog { get; init; }

    internal required ManifestSearchIndex SearchIndex { get; init; }

    internal required SefariaDocumentFileLoader FileLoader { get; init; }

    internal required IConfigurationRoot Configuration { get; init; }

    internal required double LoadMilliseconds { get; init; }

    internal required double SearchIndexMilliseconds { get; init; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposeState, 1) == 0 && Configuration is IDisposable disposableConfiguration)
        {
            disposableConfiguration.Dispose();
        }
    }
}
