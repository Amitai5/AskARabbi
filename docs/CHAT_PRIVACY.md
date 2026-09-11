# Chat storage and Azure AI retention

AskRabbi saves chats so users can reopen and continue them. The application disables Azure OpenAI's stored-response feature, but Microsoft may still retain prompts and answers for abuse monitoring. These are separate storage boundaries: do not claim that AskRabbi stores no chat data or that the service has zero retention.

## Current behavior

| Data or processing | Storage boundary |
| --- | --- |
| Saved questions, answers, source references, and conversation metadata | Stored by AskRabbi in Azure Cosmos DB for MongoDB as owner-scoped chat history. |
| Model drafts, validation, repairs, and calendar-function continuations | The shared `AzureResponsesTransport` sets `StoredOutputEnabled = false` (`store=false`) on Responses requests. Retries use the same policy. |
| Source lookups | `AzureOpenAIVectorStoreClient.SearchAsync` explicitly sends `store=false`, including retries. |
| Background Dvar Torah research, writing, and review | Use the same model and source-search clients. These are application jobs, not Azure Responses `background=true` requests. |
| Approved Torah/source corpus | Intentionally uploaded and retained in Azure OpenAI files/vector storage; it is not user chat history. |
| Microsoft abuse monitoring | Separate provider-controlled storage may retain prompts and answers for authorized review. `store=false` does not disable it. |
| Backups and operational/security logs | Separate retention policies apply. This application does not promise immediate physical purge of provider backups or logs. |

Microsoft documents [stateless Responses requests](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/responses#encrypted-reasoning-items), their distinction from [stateful background mode](https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/responses#background-tasks), and [provider data processing and abuse monitoring](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy). No fixed provider-retention duration is promised here.

## User-visible disclosure

The sign-in screen has an expandable **Chat history and AI privacy** notice; **Settings & Personalization → Your data** shows the same notice beside the deletion controls. Both render `Frontend/src/components/ChatPrivacyNotice.tsx` so the provider-neutral storage and abuse-review explanation stays consistent without new API calls. The notice distinguishes saved chats from separately retained security and abuse-prevention records without naming providers or linking to provider documentation.

Users can delete saved chats or their account. Chat deletion keeps the account, preferences, and usage counters. Account deletion covers the owned application records and configured WorkOS identity. Neither action deletes Microsoft's abuse-monitoring records or the user's upstream Google account. See [account data deletion](ACCOUNT_DATA.md) for confirmation, concurrency, and retry behavior.

A separate private-chat mode remains a design proposal, not a production feature. Changing wording must not silently remove conversation persistence or advertise an unimplemented privacy mode.

## Production audit: September 11, 2026

Read-only checks confirmed that `askarabbi-api-production` and `askarabbi-dvar-torah-production` both use `AARProduction-OpenAI`. The resource did not expose `ContentLogging=false`, and its diagnostic-settings list was empty. An empty diagnostic-settings list does **not** establish that abuse-monitoring storage is disabled.

Microsoft identifies `ContentLogging=false` as the confirmation that abuse-monitoring content storage is off. [Modified abuse monitoring requires Microsoft approval and eligibility](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/limited-access#registration-for-modified-guardrails-andor-abuse-monitoring); changing content-filter thresholds or networking does not grant it. Recheck the deployed resource before changing this disclosure. This audit verifies configuration, not the contents or deletion of historical provider records.

To verify the relevant resource capability without exposing credentials:

```powershell
az cognitiveservices account show --name AARProduction-OpenAI --resource-group AARProduction --query 'properties.capabilities' --output json
```

## Change boundaries

This disclosure update does not change storage, deletion, API contracts, Microsoft settings, or database schemas. It adds no dependencies or network requests. Any future provider, retention-policy, or private-mode change must update both this document and the shared application notice after verification.
