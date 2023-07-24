using System.Threading.Tasks;

namespace CommandHandlers;

public interface IAdrInitCommandHandler
{
    Task<int> InitializeAsync(string adrRootPath, string templateRootPath);
}

