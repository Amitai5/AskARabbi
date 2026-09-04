# AskRabbi

[![Project status](https://img.shields.io/badge/status-early%20development-D97706?style=for-the-badge)](#project-status)
[![Website](https://img.shields.io/badge/askarabbi.ai-planned-2563EB?style=for-the-badge&logo=googlechrome&logoColor=white)](https://askarabbi.ai)
[![React](https://img.shields.io/badge/React-implemented-20232A?style=for-the-badge&logo=react&logoColor=61DAFB)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-implemented-3178C6?style=for-the-badge&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![.NET](https://img.shields.io/badge/.NET-prototype-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)

> Source-grounded Jewish learning, with context and citations—not judgment.

AskRabbi is a planned AI-assisted learning experience for people who want to explore Judaism through its texts. It will help users ask everyday, difficult, personal, or highly specific questions and receive an accessible explanation grounded in sources from Tanakh, Talmud, rabbinic literature, and other parts of the Jewish textual tradition.

The goal is not to produce a one-word ruling. The goal is to show the conversation: which texts are relevant, how later authorities interpreted them, where views differ, and how a conclusion developed over time. Every response should give the user enough context to continue learning and enough agency to decide what the material means for their own Jewish life.

AskRabbi is intended for [askarabbi.ai](https://askarabbi.ai).

## Why AskRabbi?

Jewish tradition contains thousands of years of law, commentary, debate, stories, philosophy, and lived interpretation. That richness is beautiful, but it can also make a seemingly simple question difficult to research.

Consider a modern question such as:

> A server I own continues running on Shabbat, but I do not operate it during Shabbat. How do Jewish sources approach that situation?

Finding a useful answer may require more than locating a single verse. A reader may need to understand relevant categories of work, the treatment of automated processes, later responsa, differences between communities, and how older principles are applied to modern technology.

AskRabbi is being designed to make that path easier to follow. It should:

- Identify the concepts and sources that make the question meaningful.
- Present the original text alongside available translations.
- Explain the chain of interpretation instead of jumping to a conclusion.
- Distinguish the source text, later commentary, custom, and modern application.
- Name disagreements and identify the traditions or authorities behind them.
- Cite exact passages so users can inspect the evidence themselves.
- Be honest when the sources do not support a confident answer.

## The guiding principle: explain, never judge

AskRabbi must never shame a user, grade their Jewishness, or tell them that they are “doing Judaism wrong.”

If someone asks why many Jewish communities do not eat chicken with dairy, for example, AskRabbi should explain the Torah's prohibition, the later rabbinic discussion, the reasoning used to extend the practice, and meaningful differences in interpretation or observance. It should not turn that explanation into a judgment about what the user must choose.

That distinction is central to this project:

| AskRabbi should | AskRabbi should not |
| --- | --- |
| Explain what a source says and how it has been interpreted | Present itself as God, a rabbi, or a final religious authority |
| Show how a rabbinic conclusion was reached | Issue a personalized *psak* or definitive halakhic ruling |
| Represent disagreement and multiple traditions accurately | Flatten Jewish thought into one universal answer |
| Give citations and make uncertainty visible | Invent a source or hide uncertainty behind confident language |
| Respect the user's autonomy and level of observance | Pressure, shame, or measure a person's Jewish identity |

AskRabbi can support learning and preparation for a conversation with a trusted rabbi, teacher, or community leader. It is not a substitute for them, especially when a question is personal, urgent, or consequential.

## Planned experience

### Source-grounded conversations

Users will be able to have a natural conversation with an AI that retrieves relevant Jewish texts before answering. Responses will be grounded in the retrieved material, not presented as unsupported model knowledge.

### Transparent citations

Answers link claims to precise textual references. Saved assistant messages retain exact validated quotations, bounded source context, work, canonical reference, language, edition or translation, license, a direct Sefaria passage link, and separate edition attribution when available.

### Hebrew and translation together

The system will preserve source-language text—including Hebrew and Aramaic where applicable—and pair it with available translations. It should never blur the original wording and a translator's interpretive choices into a single unattributed quotation.

### Selectable source collections

Each new conversation enables the core Torah, Tanakh, Mishnah, and Talmud collections by default. Users can add approved supplemental works, select every approved source, or narrow the set for that conversation; sending is disabled when no source remains selected.

### Saved and private chats

AskRabbi will support two conversation modes:

- **Saved chats** will appear in the user's account and can be continued later.
- **Private chats** will not be written to AskRabbi's persistent chat history.

Private requests still have to be processed in memory to generate a response. The production privacy notice will clearly disclose any transient processing by infrastructure or model providers; the product must not promise stronger privacy than its deployed systems can verify.

### Accounts and responsible usage limits

Accounts will provide access to conversation history, source preferences, and usage information. Configurable limits will protect service reliability and keep access equitable without embedding a particular pricing model into the application design.

### A weekly Dvar Torah

The signed-in application includes a weekly-learning destination separate from conversation history. It loads only when opened and asks the API for the upcoming Shabbat's publication; if the current teaching is not ready, the API may return the most recent earlier publication without mislabeling it as current. A separate scheduled Azure Container Apps Job owns generation and publication so model latency or retries never block the conversational API.

After text publication, a separate narration coordinator can produce an English/Hebrew Azure Speech recording, store it privately in Hot Blob Storage, and attach versioned audio metadata to the same Mongo document. The authenticated API streams MP3 byte ranges; the browser supports playback, seeking, speed controls, and synchronized word highlighting without downloading a speech model. See [private narration and deployment](docs/DVAR_TORAH_AUDIO.md).

## Texts and data

The primary planned source is [Sefaria](https://www.sefaria.org/), a nonprofit organization assembling a free, interconnected digital library of Jewish texts in Hebrew and translation. Its library includes Tanakh, Mishnah, Talmud, Midrash, Halakhah, responsa, Jewish thought, and other collections.

AskRabbi expects to use Sefaria's structured data and reference system to preserve relationships between passages and generate human-readable citations. See the [Sefaria Library](https://www.sefaria.org/texts), [Developer Portal](https://developers.sefaria.org/), and [API documentation](https://developers.sefaria.org/reference/getting-started).

Each text version or translation may have its own license and attribution requirements. AskRabbi will preserve that metadata and include only material whose terms permit the intended use.

Additional sources may be evaluated in the future, including Jewish Q&A collections from sites such as [Chabad.org](https://www.chabad.org/). No external collection should be ingested merely because it is publicly readable; permission, licensing, attribution, provenance, and editorial quality must be established first.

AskRabbi is an independent project and is not currently affiliated with or endorsed by Sefaria, Chabad.org, or any religious institution.

## How an answer should be built

1. **Understand the question** and ask for context when different facts would materially change the sources involved.
2. **Search the enabled collections** in the user's source settings.
3. **Run a trusted local calculation when needed** for Hebrew dates, today's Hebrew/Gregorian date, Hebrew birthday anniversaries, or the weekly parashah; never ask the model to calculate these from memory.
4. **Retrieve the strongest passages** in their original language and available translations.
5. **Map the discussion** across primary text, commentary, later rulings, customs, and modern applications.
6. **Lead with the bottom line** in one or two direct sentences, then give a concise explanation that distinguishes material consensus, disagreement, and uncertainty.
7. **Quote every source or calculated result in context** and, when describing an interpretive chain, show both the later view and the earlier passage it relies on.
8. **Validate every citation and quotation** against the retrieved text or exact tool result, then independently audit whether each claim actually follows from the evidence it cites.
9. **Repair once or fail visibly** using the same evidence packet; never fall back to unsupported model knowledge.
10. **Leave the decision with the user** and end with a visible reminder that the explanation is one interpretation, not infallible truth or binding *psak*.

## Product commitments

AskRabbi is being built around the following commitments:

- **Sources before certainty.** A limited, well-supported answer is better than a confident invention.
- **Bottom line, then reasoning.** Answer the question directly, then show enough evidence for users to follow and challenge the reasoning.
- **Context before quotation.** A passage should not be detached from its genre, period, or interpretive history.
- **Pluralism without false equivalence.** Meaningful disagreements should be represented accurately, including their relative scope and authority.
- **Autonomy without indifference.** The system can explain consequences and traditions clearly while leaving religious choices to the user.
- **Privacy by design.** Saved and private conversations must have genuinely different retention behavior.
- **Visible provenance.** Users should know where text came from, which edition they are reading, and how to inspect it.

## Planned technology

| Layer | Planned technology | Responsibility |
| --- | --- | --- |
| Web application | React, TypeScript, and Vite | Accounts, chat, source viewer, settings, and usage experience |
| Application API | ASP.NET Core and C# | Users, conversations, authorization, quotas, and orchestration |
| Identity | WorkOS AuthKit integration implemented | Purpose-specific hosted login/sign-up, password recovery, rotating provider sessions, verified identity projection, and backend-owned application cookies |
| Prototype retrieval | SQLite FTS5 through `AskARabbiLIB` | Exact references, tiered full-concept/pair/fallback BM25 search, deterministic vocabulary expansion, Unicode normalization, provenance filters, and bounded evidence |
| Production retrieval | Azure OpenAI managed vector store | Forced Responses `file_search`, stable source filters enforced locally, manifest-backed provenance, and bounded evidence behind `ISourceRetriever` |
| Application persistence | Azure Cosmos DB for MongoDB integrated | Owner-scoped accounts, saved conversation metadata/messages, personalization/preferences, monthly usage counters, and global weekly Dvar Torah publications |
| Weekly publisher | .NET 10 Azure Container Apps Job and Docker image scaffolded | Sunday UTC cron, Hebrew-week selection, idempotent generation leasing, and atomic publication; content generator intentionally pending approval |
| AI provider | Azure OpenAI Responses API through `IAIEngine` | Strict structured output from an approved evidence packet plus explicitly registered, bounded local calendar functions; production uses the Container App's Entra identity and the prototype supports a local API key |
| Text provider | Sefaria initially | Jewish texts, translations, relationships, and canonical references |

The public topology is fixed: the frontend runs at `https://askarabbi.ai` and the API runs in Azure Container Apps behind `https://api.askarabbi.ai`, with WorkOS AuthKit for identity and Azure Cosmos DB for MongoDB for application persistence. The backend is packaged in ACR and its production-only GitHub workflow deploys verified commits by immutable digest. The `askarabbi-gpt-5-mini` deployment and full managed Sefaria corpus exist, and the production API composes managed Responses file-search retrieval with the same fail-closed grounded-answer pipeline used by the prototype. Deploying this integration and completing authenticated live validation remain before public chat can be enabled. See the [production deployment plan](docs/PRODUCTION_DEPLOYMENT.md), [managed corpus operations](docs/MANAGED_VECTOR_STORE.md), and [production readiness checklist](docs/PRODUCTION_READINESS.md).

For the implemented question-to-answer path, read the [chat workflow](docs/CHAT_WORKFLOW.md). For the proposed architecture, privacy contract, API shape, retrieval pipeline, data model, testing strategy, and phased delivery plan, read the [technical design](docs/TECHNICAL.md).

## Project status

AskRabbi is in **early development**. The Azure API, registry, Cosmos DB foundation, model deployment, full managed corpus, and grounded production code path exist, but the public product is not launch-ready: the latest frontend/backend must be deployed, WorkOS must be configured and validated, and the remaining security and operational controls must be completed.

The [`Frontend`](Frontend) application is a responsive React, TypeScript, Tailwind CSS, and Vite experience connected to the .NET API for backend-owned WorkOS email/Google/sign-up flows, rotating session hydration, saved conversations, a lazy weekly Dvar Torah view, source filters, Cosmos-backed personalization/preferences, exact-period usage, password recovery, logout, and grounded chat turns. [`Backend`](Backend) provides the tested .NET 10 ASP.NET Core boundary with S256 PKCE, restrictive credentialed CORS, owner-scoped Azure Cosmos DB for MongoDB stores, managed Azure OpenAI retrieval/generation, `GET /health`, and a separate scheduled Container Apps Job image for weekly publication. The message endpoint stores the user question, retrieves approved evidence, validates the structured answer and its exact quotations, persists only a validated assistant answer, and increments usage only after success. The weekly generator uses no-subscription public feeds, enforces at least 80% Torah grounding, stores searchable tags and bounded source provenance, and fails closed through independent neutrality, violence, hate, racism, sexism, protected-group, and inclusion review; its production execution gate remains disabled pending credentials and a non-production smoke test. An explicit Development-only local profile exercises the same HTTP controllers and cookie flow without credentials. The [authentication design](docs/AUTHENTICATION.md) records the flow and remaining launch hardening.

The reusable `AskARabbiLIB` project and its tests live under `Library`, while the separate `AskARabbiPrototype` solution is a thin Spectre.Console host. AI Chat is the default experience: it is continuous, profile-aware, locally retrieved, and fail-closed behind exact citation/quotation checks plus an independent claim-support audit. Source Search remains a separate local tool for manifest search and source inspection. Interactive chat accepts strict local JSON profiles or process-only custom context. The normal model prompt receives calculated age rather than exact birth data; when a calendar question requires the saved birth date, trusted server code uses it privately and exposes only the calculated result as validated evidence. All model-facing instructions and response schemas are reviewable under [`Prototype/Prompts`](Prototype/Prompts). The local segment index is reproducible and untracked; AI configuration is unnecessary unless AI Chat or the one-shot `ask` command is used. See the [library guide](Library/README.md), [prototype guide](Prototype/README.md), [profile guide](Prototype/Profiles/README.md), [chat workflow](docs/CHAT_WORKFLOW.md), and [technical design](docs/TECHNICAL.md).

The broad delivery path is:

1. Establish the application foundation, account model, and security boundaries.
2. Build a bilingual Sefaria ingestion and retrieval proof of concept.
3. Generate citation-backed answers and measure source faithfulness.
4. Add saved/private chat modes, source settings, and usage controls.
5. Conduct scholarly review, privacy review, accessibility testing, and adversarial evaluation.
6. Prepare the initial release for [askarabbi.ai](https://askarabbi.ai).

## Continuous integration

The separate `Verify` workflow runs for pushes to every branch and for every pull request. It installs the locked frontend dependencies, lints, tests, and builds the Vite application; it also restores and builds all three .NET solutions, runs the library and backend MSTest suites, enforces at least 80% library branch coverage from the Cobertura report, and retains the .NET test results for troubleshooting.

The `Deploy Backend` workflow runs only after `Verify` succeeds for a push to `production`. It authenticates to Azure through GitHub OIDC, builds the API container, pushes a commit-SHA image to ACR, deploys that image to Azure Container Apps by immutable digest, and verifies the revision and public health endpoint. It never deploys pull requests, failed builds, staging branches, or any branch other than `production`. The frontend retains its separate production deployment.

## Contributing

The detailed contribution workflow is still evolving. In the meantime, run the affected project's documented verification command and use issues or design discussions for:

- Source coverage and citation correctness.
- Jewish textual nuance and representation of disagreement.
- Retrieval and evaluation approaches.
- Privacy, security, accessibility, and internationalization.
- Product language that is respectful across levels of knowledge and observance.

Before proposing a large implementation, open an issue so the architecture and scope can be discussed.

## Educational and religious-use notice

AskRabbi is an educational tool under development. AI-generated text can be incomplete or wrong even when citations are present. Users should inspect the cited sources and consult a qualified human for personalized religious decisions or other high-consequence questions.

## License

A license for the AskRabbi source code has not yet been selected. Until a license file is added, the repository should not be assumed to grant permission to copy, modify, or redistribute its code or documentation.

Source texts and translations remain subject to their respective licenses and attribution requirements.

## Acknowledgments

AskRabbi is inspired by Judaism's long tradition of asking questions, preserving disagreement, and returning to the text. The project is grateful to [Sefaria](https://www.sefaria.org/about) and its contributors for expanding access to Jewish texts and building reusable infrastructure for Jewish learning.
