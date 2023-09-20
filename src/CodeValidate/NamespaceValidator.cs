namespace CodeValidate;

/// <summary>
/// Validates the namespace of all files in the specified directory
/// </summary>
public class NamespaceValidator : IValidator
{
    private readonly string[] skipList = new[] { "bin", "obj", "Properties" };
    private readonly DirectoryInfo directory;

    public NamespaceValidator(string[] args)
    {
        directory = new DirectoryInfo(args[0]);
    }

    public int Validate()
    {
        if (!directory.Exists) return -1;
        return Validate(directory, string.Empty);
    }

    private int Validate(DirectoryInfo directory, string baseNamespace)
    {
        if (!directory.Exists) return -1;
        if (skipList.Contains(directory.Name)) return 0;

        var files = directory.GetFiles("*.cs", SearchOption.TopDirectoryOnly);
        var expectNameSpace = baseNamespace.Length>0 
            ? string.Concat(baseNamespace, ".", directory.Name) 
            : directory.Name;
        var errors = 0;
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file.FullName);
            var namespaceLine = lines.FirstOrDefault(l => l.StartsWith("namespace"));
            if (namespaceLine == null)
            {
                Console.WriteLine($"No namespace found in {file.FullName}");
                errors++;
                continue;
            }

            var actualNamespace = namespaceLine.Split(' ')[1].TrimEnd(';');
            if (expectNameSpace != actualNamespace)
            {
                Console.WriteLine($"Namespace mismatch in {file.FullName}: expected {expectNameSpace}, actual {actualNamespace}");
                errors++;
            }
        }

        foreach (var subDirectory in directory.GetDirectories())
        {
            errors += Validate(subDirectory, expectNameSpace);
        }

        return errors;
    }
}