# 00002. Commands are implemented with System.CommandLine

2023-08-03

## Status

__Accepted__

## Context

The ADR-CLI tool is a command line tool. Upon startup, a list of command line arguments are passed to the applicatiuon and they
need to be interpreted. By using a commandline interpreter, it is easier to focus on functionality and leave the handling of
command line parameters to a generic library.

## Decision

There are different options for the basic functionality of a command line interpreter. Each has their pros and cons. 

Option 1: Do it yourself
Option 2: [commandlineparser](https://github.com/commandlineparser/commandline)
Option 3: [system.commandline](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)

The first option should be adequate as the number of commands that will be created in the first version of the project
is rather limited. Option 2 is a well known and much used library, that has the advantage of having no dependencies on
any other library. The last option is still in beta, but it is a library that has some resemblance to the _commandlineparser_
and it's part of the .Net Fundamentals.

Options not evaluated:
- [CliFx](https://github.com/Tyrrrz/CliFx)
 
Even though building code yourself is probably easier to start with, for future development, it might not be the best option. Using
a library has the advantage of having a users group and it's more likely to evolve with the evolution of the .NET framework. For this
project, the [system.commandline](https://learn.microsoft.com/en-us/dotnet/standard/commandline/) is used.

## Consequences

System.CommandLine is currently in PREVIEW (2.0.0-beta4.22272.1). Some information relates to prerelease product that may be 
substantially modified before it's released. This means that with newer versions, rework might be required.

The positive consequence is that by using this library, some experience is gained with a tool that probably is part
of the _.Net Fundamentals_ and it will have long term support.