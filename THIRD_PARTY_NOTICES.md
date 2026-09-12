# Third-party notices

AskRabbi includes the following third-party software in compiled .NET artifacts. Each component remains subject to its own license; this notice does not replace or modify those terms.

## Zmanim 1.5.0

- Package: [Zmanim 1.5.0](https://www.nuget.org/packages/Zmanim/1.5.0)
- Upstream source: [Yitzchok/Zmanim](https://github.com/Yitzchok/Zmanim), package commit `c02f94a6f13efa54dd33b8932e56a34adefb513f`
- Package copyright: Copyright © Eliyahu Hershfeld 2013
- License declared by the package metadata: [GNU Lesser General Public License](https://www.gnu.org/copyleft/lesser.html)

AskRabbi dynamically references the unmodified NuGet assembly. `HebrewCalendarService` combines .NET's built-in `HebrewCalendar` calculations with the package's weekly parashah data. AskRabbi's wrapper and regression tests are part of this repository; no modified Zmanim binary is distributed from source control.

## Optional BDB dictionary data

- Source: [BibleAquifer/BDBHebrewLexicon](https://github.com/BibleAquifer/BDBHebrewLexicon/tree/4fa2054acd751af9b1bc16c5102a542b1261f5d1), pinned revision `4fa2054acd751af9b1bc16c5102a542b1261f5d1`.
- Work: Brown–Driver–Briggs Hebrew and Aramaic lexicon; Aquifer English article data, version `1.0.4` (resource metadata `1.1.2`).
- License declared by this digital edition: [CC0 1.0](https://creativecommons.org/public-domain/cc0/), as recorded in its pinned README and `eng/metadata.json`. This does not establish permission to copy other BDB editions or unrelated dictionaries.
- Data is downloaded only by the explicit importer, not bundled in source control or fetched during a conversation. Original article IDs, edition, source URL, file checksum, and license are preserved in MongoDB and citations. HTML is deterministically converted to plain text; no generated definitions replace the original articles.

See [BDB import and provenance](Tools/AskARabbi.DictionaryImporter/README.md) for verification and rollout details.
