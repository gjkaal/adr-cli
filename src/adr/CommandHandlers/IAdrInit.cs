using System.Threading.Tasks;

namespace adr.CommandHandlers;

/// <summary>
/// Command handler for initializing a new ADR folder.
/// </summary>
public interface IAdrInit
{
    /// <summary>
    /// Initialize an ADR set at the current location.
    /// </summary>
    /// <param name="adrRootPath">The (relative) path for the Adr folder.</param>
    /// <param name="templateRootPath">The (relative) path for the template folder.</param>
    /// <returns></returns>
    Task<int> InitializeAsync(string adrRootPath, string templateRootPath);
}

