# Legal policies

The public documents live at:

- `https://askarabbi.ai/privacy`
- `https://askarabbi.ai/terms`

Their source files are `WebsiteFrontend/privacy.html` and `WebsiteFrontend/terms.html`. Vite builds both as complete HTML entries in `WebsiteFrontend/dist`; Vite preview and Cloudflare Pages resolve `/privacy` and `/terms` to these files. They reuse the public website theme and `src/legal.css`, include section navigation and print styles, and do not load React, call account APIs, or require JavaScript. The application's reading-preference bootstrap is not used by the website.

Deploy the entire `WebsiteFrontend/dist` output at `askarabbi.ai`, including both HTML documents, `_headers`, and `_redirects`. Deploy the updated `Frontend/dist` at `app.askarabbi.ai`; it contains policy redirects but no legal documents or policy-only styles. Publish the website first so the app's new links resolve immediately. Cloudflare Pages redirects old website and app policy URLs, including `.html` and trailing-slash variants, to the canonical website pages. Preserve section IDs so existing fragment links still work. Policy responses require revalidation; app notices label these public documents as available online.

Preserve the `email_off` HTML comments around contact links. They use Cloudflare's [documented email-obfuscation exception](https://developers.cloudflare.com/waf/tools/scrape-shield/email-address-obfuscation/#prevent-cloudflare-from-obfuscating-email) so the privacy and legal contact remains readable and clickable without JavaScript. Confirm this against the deployed site because local preview does not perform Cloudflare's HTML rewriting.

The shared `LegalLink` opens a separate tab, announces that behavior to assistive technology, and keeps unfinished sign-in, personalization, and chat input in place. Links appear at sign-in/account creation, password reset, onboarding, personalization, data controls, the composer, weekly teachings, calendar, offline learning, and print setup. Printed copies include absolute policy URLs that remain useful in a PDF or on paper. Settings search can find both policies.

## Source and operational facts

The content was checked against the implementation and public production page on September 11, 2026:

- Authentication: backend WorkOS integration and server-managed cookies.
- Required profile fields and optional nondisclosure choices: `personalizationValidation.ts` and `personalizationOptions.ts`.
- AI context: `ConversationPersonalization.CreateUserContext` and the grounded-answer pipeline; relevant prior turns, first name, age group, religion, heritage, and additional context may be processed.
- Calendar disclosure: `HebcalCalendarClient` and location resolution.
- Deletion: `MongoUserDataStore`, the account-deletion workflow, and existing data-control messages. Conversation deletion, account deletion, provider retention, backup expiry, and local copies are different operations.
- Local storage: reading preferences, `offlineLibrary.ts`, and the service-worker allowlist.
- Hosting: the successful production Cloudflare Pages check. The public HTML includes Cloudflare's Web Analytics beacon, injected by hosting rather than application source; the policy discloses it.
- Contact: the operator supplied `support@askarabbi.ai` for privacy and legal requests.

The operator's legal name and location were not supplied. The published documents identify the service as AskRabbi and use the confirmed support address; they do not invent a legal entity, address, or governing jurisdiction. Add the responsible person's or company's legal identity and any required address or representative information when confirmed. The user authorized publication with the available contact information.

Provider and regulatory references consulted:

- [Microsoft AI data processing](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy)
- [Cloudflare Web Analytics](https://developers.cloudflare.com/web-analytics/)
- [California privacy rights](https://oag.ca.gov/privacy/ccpa)
- [FTC COPPA guidance](https://www.ftc.gov/business-guidance/resources/complying-coppa-frequently-asked-questions)

The conversational [answer-reliability contract](ANSWER_RELIABILITY.md) permits reviewed introductory background without citations and saves fixed recovery replies when validation or required evidence fails. Those replies remain ordinary account history and can be processed as later context. This changes neither provider retention nor account-deletion scope, and does not create a guarantee that every answer is source-backed or human-reviewed.

## Maintaining the documents

Keep the effective date and version aligned across both documents when making a joint update. Review text against actual behavior when changing providers, analytics, stored data, model training, retention, account eligibility, or paid features. The initial Terms use a 13-year minimum (or a higher local minimum), guardian permission for minors, no mandatory arbitration, and liability limitations subject to mandatory legal protections.

The documents do not create a consent database, age-verification system, automated privacy-request workflow, retention schedule, or new WorkOS-hosted UI settings. The app presents legal links before its authentication handoff; no server-side record of agreement is introduced. Privacy requests beyond existing self-service controls go to the support contact. Legal applicability, enforceability, any required sensitive-data consent mechanism, and provider contractual safeguards require operational and legal review; these pages alone do not establish compliance.

Run `pnpm --dir WebsiteFrontend verify` for website lint, type checking, static builds, and the dependency-free Node tests for complete documents, section links, local assets, redirect rules, and policy caching. Run `pnpm --dir Frontend verify` for application links, print URLs, compatibility redirects, and the app build. Browser checks should include direct URLs, reload, no-JavaScript reading, section and cross-document links, return navigation, and mobile layout. The website uses its own light theme independently of app reading settings. Check redirect status and fragment preservation on Cloudflare Pages; Vite preview does not interpret `_redirects` or `_headers`. Confirm actual document titles and content, not merely HTTP 200 from a fallback.
