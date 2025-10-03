namespace McpCore;

public class VerifyResult
{
    private readonly List<string> messages = [];

    public VerifyResult()
    {
    }

    public void Fail(string message)
    {
        messages.Add(message);
        Success = false;
    }

    public void Remark(string message)
    {
        messages.Add(message);
    }

    public bool Success { get; private set; }
    public bool Failure => !Success;
    public IEnumerable<string> Messages => messages;

    public static VerifyResult Start(string message)
    {
        var result = new VerifyResult();
        result.Remark(message);
        return result;
    }

    public void ThrowIfFailed()
    {
        if (!Success)
        {
            throw new ArgumentException(string.Join(", ", messages));
        }
    }
}