# AskRabbi backend

`Backend` is the reserved production boundary for the future AskRabbi API. No server project or runtime dependency is intentionally included in this milestone.

The planned API will be responsible for authentication exchange, authorization, conversation persistence, usage policies, retrieval orchestration, and access to the existing `AskARabbiLIB` grounding services. The frontend currently uses an in-memory demo authentication adapter and makes no backend or AI requests.

WorkOS AuthKit is the planned authentication and user-management infrastructure. Users will see provider choices such as Google, not a “WorkOS” login. The backend will own authorization redirects, callback validation, code exchange, local-user mapping, and secure application sessions. See the [production authentication plan](../docs/AUTHENTICATION.md) for the provider rollout, endpoint boundary, data ownership, and security requirements.

When implementation begins, create the ASP.NET Core project here and keep transport, identity, persistence, and external-provider code outside the reusable religious-text and grounding domain library.
