namespace McpCore;

/// <summary>
/// Generates short, unique session codes for client connections Uses a combination of letters and
/// numbers to create human-friendly codes
/// </summary>
public class SessionCodeGenerator : ISessionCodeGenerator
{
    private static readonly char[] Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
    private static readonly Random Random = new();

    /// <summary>
    /// Generates a random 6-character session code (e.g., "ABC123")
    /// </summary>
    /// <returns>
    /// A unique session code
    /// </returns>
    public string GenerateCode()
    {
        var code = new char[6];
        for (var i = 0; i < 6; i++)
        {
            code[i] = Characters[Random.Next(Characters.Length)];
        }
        return new string(code);
    }

    /// <summary>
    /// Validates that a session code has the correct format
    /// </summary>
    /// <param name="code">
    /// The session code to validate
    /// </param>
    /// <returns>
    /// True if the code is valid
    /// </returns>
    public bool IsValidCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 6)
        {
            return false;
        }

        return code.All(c => Characters.Contains(char.ToUpper(c)));
    }
}