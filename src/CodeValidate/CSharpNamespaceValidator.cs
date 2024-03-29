﻿using System.Collections.ObjectModel;

namespace CodeValidate;

/// <summary>
/// Validates the namespace of all files in the specified directory
/// </summary>
public class CSharpNamespaceValidator : IValidator
{
    private readonly string[] SkipFiles = new[] { "GlobalUsings.cs" };
    private readonly string[] skipList = new[] { "bin", "obj", "Properties", ".git", ".vs", ".idea", "TestResults" };
    private readonly DirectoryInfo directory;
    private readonly IStdIo stdIo;

    private readonly string[] ignoreList;
    public ReadOnlyCollection<string> IgnoreList => new(ignoreList);

    public CSharpNamespaceValidator(string[] args, IStdIo stdIo, string[] ignoreList)
    {
        directory = new DirectoryInfo(args[0]);
        this.stdIo = stdIo;
        this.ignoreList = ignoreList;
    }

    public int Validate()
    {
        foreach(var ignore in ignoreList)
        {
            stdIo.WriteInfo($"Ignoring code files that end with: {ignore}");
        }

        if (!directory.Exists)
        {
            stdIo.WriteError($"Directory {directory.FullName} does not exist.");
            return -1;
        }

        // verify if this is a project folder
        var files = directory.GetFiles("*.csproj", SearchOption.TopDirectoryOnly);

        if (files.Length > 0)
        {
            stdIo.WriteInfo($"Project file found in {directory.FullName}.");
            return Validate(directory, string.Empty);
        }
        else
        {
            var errors = 0;
            stdIo.WriteInfo($"Using {directory.FullName} as solution folder.");
            foreach (var subDirectory in directory.GetDirectories())
            {
                errors += Validate(subDirectory, string.Empty);
            }
            return errors;
        }
    }

    private int Validate(DirectoryInfo directory, string baseNamespace)
    {
        if (!directory.Exists)
        {
            stdIo.WriteError($"Directory {directory.FullName} does not exist.");
            return -1;
        }

        if (skipList.Contains(directory.Name))
        {
            stdIo.WriteInfo($"Skipping directory {directory.FullName}.");
            return 0;
        }

        var errors = 0;
        var files = directory.GetFiles("*.cs", SearchOption.TopDirectoryOnly);
        var expectNameSpace = BuildExpectedNamespace(directory, baseNamespace);
        stdIo.WriteInfo($"Validating {files.Length} files in {directory.FullName}.");

        foreach (var file in files)
        {
            if (CheckForIgnore(file)) continue;

            var lines = File.ReadAllLines(file.FullName);
            if (lines.Any(l => l.Contains("<auto-generated>", StringComparison.OrdinalIgnoreCase)))
            {
                stdIo.WriteInfo($"Skipping auto generated file {file.FullName}.");
                continue;
            }

            var namespaceLine = lines.FirstOrDefault(l => l.StartsWith("namespace"));
            if (namespaceLine == null)
            {
                stdIo.WriteInfo($"No namespace found in {file.FullName}");
                errors++;
                continue;
            }

            var actualNamespace = namespaceLine.Split(' ')[1].TrimEnd(';');
            if (string.Compare(expectNameSpace, actualNamespace, StringComparison.OrdinalIgnoreCase) != 0)
            {
                stdIo.WriteError($"ERROR: Namespace mismatch in {file.FullName}: expected {expectNameSpace}, actual {actualNamespace}");
                errors++;
            }
        }

        foreach (var subDirectory in directory.GetDirectories())
        {
            errors += Validate(subDirectory, expectNameSpace);
        }

        return errors;
    }

    private bool CheckForIgnore(FileInfo file)
    {
        if (SkipFiles.Contains(file.Name))
        {
            stdIo.WriteInfo($"Skipping file {file.FullName}.");
            return true;
        }

        var ignoreCurrentFile = false;
        foreach (var ignore in ignoreList)
        {
            var useName = ignore;
            if (!useName.EndsWith(".cs")) useName = string.Concat(ignore, ".cs");
            if (file.FullName.EndsWith(useName, StringComparison.OrdinalIgnoreCase))
            {
                stdIo.WriteInfo($"Ignore file {file.FullName}.");
                ignoreCurrentFile = true;
                break;
            }
        }

        return ignoreCurrentFile;
    }

    private string BuildExpectedNamespace(DirectoryInfo directory, string baseNamespace)
    {
        string cleanName;
        if (directory.Name.Contains(' '))
        {
            cleanName = directory.Name.Replace(" ", string.Empty);
            stdIo.WriteError($"WARNING: Directory {directory.FullName} contains spaces, using {cleanName} as namespace.");
        }
        else
        {
            cleanName = directory.Name;
        }

        if (string.IsNullOrEmpty(baseNamespace)) return cleanName;        
        if (directory.Name.StartsWith(baseNamespace, StringComparison.OrdinalIgnoreCase)) return cleanName;
        return string.Concat(baseNamespace, ".", cleanName);
    }
}