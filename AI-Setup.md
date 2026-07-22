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

`adr-cli` never stores an API key in `adr.config.json`. It authenticates with
[`DefaultAzureCredential`](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential),
which picks up your Azure CLI login automatically:

```bash
az login
```

If `adr-cli` ever runs inside Azure (e.g. a hosted job), `DefaultAzureCredential` will instead pick
up the resource's managed identity - no code or config changes needed, as long as that identity has
been granted access to the Foundry project (see step 3).

## 2. Add the `ai` section to `adr.config.json`

Add an `ai` object next to the existing `path` / `templates` / `tasks` settings:

```json
{
  "path": "\\docs\\adr",
  "templates": "\\docs\\adr-templates",
  "tasks": "\\docs\\planning",
  "ai": {
    "provider": "AzureFoundry",
    "endpoint": "https://<your-project>.services.ai.azure.com",
    "deploymentName": "gpt-4o"
  }
}
```

- **provider** - must be `AzureFoundry` today; an empty or missing `ai` section (or a missing
  `provider`) means AI drafting is off.
- **endpoint** - your Azure AI Foundry project endpoint.
- **deploymentName** - the name of the chat model deployment to use.

## 3. Grant access

Whoever runs `adr-cli` (your own account after `az login`, or a managed identity) needs a role that
allows creating and running agents against the Foundry project - typically **Azure AI Developer**
or **Azure AI User**, assigned on the Foundry project resource. See [Azure AI Foundry RBAC
roles](https://learn.microsoft.com/azure/ai-foundry/concepts/rbac-azure-ai-foundry) for the current
list.

## 4. Try it

```bash
adr new --title "Use a message bus for service integration" --ai
```

`adr-cli` will:

1. Gather short summaries (title, status, context) of your existing ADRs.
2. Start a Foundry agent with a tool it can use to search those summaries for related or
   conflicting prior decisions.
3. Fill in `Decision` and `Consequences` from the agent's reply, then open the ADR in your editor
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
| Authentication / `CredentialUnavailableException` errors | Run `az login`, or confirm the managed identity in use has been granted a role on the Foundry project. |
| "Agent run did not complete within 60s" | The model deployment may be slow, overloaded, or unreachable - verify `endpoint` and `deploymentName`, and check the deployment's status in Azure AI Foundry. |
| Decision/Consequences look wrong or empty | The agent didn't follow the expected `## Decision` / `## Consequences` format; the raw reply is kept as the Decision text so nothing is lost - edit the ADR by hand after it opens. |
