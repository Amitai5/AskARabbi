# Writing the weekly D'var Torah

The reader should not need to have read the parashah. Build one understandable argument from the Torah, rather than a summary of every episode or a list of impressive sources. This editorial contract draws on the supplied *Master Guide to Writing a Dvar Torah* and the Vayigash example; neither substitutes for approved evidence.

## The recurring welcome

Every newly generated article starts with this application-owned paragraph:

> Welcome to AskARabbi's weekly D'var Torah. Let's explore this week's Torah reading and one idea to carry into our lives.

`WeeklyDvarTorahIntroduction` adds it once before review and publication. It is stored in the body, so reading, narration, and word positions agree without a new API or timing format. The model starts with the actual teaching, not another welcome.

## The essay

1. **Beginning — draw in, then orient.** After the recurring welcome, open with one vivid modern hook: a supported discovery, invention, news development, statistic, or cultural example that poses a meaningful question. If the packet has no suitable factual detail, use an explicitly imagined everyday situation, not a fact from memory. In the next paragraph, bridge to the parashah and explain who the people are, their relationships, what has happened, and the stakes before relying on the reader's knowledge. For a covenant, law, or festival passage, identify the speaker, audience, and issue. Give enough background for this argument, not a synopsis of every chapter. Introduce one genuine textual question.
2. **Middle — develop and demonstrate.** State a specific insight and build it with connected evidence. Weave each exact Torah quotation into the paragraph: introduce it, quote it, then explain why it changes our understanding. Several supported claims should develop the same thesis, not compete as separate sermons. Distinguish explicit narrative from an interpretation; do not assert imagined motives as fact. Define unfamiliar terms and attribute borrowed ideas only when evidence supports them.
3. **End — apply and return.** Bring the Torah insight back to the modern situation that opened the essay. The contemporary connection is a brief lens, not the subject. Integrate the three practical actions into prose on the same theme; at least one must identify an action the reader can take today and when to do it. Return to the opening image or question, and finish on one hopeful, memorable thought. Encourage growth without shame, guilt, or promises of guaranteed transformation. Stop there.

Aim for five to eight minutes of spoken delivery within configured length bounds. Use readable paragraphs, mostly short sentences, purposeful repetition, and clear transitions. Avoid headings that mechanically announce the essay's stages, unexplained Hebrew, a source dump, a line break after every sentence, dramatic ellipses or repeated dashes that interrupt narration, boilerplate caveats, and commentary about tools or evidence-processing machinery.

## Research and publication checks

- The research prompt plans a textual question and connected searches, including the scene and stakes. The bounded retrieval packet reserves up to two passages from a dedicated context search. All passages still pass the existing weekly-reading, licensing, and content filters.
- The contemporary lens must be constructive and nonpolitical. Political news and multi-topic newsletters/roundups are excluded before research. Selected publishers must corroborate the same specific development; an unrelated fact from a roundup is not corroboration.
- The hook must use only details present in that verified evidence, preserving dates, statistical scope, and uncertainty. Do not invent statistics, records, study findings, movie plots, or supposedly recent events. An explicitly imagined analogy does not waive the existing corroborated-news publication requirement.
- The drafting target is four connected teaching claims, supported by at least eight distinct Torah passages, plus one brief corroborated news fact. This keeps one essay from becoming eight separate mini-sermons while preserving both 80% Torah-grounding checks.
- For each of the three featured Torah sources, the draft uses its supplied `quotationSlot`, such as `{{quote:TA}}`, exactly once inside the explanatory paragraph. The application replaces it in place with the bounded exact public-domain/CC0 wording and a source marker. The source reader retains the canonical reference. Missing, repeated, unresolved, altered, or detached quotations fail deterministic validation; no quotation is appended to an unrelated paragraph as a fallback. The model cannot improvise quotations or reference URLs.
- Existing Torah/news weighting, corroboration, safety, grounding, and source-provenance requirements remain intact. Editorial changes do not relax those checks.
- Review schema `weekly_dvar_torah_review_v4` adds `openingHookGrounded` and `quotationsIntegrated`, and preserves `storyContextClear`, `argumentHasBeginningMiddleEnd`, `conclusionReturnsToOpening`, and all other existing checks (24 total). The reviewer evaluates the surrounding grammar and interpretation without changing application-owned quotation wording. A failed editorial check goes through the same single repair attempt; a second failure leaves the article unpublished.
- Review concerns contain only an enumerated check, known source IDs, and a paragraph number (zero for overall/metadata issues). The schema restricts source IDs to the current packet. Application-written repair messages explain the check; the reviewer cannot return article text, headlines, or quotations. This is an internal model-contract change; the API and Mongo document shape are unchanged. Existing articles retain their original review-version metadata.
- Azure output protections also apply to review responses. A provider-blocked completion stops immediately, without an automatic drafting retry. Safe logs retain failed check names and provider response IDs, including when a repair request fails.
- Missing source context is not permission to invent a story. Choose a supported angle, or fail review rather than publish an unsupported essay.

Edit the research, draft, and review instructions together in `Backend/AskARabbi.DvarTorahJob/Prompts`. Keep review JSON and its typed contract synchronized. The default generator version is `weekly-dvar-torah-v4`, draft schema name is `weekly_dvar_torah_draft_v2`, and review schema name is `weekly_dvar_torah_review_v4`; update an explicit generator-version environment override if one is configured. These are internal generation/review contracts, not a change to the public API or Mongo schema.

## Narration and rollout

Source markers such as `[TB]`, `[TC]`, `[TAA]`, `[NA]`, and numeric citations remain on screen but are replaced by equal-length spaces for synthesis. Application-rendered quotation-reference labels and marker-only source appendices are also silent. The quotation itself, including Hebrew, is still read; ordinary bracketed prose is preserved.

Narration keeps the approved male Andrew multilingual voice. The updated narration format prefers paragraph/sentence boundaries when splitting requests, collapses silent citation gaps before synthesis while retaining exact display-position mapping, and controls chunk-edge silence. These changes address avoidable pauses without removing normal spoken punctuation. See the [audio guide](DVAR_TORAH_AUDIO.md).

The frontend keeps one player in a bottom dock outside the article's scroll area. It follows spoken highlights only when they approach the visible area's edge. Manual scrolling pauses following, and **Follow text** resumes it. Opening a source reader temporarily suspends automatic scrolling. Reduced-motion preferences disable animated scrolling.

Current and opened archive articles show an estimated reading time beneath the title, calculated from the saved audio duration at 1× speed and rounded up to a whole minute. Playback-speed changes do not change this estimate. No audio download or timings request is needed; an article without a valid recording duration has no fabricated estimate.

Deploy the generator and frontend to apply these changes. Existing published text is immutable and is not automatically rewritten or given a new introduction. Existing recordings remain playable; they do not become silent merely by deploying frontend code. After the new generator image is deployed, use the existing audio-only backfill for each desired published week. The narration-format version change creates a new MP3/timing pair without modifying its article. Re-running text generation for an already-published week still returns `AlreadyPublished`.

Tests use deterministic model, source, and speech doubles plus browser fixtures. They verify the contract and interactions, not the literary quality of an ungenerated production essay; review the next real publication after rollout.
