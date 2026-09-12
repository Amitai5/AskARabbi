using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AskARabbiLIB.Tests")]
// Allows hermetic DispatchProxy tests of typed MongoDB collections without making persistence documents public.
[assembly: InternalsVisibleTo("ProxyBuilder")]
[assembly: InternalsVisibleTo("AskARabbi.CorpusPublisher")]
