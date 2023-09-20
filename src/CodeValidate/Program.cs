namespace CodeValidate;

internal class Program
{
    private static int Main(string[] args)
    {
        // Use the arguments to check which validator to call
        if (args.Length == 0)
        {
            Console.WriteLine("No validator specified");
            return -1;
        }

        IValidator validator;
        switch (args[0])
        {
            case "namespace":
                validator = new NamespaceValidator(args[1..]);
                break;

            default:
                Console.WriteLine("Unknown validator");
                return -1;
        }

        return validator.Validate();
    }
}
