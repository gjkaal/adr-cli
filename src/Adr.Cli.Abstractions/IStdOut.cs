using McpCore;

namespace Adr.Cli.Services;

public interface IStdOut
{
    void Mute();

    void UnMute();

    bool Muted { get; }

    void Write(string text);

    void WriteLine(string text);

    void Write(Response response);
}
