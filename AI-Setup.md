# AI-Assisted ADR Proposals

`adr-cli` can optionally draft the **Decision** and **Consequences** sections of a new ADR using
an Azure AI Foundry agent, grounded in your existing ADRs. This is entirely optional: without the
setup below, `adr new` / `adr_new` behave exactly as they always have. AI drafting only runs when
you explicitly pass `--ai` (CLI) or `ai: true` (MCP tool `adr_new`) **and** an AI provider is
configured - otherwise it's a no-op.

## Prerequisites

- An Azure AI Foundry project with a deployed chat model (e.g. `gpt-4o`). This guide assumes the
  project and deployment already exist - see [Azure AI Foundry
  documentation](https://learn.microsoft.com/azure/ai-foundry/) if you still need to create one.
- The Azure CLI, for local authentication.

## 1. Authenticate

`adr-cli` never stores a secret in `adr.config.json`. It supports two auth paths, checked in this
order:

1. **API key** (optional) - set the `ADR_CLI_AI_API_KEY` environment variable to the key shown on
   your project's **Overview** page under **Endpoints and keys**.
2. **`DefaultAzureCredential`** (fallback, used when `ADR_CLI_AI_API_KEY` is unset; requires no
   config) - picks up your Azure CLI login automatically:

    ```bash
    az login
    ```

    If `adr-cli` ever runs inside Azure (e.g. a hosted job), `DefaultAzureCredential` will instead
    pick up the resource's managed identity - no code or config changes needed, as long as that
    identity has been granted access (see step 3). `DefaultAzureCredential` also checks
    `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET` / `AZURE_TENANT_ID` before trying `az login`, if you'd
    rather use a service principal via environment variables than an API key.

`az login` alone is enough to get started - the API key is only needed if you'd rather not rely on
an interactive/managed-identity login.

If you work across multiple repositories that each need a different API key (e.g. different Azure
AI Foundry projects per client), set `"apiKeyName"` in the `ai` section (step 2) to read from a
differently-named environment variable instead of the `ADR_CLI_AI_API_KEY` default - see step 2.

## 2. Add the `ai` section to `adr.config.json`

Add an `ai` object next to the existing `path` / `templates` / `tasks` settings:

```json
{
  "path": "\\docs\\adr",
  "templates": "\\docs\\adr-templates",
  "tasks": "\\docs\\planning",
  "ai": {
    "provider": "AzureFoundry",
    "endpoint": "https://<your-resource>.services.ai.azure.com/openai/v1",
    "deploymentName": "<your-deployment-name>"
  }
}
```

- **provider** - must be `AzureFoundry` today; an empty or missing `ai` section (or a missing
  `provider`) means AI drafting is off.
- **endpoint** - the OpenAI-compatible endpoint for your Foundry resource:
  `https://<resource>.services.ai.azure.com/openai/v1` (same `<resource>` name as the Cognitive
  Services resource, different domain/path). This is the endpoint used by "agent mode" quickstart
  code snippets in the Foundry portal - not the plain Cognitive Services resource endpoint
  (`*.cognitiveservices.azure.com`) and not the Foundry project endpoint
  (`*.services.ai.azure.com/api/projects/...`); neither of those work for this SDK path.
- **deploymentName** - the name of the chat model deployment to use, as shown under **Models +
  Endpoints** for the resource. Note some models (e.g. older `gpt-4o` versions) may be in a
  deprecating state and unavailable for new deployments - check the model catalog if deployment
  fails.
- **apiKeyName** (optional) - name of the environment variable to read the API key from, instead of
  the `ADR_CLI_AI_API_KEY` default. Only needed if this machine works across multiple repositories
  that each need a distinct key stored under a distinct variable name.

## 3. Grant access

Only relevant if you're using `DefaultAzureCredential` instead of an API key (step 1). Whoever runs
`adr-cli` (your own account after `az login`, or a managed identity) needs a role that allows
calling the deployed model - typically **Cognitive Services OpenAI User**, assigned on the
underlying Azure AI Services resource.

## 4. Try it

```bash
adr new --title "Use a message bus for service integration" --ai
```

`adr-cli` will:

1. Gather short summaries (title, status, context) of your existing ADRs.
2. Send them, along with the title and context, to the configured model in a single request - so it
   can stay consistent with (or flag conflicts with) prior decisions.
3. Fill in `Decision` and `Consequences` from the model's reply, then open the ADR in your editor
   as usual so you can review and edit before committing.

Via MCP, pass `"ai": true` as an argument to the `adr_new` tool.

## Failure behavior

If AI drafting fails for any reason (auth, network, timeout, misconfiguration), `adr-cli` prints a
warning and creates the ADR exactly as it would without `--ai` - drafting is a best-effort bonus,
never a blocker.

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| "AI proposal generation is not configured" | No `ai` section (or empty `provider`) in the `adr.config.json` that's active for your current directory - check with `adr context`. |
| 401 / authentication errors, `ADR_CLI_AI_API_KEY` set | The key was likely copied from the wrong resource, or has been rotated - re-copy it from the project's Overview page. |
| 401 / `CredentialUnavailableException`, no `ADR_CLI_AI_API_KEY` set | Run `az login`, or confirm the managed identity/service principal in use has the **Cognitive Services OpenAI User** role on the resource. |
| `404 Resource not found` | `endpoint` is likely the plain Cognitive Services endpoint or the Foundry project endpoint instead of the `.../openai/v1` endpoint - see step 2 above. |
| `404 DeploymentNotFound` | `deploymentName` doesn't match an existing deployment on this resource - check **Models + Endpoints** in the Foundry portal for the exact name (deployment names are user-chosen and don't have to match the underlying model name). |
| Request is slow or times out | The model deployment may be slow, overloaded, or unreachable - verify `endpoint` and `deploymentName`, and check the deployment's status in Azure AI Foundry. |
| Decision/Consequences look wrong or empty | The agent didn't follow the expected `## Decision` / `## Consequences` format; the raw reply is kept as the Decision text so nothing is lost - edit the ADR by hand after it opens. |
