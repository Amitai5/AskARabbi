# Writing the weekly D'var Torah

The reader should not need to have read the parashah. Build one understandable argument from the Torah, rather than a summary of every episode or a list of impressive sources. This editorial contract draws on the supplied *Master Guide to Writing a Dvar Torah* and the Vayigash example; neither substitutes for approved evidence.

## The recurring welcome

Every newly generated article starts with this application-owned paragraph:

> Welcome to AskARabbi's weekly D'var Torah. Let's explore this week's Torah reading and one idea to carry into our lives.

`WeeklyDvarTorahIntroduction` adds it once before review and publication. It is stored in the body, so reading, narration, and word positions agree without a new API or timing format. The model starts with the actual teaching, not another welcome.

## The essay

1. **Beginning — orient and ask.** Open with a striking detail or scene. Explain who the people are, their relationships, what has happened, and the stakes before relying on the reader's knowledge. For a covenant, law, or festival passage, identify the speaker, audience, and issue. Give enough background for this argument, not a synopsis of every chapter. Introduce one genuine textual question.
2. **Middle — develop and demonstrate.** State a specific insight and build it with connected evidence. Explain each quotation and why it changes our understanding. Several supported claims should develop the same thesis, not compete as separate sermons. Distinguish explicit narrative from an interpretation; do not assert imagined motives as fact. Define unfamiliar terms and attribute borrowed ideas only when evidence supports them.
3. **End — apply and return.** Earn a concrete human application from the argument. The existing neutral, corroborated current-event connection remains a brief lens, not the subject. Integrate the three practical actions into prose, return to the opening image or question, and finish on one memorable thought. Stop there.

Aim for five to eight minutes of spoken delivery within configured length bounds. Use readable paragraphs, mostly short sentences, purposeful repetition, and clear transitions. Avoid headings that mechanically announce the essay's stages, unexplained Hebrew, a source dump, a line break after every sentence, boilerplate caveats, and commentary about tools or evidence-processing machinery.

## Research and publication checks

- The research prompt plans a textual question and connected searches, including the scene and stakes. The bounded retrieval packet reserves up to two passages from a dedicated context search. All passages still pass the existing weekly-reading, licensing, and content filters.
- The contemporary lens must be constructive and nonpolitical. Political news and multi-topic newsletters/roundups are excluded before research. Selected publishers must corroborate the same specific development; an unrelated fact from a roundup is not corroboration.
- The drafting target is four connected teaching claims, supported by at least eight distinct Torah passages, plus one brief corroborated news fact. This keeps one essay from becoming eight separate mini-sermons while preserving both 80% Torah-grounding checks.
- Exact quotations are inserted by the application from approved public-domain/CC0 evidence. The model cannot improvise quotations or reference URLs.
- Existing Torah/news weighting, corroboration, safety, grounding, and source-provenance requirements remain intact. Editorial changes do not relax those checks.
- Review schema `weekly_dvar_torah_review_v3` preserves `storyContextClear`, `argumentHasBeginningMiddleEnd`, and `conclusionReturnsToOpening`, alongside all existing checks. A failed editorial check goes through the same single repair attempt; a second failure leaves the article unpublished.
- Review concerns contain only an enumerated check, known source IDs, and a paragraph number (zero for overall/metadata issues). The schema restricts source IDs to the current packet. Application-written repair messages explain the check; the reviewer cannot return article text, headlines, or quotations. This is an internal model-contract change; the API and Mongo document shape are unchanged. Existing articles retain their original review-version metadata.
- Azure output protections also apply to review responses. A provider-blocked completion stops immediately, without an automatic drafting retry. Safe logs retain failed check names and provider response IDs, including when a repair request fails.
- Missing source context is not permission to invent a story. Choose a supported angle, or fail review rather than publish an unsupported essay.

Edit the research, draft, and review instructions together in `Backend/AskARabbi.DvarTorahJob/Prompts`. Keep review JSON and its typed contract synchronized. The default generator version is `weekly-dvar-torah-v3`; update an explicit environment override if one is configured.

## Narration and rollout

Source markers such as `[TB]`, `[TC]`, `[TAA]`, `[NA]`, and numeric citations remain on screen but are replaced by equal-length spaces for synthesis. Application-rendered quotation-reference labels and marker-only source appendices are also silent. The quotation itself, including Hebrew, is still read; ordinary bracketed prose is preserved.

The frontend keeps one player in a bottom dock outside the article's scroll area. It follows spoken highlights only when they approach the visible area's edge. Manual scrolling pauses following, and **Follow text** resumes it. Opening a source reader temporarily suspends automatic scrolling. Reduced-motion preferences disable animated scrolling.

Deploy the generator and frontend to apply these changes. Existing published text is immutable and is not automatically rewritten or given a new introduction. Existing recordings remain playable; they do not become silent merely by deploying frontend code. After the new generator image is deployed, use the existing audio-only backfill for each desired published week. The narration-format version change creates a new MP3/timing pair without modifying its article. Re-running text generation for an already-published week still returns `AlreadyPublished`.

Tests use deterministic model, source, and speech doubles plus browser fixtures. They verify the contract and interactions, not the literary quality of an ungenerated production essay; review the next real publication after rollout.
