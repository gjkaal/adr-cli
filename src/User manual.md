# ADR-CLI toolset

An architecture decision record (ADR) is a document that captures an important 
architecture decision made along with its context and consequences.

More information on ADR's and how to use them, can be found [here](https://github.com/joelparkerhenderson/architecture-decision-record#how-to-start-using-adrs-with-tools).

The adr-cli is a command line tool that provides a toolset for
managing an Architecture Decision Record repository based on
a folder structure with markdown / json files. 

## Getting started

- Install the tool (MSI)
- Starting a new ADR repository
- Adding your first record 

### Installing

To install the tool, create a folder in the program files folder, or any other location that you see fit 
where executable programs should be located. Using the programs folder has the advantage that its not 
necessary to add an execution policy to the file.

The suggested location is:

`C:\Program Files (x86)\Nauplius\adrcli`

Step 2 is to add the path to the executable to the PATH environment setting. This can be done using the
control panel or by executing a shell command (only with elevated privileges => administrator):

`setx /M "%PATH%;C:\Program Files (x86)\Nauplius\adrcli\"` 

### Create your first ADR

After installing the tool, decide at what folder you want to initialize
the ADR repository. This can be at any location, but at the root of a GIT repository
would be a logical choice. Initialization is done by executing an `init` command.
Initialization can only be done once and it is required before adding any
decision records

```
> C:
> CD C:/repositories/project
> adr-cli init
> adr_cli new "Use a portable database as a baseline"
```

## Commands

### ADR Commands

| command | description |
| ------- | ---------- |
| init         | Initialize a new ADR folder |
| sync         | Sync the metadata using the content in the markdown files |
| new          | Create a new Architecture Decision Record |
| copy         | Copy an existing ADR to as a new ADR |
| list         | List all Architecture Decision Records |
| find         | Find Architecture Decision Records |
| link         | Link 2 ADR's for ammend / clarify or some other reason |
| rlink        | Remove all links from one ADR to another |
| generate-toc | Generate a table of contents |

### Task Management Commands

| command | description |
| ------- | ---------- |
| task-new  | Create a new task for project planning |
| task-list | List all tasks |
| task-find | Find tasks using a filter |
| task-update | Update a task's status |
| task-link | Link two tasks together |
| task-unlink | Remove link between two tasks |
| task-toc | Generate table of contents for tasks |

### Initialization

Initialize a new ADR folder in the current folder. The command initializes a configuration
file that is used to describe the ADR repository. When calling the adr-cli tool from any subfolder,
this file is located and used to locate the ADR repository.

The adr document root and the adr template root can be set while initializing. The config file is
a simple json file. If this file is modified, the tool uses the new settings immediately. If you
modify the settings file, you have to move the documentation and template files by hand.

The default paths for the Adr are:

- \docs\adr
- \docs\adr-templates

__Usage__

`adr-cli init [options]`

__Options__

```
  --adrRoot <adrRoot>  Set the adr root directory
  --tmpRoot <tmpRoot>  Set the template root directory
```

### Creating new records

Create a new Architecture decision record, a new Architectural requirement or a revision for an earlier record.
After creating a record, the default editor for the document (markdown) is opened, so the details for the ADR
can be modified.

The record is created using a template. These templates can be found in the template root folder. The first time
a template is used, it is created using a default setup.

The title for the ADR is required. It will be used to define part of the file name. An ADR filename starts with 
a numeric value, which indicates the sequence in which the records are defined. This value is also used when
referencing records (for the `--rev` option and the `link`, `rlink` commands.

__Usage__

To create a new record:

`adr-cli new --title "We use a standard for describing our API"`

To Create a new record that revises an existing record:

`adr-cli new --title "We use OpenAPI specification first" --rev 5`

To create an Architectural requirement

`adr-cli new --req --title "All modules must have traceablity using ILogger"`

__Options__

```
  --title "<title>" (REQUIRED)  The title for the ADR
  --req                         The ADR is a critical requirement.
  --rev <rev>                   The ADR rivision for an earlier ADR, provide a valid id.
```

### Copy to new record

You can create a new ADR based on an existing ADR (even if they are obsolete) by using th copy command.
This creates a copy of an existing record, with a new Id and with the state 'New'. A reference to the
original ADR is added (you could remove the reference with the ulink command).

If you want to make a revision with only minor changes, the copy command might be a better option than
using the `adr-cli new` command, as it is more compact. The only thing is that you cannot change the 
title, and with that the filename of the record.

__Usage__

Copy existing record `2` to a new record:

`adr-cli copy -s 2`

Copy existing record `5` to a new record as a revision for record 5:

`adr-cli copy --source 5 --rev`

__Options__

```
  -s | --source <recordId>  (REQUIRED)   Define the source ADR record
  --rev                                  Create the copy as a revision
```

### Sync Markup documents and metadata

The metadata files contain basic ADR information like the id, title, creation date status and references. This 
data is available in the markdown as well, but less structured. Although it's possible to deduct all information
from the markdown files, using the metadata is easier and faster. The downside is that when the documewnts are 
modified, the metadata will not change automatically. This command goes through all markdown files and modifies 
or restores the metadata files.

If you want to synchronize a single record, you can provide the identifier for that record.

__Usage__

Synchronize all records

`adr-cli sync`

Synchronize only record 15

`adr-cli sync --record 15`

__Options__

```
  --record <recordId>       Synchronize a single record
```


### List

Display an overview of all current ADR's on the console. The default response is with a single line
per record. It is possible to get more information using the `--verbose` option. The default sort order 
can be reversed with the `--desc` option.

__Usage__

`adr-cli list`

__Options__

```
  --desc          Show the ADR's with the latest ADR first
  --verbose       Show the ADR's more information
```

### Find

The adr-cli tool provides simple search functionality. A full text index search tool
probably does a much better job, but providing some basic search functionality helps
with finding the records you might want to reference using the command line.

Basic search is done using the metadata files. Keeping them in sync is therefore 
a good idea. If you want to search in the markdown files as well, use the `--full` option. 
The find command is an extended version of the list command and you can use the `--verbose` 
and `--desc` options.

Providing a longer text, will search for any word in the set. So a query for
"architecture selection" will return an ADR in the set if it contains either "architecture"
or "selection".

__Usage__

`adr-cli find -q sql`

`adr-cli find -q "architecture selection" --verbose`

__Options__

```
  --desc             Show the ADR's with the latest ADR first
  --verbose          Show the ADR's more information
  --full             Search the full records (slow)
```

### Linking and unlinking

To add references between ADR's, the tool allows you to add and remove links between ADR's
based on their record Id. You can add as much relations as you want. Removing a relation 
is always one way. So if you add a link from record 3 to record 1 and another link between
from record 1 to record 3, removing the link (or links) from record 3 to record 1, will not remove
any reverse link you made. It's easier that it sounds, try it and examine the markdown files
and the metadata files to see how it works.

Backward referencing is propably the way you want to work, but the tool doesn't prohibit 
adding a link referencing a record that did not exist when the ADR was defined.

__Usage__

Add a link to indicate that ADR 3 clarifier ADR 1:

`adr-cli link -s 3 -t 1 -reason clarifies`

Add a link to indicate that ADR 1 is explained in ADR 3:

`adr-cli link --source 1 -- target 3 -r "More information"`

Remove all references from ADR 1 to ADR 3:

`adr-cli rlink --source 1 -- target 3`

__Options__

```
  -s, --source <source> (REQUIRED)  The source ADR
  -t, --target <target> (REQUIRED)  The target ADR
  -r, --reason <reason>             The reason for the link (only while linking).
```

  link   Link 2 ADR's for ammend / clarify or some other reason
  rlink  Remove all links from one ADR to another

### Generating additional documentation

Add or update the table of content in the documentation root (one level above the ADR directory).

__Usage__

`adr-cli generate-toc`

__Options__

No options

## AI-Assisted Drafting

`adr new`/`adr-cli new --ai` and `task-new --ai` can draft an ADR's Decision/Consequences or a
task's Description/Details using a configured AI provider, grounded in your existing ADRs/tasks so
new records stay consistent with prior ones. This is entirely optional - without setup, both
commands behave exactly as they always have, and `--ai` is a no-op if nothing is configured.

Add an `ai` section to `adr.config.json`:

```json
{
  "ai": {
    "provider": "AzureFoundry",
    "endpoint": "https://<your-resource>.services.ai.azure.com/openai/v1",
    "deploymentName": "<your-deployment-name>"
  }
}
```

Credentials are read from the `ADR_CLI_AI_API_KEY` environment variable (falling back to
`DefaultAzureCredential`/`az login` if unset) - never stored in `adr.config.json`. See
[AI-Setup.md](../AI-Setup.md) for the full walkthrough, including how to authenticate, grant
access, and troubleshoot common errors.

## Task Export/Import (GitHub Projects Sync)

Tasks created and maintained in this repository can be exported to and re-imported from an
external work-management board - today, GitHub Projects (v2). The repository stays authoritative
for task content; export pushes local title/description/details and status out, import pulls
status (and, when safe, content) back in. Neither direction ever guesses at a conflict - see
`docs/adr/00008-*.md` and `docs/adr/00009-*.md` for the full design rationale.

### Setup

Add a `sync` section to `adr.config.json`:

```json
{
  "sync": {
    "provider": "GitHubProjects",
    "settings": {
      "ownerType": "User",
      "owner": "<your-github-username-or-org>",
      "projectNumber": 2,
      "statusFieldName": "Status",
      "importStatusMap": { "Todo": "New", "In progress": "Active", "Done": "Completed" },
      "exportStatusMap": { "New": "Todo", "Active": "In progress", "Completed": "Done" }
    }
  }
}
```

- **ownerType** - `"User"` or `"Organization"`, matching whether the project belongs to a personal
  account or an org (e.g. `github.com/users/<name>/projects/2` vs `github.com/orgs/<org>/projects/2`).
- **owner** - the GitHub username or organization name that owns the project.
- **projectNumber** - the numeric project number from its URL.
- **statusFieldName** - the name of the single-select field on the board that carries workflow
  status (defaults to `"Status"`, GitHub's default column field name).
- **importStatusMap** / **exportStatusMap** - map GitHub's status field option names to/from this
  tool's task status values (`New`, `OnHold`, `Planned`, `Active`, `Related`, `ReviewPending`,
  `ReviewComplete`, `AcceptancePending`, `Completed`, `Abandoned`). The two maps are independent -
  export is not derived by inverting import - since several GitHub options can map to the same
  local status. A status with no entry in the relevant map is left unmapped rather than guessed.

A personal access token is required, read from the `ADR_CLI_SYNC_PAT` environment variable by
default. If this machine works across multiple repositories that each need a different token (e.g.
different clients/orgs), set `"syncPatName"` in the `sync` section to read from a differently-named
variable instead:

```json
"sync": { "provider": "GitHubProjects", "syncPatName": "ADR_CLI_SYNC_GITHUB_PAT", "settings": { ... } }
```

The token needs write access to the target project.

Exporting creates a **Draft Issue** on the board (not a repository-backed Issue) - no target
repository or repository permissions are needed. Reading (import/discovery) works for any item type
on the board, including real issues someone added directly; pushing content, however, only works
for draft issues.

### Exporting tasks

Create or update each task's external item and push its local status.

__Usage__

`adr-cli task-export --id 3`

`adr-cli task-export --id "3,4,7"`

`adr-cli task-export -q authentication`

`adr-cli task-export --id 3 --force`

__Options__

```
  --id <ids>       Comma or space separated task ids to export. Required unless --filter is given.
  -q <filter>      Filter text to select tasks by title/description, used when --id is omitted.
  --force          Skip the remote-divergence check and overwrite the external item with local
                    content regardless. Intended for a single task (--id) at a time.
```

### Importing task status

Pull status (and, when safe, content) from each selected task's external item.

__Usage__

`adr-cli task-import`

`adr-cli task-import --id 3`

`adr-cli task-import -q authentication`

__Options__

```
  --id <ids>       Comma or space separated task ids to import.
  -q <filter>      Filter text to select tasks by title/description.
```

With neither `--id` nor `-q`, `task-import` defaults to every task already linked to the active
provider, plus any board item with no local counterpart yet - those are adopted as brand-new local
tasks automatically. Each task in a batch is reported individually (succeeded / unmapped / mismatch
/ skipped / failed) so one failure never hides the rest of the batch's results.

## Task Management

The adr-cli tool includes task management capabilities to help with project planning and tracking.

### Creating a new task

Create a new task for project planning with a title, description, and optional due date.

__Usage__

`adr-cli task-new --title "Implement user authentication" --description "Add OAuth2 support" --dueDate "2024-12-31"`

__Options__

```
  --title <title> (REQUIRED)        The title for the task
  --description, -d <description>   Description of the task
  --dueDate <dueDate>              Due date for the task
```

### Listing tasks

Display an overview of all tasks. The default response shows one line per task. Use `--verbose` for more information and `--desc` to reverse sort order.

__Usage__

`adr-cli task-list`

`adr-cli task-list --verbose --desc`

__Options__

```
  --desc          Show tasks with the latest task first
  --verbose       Show tasks with more information
```

### Finding tasks

Search for tasks using a filter on title and description. Use `--full` to include content in the search.

__Usage__

`adr-cli task-find -q authentication`

`adr-cli task-find -q "user login" --status InProgress --verbose`

__Options__

```
  -q <filter> (REQUIRED)    Filter text to search for
  --status <status>         Filter by task status
  --desc                    Show tasks with the latest task first
  --verbose                 Show tasks with more information
  --full                    Search the full records (slow)
```

### Updating task status

Update a task's status with an optional justification.

__Usage__

`adr-cli task-update -s 3 --status InProgress --justification "Started implementation"`

__Options__

```
  -s, --source <source> (REQUIRED)  The task ID to update
  --status <status> (REQUIRED)      New status for the task
  --justification, -j <text>        Justification for the status change
```

### Linking tasks

Create a relationship between two tasks.

__Usage__

`adr-cli task-link -s 5 -t 3 --remark "Depends on"`

__Options__

```
  -s, --source <source> (REQUIRED)  The source task ID
  -t, --target <target> (REQUIRED)  The target task ID
  --remark, -r <remark>            Remark explaining the relationship
```

### Unlinking tasks

Remove the relationship between two tasks.

__Usage__

`adr-cli task-unlink -s 5 -t 3`

__Options__

```
  -s, --source <source> (REQUIRED)  The source task ID
  -t, --target <target> (REQUIRED)  The target task ID
```

### Generating task table of contents

Generate a table of contents markdown file for all open tasks.

__Usage__

`adr-cli task-toc`

__Options__

No options

## Ideas and Improvements

Currently, version 1.0.0 of the tool is developed to help with managing an ADR on an existing project
that has some dire need of documentation. There are already some improvements that could be implemented,
like syncronizing a single record instead of checking all files. Another usefull addition, would be to 
generate a table of contents markdown file, containing the titles and possibly decisions of all ADR's 
in the repository.

If you encounter errors or if you have any suggestions for improvements, please send an email to:

[nauplius.software@gmail.com](mailto:nauplius.software@gmail.com)

Please add the version number of the adr-cli tool in the subject, together with a subject describing the issue.
You can find the version by using:

`adr-cli --version`
