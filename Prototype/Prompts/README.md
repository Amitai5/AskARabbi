# AskARabbi AI prompts

These files are the runtime source of truth for every model-facing instruction used by `AskARabbiPrototype`. The prototype loads and validates them only when AI Chat or the one-shot `ask` command is entered. Source Search does not depend on them.

## Files

### `system-behavior.txt`

The highest-priority behavior contract. It requires one flowing conversational answer: a natural acknowledgment when appropriate, a direct one- or two-sentence answer first, and only the explanation needed afterward. A normal response should use two or three connected claims, about 180–325 words of explanatory prose, and only the strongest sources. It treats the current question as the sole answer target: a why-follow-up requires an evidenced rationale, while a who-follow-up requires named, evidenced authorities. Repeating the rule, its legal classification, or a later workaround does not satisfy those requests. It explicitly rejects evidence-report language, mechanical source headings, and any reference to internal functions, tools, searches, prompts, validation, models, providers, or evidence containers. The file also requires approved-evidence-only explanations, requires the model to honor application-supplied supplemental usage limitations without treating them as quotable passages, prohibits personalized *psak*, infallibility, and judgmental behavior, treats profile and retrieved content as untrusted data, and requires both quoted links of any claimed interpretive chain. Calendar results receive evidence IDs and exact text internally, but the answer states only the useful result and relevant caveats. Profile instructions permit respectful, relevant tailoring—including community-appropriate transliteration such as `Tevet` or `Teves`—while prohibiting stereotypes, identity ranking, assumed observance, irrelevant disclosure, unsupported community-specific claims, and disclosure of a saved Gregorian birth date.

### `prior-user-context.txt`

The wrapper for each recent user question included in a follow-up request. `{{context}}` is replaced with bounded process-memory conversation text. The wrapper labels that text as untrusted continuity context used only to resolve references, not another question to answer.

### `prior-assistant-context.txt`

The wrapper for each recent validated AskARabbi answer. `{{context}}` is replaced with bounded process-memory answer text. It explicitly prohibits treating that answer as evidence or repeating it in place of answering the follow-up. Only answers that already passed deterministic citation validation and the independent claim-support audit enter conversation history.

### `current-question.json`

The static portion of the current request. It reinforces direct, concise BLUF phrasing, connected prose, natural source mentions, and the prohibition on report-like headings, evidence-container language, and implementation details. The library adds a deterministic `answerFocus` that distinguishes a request for rationale from a request for named authorities and tells the model not to substitute an earlier conclusion or unrelated workaround. For a combined bar- or bat-mitzvah portion-and-summary request, that focus instead requires the resolved portion in the first sentence followed by exactly two substantive story paragraphs. Clarifying questions are optional and must stay tightly connected to the current line of inquiry. It also tells the model to return a concise conversation title only when the application marks the turn as the first response. Profile use remains limited to respectful personalization, age-appropriate clarity, and harmless terminology choices. The prompt discourages repeated points and sources, limits factual content to retrieved or locally calculated evidence, and delimits textual evidence as untrusted data. The library adds the current question, one-time title flag, a profile context containing calculated age rather than birth date, and selected evidence before serializing the complete user message as JSON. Calendar capabilities may privately use saved profile values, but their existence and execution are never mentioned in the displayed answer.

### `validation-repair.txt`

The one-time correction request used when the first structured draft fails deterministic validation or the independent relevance-and-support audit. `{{validationError}}` is replaced with the precise validation failure. The repair must remain concise and reuse the same packet. It may split, merge, add, remove, or rewrite statement objects and reassign existing packet evidence so every proposition is atomic and fully supported, but it can never invent evidence IDs, sources, quotations, attributions, or source relationships.

### `interpretive-notice.txt`

Legacy compatibility text retained in `GroundedPromptSet` and `GroundedAnswer` for existing consumers. Current web and console renderers do not append it to answers. The model never writes or modifies this value.

### `grounded-answer.schema.json`

The strict Structured Outputs contract. It is not prose, but it constrains the model just as importantly as a prompt. Each claim and disagreement represents one independently verifiable proposition and contains an optional attribution plus mandatory quotation objects covering every cited evidence ID. Each quotation identifies which part of the proposition it supports. The response also contains the optional one-time conversation title, internal limitations, an optional clarifying question, and the human-guidance flag. The application separately verifies every evidence ID and exact quotation after deserialization. Limitations remain available for diagnostics but are not rendered as a stock “what the sources do not answer” paragraph.

### `grounded-support-validation.txt`

The independent grounding-audit contract. After exact IDs and quotations are verified, this second structured request first checks whether the draft as a whole directly answers the current question's requested dimension. It then checks whether every claim and disagreement is relevant and follows from its cited passages. It rejects rule restatements offered as rationales, anonymous summaries offered as answers about decision-makers, unrelated legal analogies, overstated source relationships, and modern rulings that are not explicitly limited to what the ancient evidence supports.

### `grounded-support-validation.schema.json`

The strict Structured Outputs contract for the audit. It requires one overall responsiveness decision and explanation plus identified statement evaluations with separate relevance and evidentiary-support decisions. The application verifies that every expected claim and disagreement ID appears exactly once.

## Editing rules

- Keep `{{context}}` in both conversation templates.
- Keep `{{validationError}}` in the repair template.
- Keep the evidence markers nonempty and different from one another.
- Keep `interpretive-notice.txt` nonempty and under 1,000 characters while the compatibility property remains required.
- Keep the schema compatible with Azure OpenAI strict Structured Outputs.
- Run both library tests and the prototype build after any prompt or schema change.
