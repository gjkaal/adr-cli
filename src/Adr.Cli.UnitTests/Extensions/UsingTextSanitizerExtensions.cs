using Xunit;

namespace Adr.Cli.Extensions;

public sealed class UsingTextSanitizerExtensions
{
    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("before — after", "before - after")]
    [InlineData("before – after", "before - after")]
    [InlineData("before ― after", "before - after")]
    [InlineData("—–―", "---")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void NormalizeDashes_ReplacesTypographicDashesWithAsciiHyphen(string? input, string? expected)
    {
        Assert.Equal(expected, input!.NormalizeDashes());
    }
}
