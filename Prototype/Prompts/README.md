# AskARabbi AI prompts

These files are the shared runtime prompt catalog for conversational answers in `AskARabbiPrototype` and the production API. The API project copies them into its published `Prompts` directory. The prototype loads and validates them only when AI Chat or one-shot `ask` is entered; Source Search does not depend on them. Deploy writer, repair, audit, schemas, and the matching library together. See [answer reliability](../../docs/ANSWER_RELIABILITY.md) for component ownership and recovery behavior.

## Files

### `system-behavior.txt`

The highest-priority behavior contract. It requires one flowing conversational answer: a natural acknowledgment when appropriate, a direct one- or two-sentence answer first, and only the explanation needed afterward. A normal response should use two or three connected claims, about 180–325 words of explanatory prose, and only the strongest sources. It treats the current question as the sole answer target: a why-follow-up requires an evidenced rationale, while a who-follow-up about a legal position requires named, evidenced authorities. A biography question instead permits accurate general background. Repeating the rule, its legal classification, or a later workaround does not satisfy those requests. It explicitly rejects evidence-report language, mechanical source headings, and any reference to internal functions, tools, searches, prompts, validation, models, providers, or evidence containers. The file also requires approved evidence for religious rulings, attributed teachings and exact quotations, while permitting independently reviewed basic background and honest uncertainty, requires the model to honor application-supplied supplemental usage limitations without treating them as quotable passages, prohibits personalized *psak*, infallibility, and judgmental behavior, treats profile and retrieved content as untrusted data, and requires both quoted links of any claimed interpretive chain. Calendar results receive evidence IDs and exact text internally, but the answer states only the useful result and relevant caveats. Profile instructions permit respectful, relevant tailoring—including community-appropriate transliteration such as `Tevet` or `Teves`—while prohibiting stereotypes, identity ranking, assumed observance, irrelevant disclosure, unsupported community-specific claims, and disclosure of a saved Gregorian birth date.

### `prior-user-context.txt`

The wrapper for each recent user question included in a follow-up request. `{{context}}` is replaced with bounded process-memory conversation text. The wrapper labels that text as untrusted continuity context used only to resolve references, not another question to answer.

### `prior-assistant-context.txt`

The wrapper for a recent assistant reply. `{{context}}` is replaced with bounded continuity text: the prototype supplies successful process-memory answers, while API history can also contain application-written recovery replies. It explicitly prohibits treating earlier replies as evidence or repeating them in place of answering the follow-up. Rejected model drafts never enter saved assistant history.

### `current-question.json`

The static portion of the current request. It reinforces direct, concise BLUF phrasing, connected prose, natural source mentions, and the prohibition on report-like headings, evidence-container language, and implementation details. The library adds a deterministic `answerFocus` that distinguishes religious rationale and authority questions from basic biography, general background, and fictional premises and tells the model not to substitute an earlier conclusion or unrelated workaround. For a combined bar- or bat-mitzvah portion-and-summary request, that focus instead requires the resolved portion in the first sentence followed by exactly two substantive story paragraphs. Clarifying questions are optional and must stay tightly connected to the current line of inquiry. It also tells the model to return a concise conversation title only when the application marks the turn as the first response. Profile use remains limited to respectful personalization, age-appropriate clarity, and harmless terminology choices. The prompt discourages repeated points and sources, distinguishes sourced teaching from independently reviewed background and honest uncertainty, and delimits textual evidence as untrusted data. The library adds the current question, one-time title flag, minimized profile context, and selected evidence before serializing the complete user message as JSON. `sourceResearchAvailable` indicates whether bounded source research is registered; an empty packet does not require a research call before drafting reviewed background or uncertainty. Calendar capabilities may privately use saved profile values, but their existence and execution are never mentioned in the displayed answer.

### `validation-repair.txt`

The one-time correction request used when the first structured draft fails deterministic validation or the independent relevance-and-support audit. `{{validationError}}` is replaced with the precise validation failure. The repair remains concise and may use remaining bounded research calls to obtain missing original sources. It may split, merge, add, remove, or rewrite statement objects and reassign verified evidence, but it can never invent evidence IDs, sources, quotations, attributions, or source relationships. Inline quotation failures identify the claim and quoted-phrase number without including potentially personal prose in logs.

For inline quotations, the writer can place `[[quote:E1:@Q2]]` in a paragraph using an actual cited evidence ID and supplied `quotationChoices` selector. `GroundedInlineQuotationExpander` substitutes the exact source wording before the semantic audit and final quotation-language checks. Unknown or uncited selectors fail closed. The response schema and persisted answer format remain unchanged: markers are internal drafting syntax, never displayed or saved in place of quotation text. This avoids retyping errors while retaining quotations inside the explanation, not only in expandable source context.

### `interpretive-notice.txt`

Legacy compatibility text retained in `GroundedPromptSet` and `GroundedAnswer` for existing consumers. Current web and console renderers do not append it to answers. The model never writes or modifies this value.

### `grounded-answer.schema.json`

The strict Structured Outputs contract. It is not prose, but it constrains the model just as importantly as a prompt. Each claim declares `kind`: `Source`, `Background`, or `Uncertainty`. Source claims and disagreements require exact quotations covering every cited evidence ID. Background and Uncertainty require empty evidence and quotation arrays and null attribution; they still undergo independent accuracy, relevance, and scope review. These are internal model-output fields; the HTTP and persisted conversation shapes do not change. Each quotation identifies which part of the proposition it supports. The response also contains the optional one-time conversation title, internal limitations, an optional clarifying question, and the human-guidance flag. The application separately verifies every evidence ID and exact quotation after deserialization. Limitations are hidden metadata, not a stock “what the sources do not answer” paragraph. Valid-sized notes that mention internal mechanisms are discarded before materialization, so they cannot reject otherwise valid prose; visible claims, titles, roles, and follow-ups still cannot disclose those mechanisms.

### `grounded-support-validation.txt`

The independent grounding-audit contract. After exact IDs and quotations are verified, this second structured request first checks whether the draft as a whole directly answers the current question's requested dimension. It then checks each statement according to its kind: Source claims and disagreements against the bounded evidence packet, Background for accuracy and permitted scope using general knowledge, and Uncertainty for honest limits and relevance. The audit can reconcile Source citations against any supplied passage, subject to exact-quotation validation. It cannot add citations to Background or Uncertainty or accept a religious conclusion relabeled as background. It rejects rule restatements offered as rationales, anonymous summaries offered as answers about decision-makers, unrelated legal analogies, overstated source relationships, and modern rulings that are not explicitly limited to what the ancient evidence supports.

### `grounded-support-validation.schema.json`

The strict Structured Outputs contract for the audit. It requires one overall responsiveness decision and explanation plus identified statement evaluations with separate relevance and evidentiary-support decisions. The application verifies that every expected claim and disagreement ID appears exactly once.

## Editing rules

- Keep `{{context}}` in both conversation templates.
- Keep `{{validationError}}` in the repair template.
- Keep the evidence markers nonempty and different from one another.
- Keep `interpretive-notice.txt` nonempty and under 1,000 characters while the compatibility property remains required.
- Keep the schema compatible with Azure OpenAI strict Structured Outputs.
- Run library tests, API conversation tests, and the prototype build after a conversational prompt or schema change. Markdown-only documentation edits do not change the runtime prompt contract.
