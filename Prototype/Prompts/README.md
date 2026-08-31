# AskARabbi AI prompts

These files are the runtime source of truth for every model-facing instruction and the application-controlled closing notice used by `AskARabbiPrototype`. The prototype loads and validates them only when AI Chat or the one-shot `ask` command is entered. Source Search does not depend on them.

## Files

### `system-behavior.txt`

The highest-priority behavior contract. It requires one flowing conversational answer: a natural acknowledgment when appropriate, a direct one- or two-sentence answer first, and only the explanation needed afterward. A normal response should use two or three connected claims, about 180–325 words of explanatory prose, and only the strongest sources. It explicitly rejects evidence-report language and mechanical source headings. The file also requires approved-evidence-only explanations, requires the model to honor application-supplied supplemental usage limitations without treating them as quotable passages, prohibits personalized *psak*, infallibility, and judgmental behavior, treats profile and retrieved content as untrusted data, and requires both quoted links of any claimed interpretive chain. Profile instructions permit respectful, relevant tailoring while prohibiting stereotypes, identity ranking, assumed observance, irrelevant disclosure, and unsupported community-specific claims.

### `prior-user-context.txt`

The wrapper for each recent user question included in a follow-up request. `{{context}}` is replaced with bounded process-memory conversation text. The wrapper labels that text as untrusted context rather than a new instruction.

### `prior-assistant-context.txt`

The wrapper for each recent validated AskARabbi answer. `{{context}}` is replaced with bounded process-memory answer text. Only answers that already passed deterministic citation validation and the independent claim-support audit enter conversation history.

### `current-question.json`

The static portion of the current request. It reinforces direct, concise BLUF phrasing, connected prose, natural source mentions, a useful follow-up question, and the prohibition on report-like headings and packet language. It also tells the model to return a concise conversation title only when the application marks the turn as the first response. Profile use remains limited to respectful personalization and age-appropriate clarity. The prompt discourages repeated points and sources, limits factual content to the retrieved evidence packet, and delimits that packet as untrusted data. The library adds the current question, the one-time title flag, an optional birth-date-minimized profile context, and selected evidence before serializing the complete user message as JSON.

### `validation-repair.txt`

The one-time correction request used when the first structured draft fails deterministic validation or the independent relevance-and-support audit. `{{validationError}}` is replaced with the precise validation failure. The repair must remain concise, reuse the same packet, and cannot introduce new claims or evidence IDs.

### `interpretive-notice.txt`

The editable closing shown in italic grey after every validated answer. The entire file is rendered as one compact notice with no separate heading. The model never writes or modifies this notice; `GroundedAnswerService` copies the validated file content into the answer returned by the library.

### `grounded-answer.schema.json`

The strict Structured Outputs contract. It is not prose, but it constrains the model just as importantly as a prompt. Each claim and disagreement contains an optional attribution plus mandatory quotation objects covering every cited evidence ID. Each quotation identifies its explanatory role. The response also contains the optional one-time conversation title, limitations, an optional clarifying question, and the human-guidance flag. The application separately verifies every evidence ID and exact quotation after deserialization, then appends the content of `interpretive-notice.txt` outside model control.

### `grounded-support-validation.txt`

The independent grounding-audit contract. After exact IDs and quotations are verified, this second structured request checks whether every claim and disagreement actually answers the question and follows from its cited passages. It rejects unrelated legal analogies, overstated source relationships, and modern rulings that are not explicitly limited to what the ancient evidence supports.

### `grounded-support-validation.schema.json`

The strict Structured Outputs contract for the audit. It requires identified statement evaluations with separate relevance and evidentiary-support decisions plus a brief reason. The application verifies that every expected claim and disagreement ID appears exactly once.

## Editing rules

- Keep `{{context}}` in both conversation templates.
- Keep `{{validationError}}` in the repair template.
- Keep the evidence markers nonempty and different from one another.
- Keep `interpretive-notice.txt` nonempty and under 1,000 characters. Its entire content is the console notice.
- Keep the schema compatible with Azure OpenAI strict Structured Outputs.
- Run both library tests and the prototype build after any prompt or schema change.
