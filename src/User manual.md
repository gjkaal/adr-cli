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
