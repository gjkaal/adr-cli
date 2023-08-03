using System.Diagnostics;

namespace Adr.Cli.Extensions;

public interface IProcessHelper
{
    void Start(string fullName);
    void Start(ProcessStartInfo processStartInfo);
    void Start(string v, string fullName);
}
