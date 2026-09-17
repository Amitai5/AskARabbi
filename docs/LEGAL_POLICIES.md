# Legal policies

The public documents live at:

- `https://askarabbi.ai/privacy-policy`
- `https://askarabbi.ai/terms-of-service`

Their source files are `Frontend/privacy-policy.html` and `Frontend/terms-of-service.html`. Vite builds both as complete HTML entries in `Frontend/dist`; Vite preview and Cloudflare Pages resolve the extensionless URLs to these files. They reuse the application theme and `src/features/legal/legal.css`, include section navigation and print styles, and do not load React, call account APIs, or require JavaScript to read the text. The existing reading-preference bootstrap only applies appearance settings.

Deploy the entire `Frontend/dist` output, including both HTML documents. Cloudflare Pages serves these files before its SPA fallback. Keep those routes public and preserve section IDs, since notices link directly to individual sections. The host configuration requests revalidation for the documents. An existing service worker uses network-first navigation and can show the offline library when disconnected; offline UI labels legal links as available online.

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

Run `pnpm --dir Frontend verify` for frontend lint, tests, type checking, and production build. Browser checks should include both direct URLs, reload, no-JavaScript reading, section links, return navigation, legal links opening without losing a form draft, and mobile and dark appearance. Production checks should confirm the actual HTML title and content, not merely an HTTP 200 from the SPA fallback.
