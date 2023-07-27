using System.Diagnostics;

namespace adr.Extensions;

public interface IProcessHelper
{
    void Start(string fullName);
    void Start(ProcessStartInfo processStartInfo);
    void Start(string v, string fullName);
}
