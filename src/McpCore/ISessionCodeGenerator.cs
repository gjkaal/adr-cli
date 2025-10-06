namespace McpCore;

public interface ISessionCodeGenerator
{
    string GenerateCode();

    bool IsValidCode(string code);
}