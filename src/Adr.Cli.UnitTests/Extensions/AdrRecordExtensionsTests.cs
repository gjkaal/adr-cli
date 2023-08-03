using System;
using System.Linq;
using Xunit;

namespace Adr.Cli.Extensions;
public class AdrRecordExtensionsTests
{
    private static class TestSet
    {
        public const string MarkdownFile1 = 
            "# 00003. Use testable database\r\n\r\n05-07-2023\r\n" +
            "## Supersedes: 2. Use testable database\r\n\r\n" + 
            "## Supersedes file: 00002-use-testable-database\r\n\r\n\r\n" + 
            "## Status\r\n\r\n__Proposed__\r\n\r\n" +
            "Explanation for [Use SQLite](00002-Use-SQLite.md)\r\n\r\n" + 
            "## Context\r\n\r\nEntity framework is not testable for pure SQL Server models\r\n\r\n" + 
            "## Decision\r\n\r\nSQLite should be used as the base line to enable integrations tests\r\n\r\n" + 
            "## Consequences\r\n\r\nDescribe consequences here\r\n";
    }

    [Fact]
    public void Extension_CanUpdateAdrFromMarkdown()
    {
        var adr = new AdrRecord() {
            RecordId = 6,
            DateTime = new System.DateTime(2022, 10, 10)
        };

        var lines = TestSet.MarkdownFile1.Split(Environment.NewLine).Select(s => s.Trim()).ToArray();

        adr.UpdateFromMarkdown(8, lines, out var modified);

        Assert.Equal(8, adr.RecordId);
        Assert.Equal(2022, adr.DateTime.Year);
        Assert.Equal(10, adr.DateTime.Month);
        Assert.Equal(10, adr.DateTime.Day);

        Assert.True(modified);
        Assert.Equal(AdrStatus.Proposed, adr.Status);
        Assert.Equal("Use testable database", adr.Title);
        Assert.Equal("Entity framework is not testable for pure SQL Server models", adr.Context);
        Assert.Equal("SQLite should be used as the base line to enable integrations tests\r\n", adr.Decision);
        Assert.Equal("Describe consequences here\r\n", adr.Consequences);
    }
}
