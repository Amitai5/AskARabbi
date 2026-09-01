# Third-party notices

AskRabbi includes the following third-party software in compiled .NET artifacts. Each component remains subject to its own license; this notice does not replace or modify those terms.

## Zmanim 1.5.0

- Package: [Zmanim 1.5.0](https://www.nuget.org/packages/Zmanim/1.5.0)
- Upstream source: [Yitzchok/Zmanim](https://github.com/Yitzchok/Zmanim), package commit `c02f94a6f13efa54dd33b8932e56a34adefb513f`
- Package copyright: Copyright © Eliyahu Hershfeld 2013
- License declared by the package metadata: [GNU Lesser General Public License](https://www.gnu.org/copyleft/lesser.html)

AskRabbi dynamically references the unmodified NuGet assembly. `HebrewCalendarService` combines .NET's built-in `HebrewCalendar` calculations with the package's weekly parashah data. AskRabbi's wrapper and regression tests are part of this repository; no modified Zmanim binary is distributed from source control.
