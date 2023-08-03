# ADR-CLI toolset

The adr-cli is a command line tool that provides a toolset for
managing an Architecture Decision Record repository based on
a folder structure with markdown / json files. 

## Getting started

- Install the tool (MSI)
- Starting a new ADR repository
- Adding your first record 

After installing the tool, decide at what folder you want to initialize
the ADR repository. This can be at any location, but at the root of a GIT repository
would be a logical choice. Initialization is done by executing an `ínit` command.
Initialization can only be done once and it is required before adding any
decision records

`adr-cli init` 

initializes a new ADR repository at the currant path, by defining
a folderset and some files. For example:




