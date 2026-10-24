using System.IO;
using System.Linq;
using Krntija.Core.Economy;
using Xunit;

namespace Krntija.Core.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(0, "0 $")]
    [InlineData(15, "15 $")]
    [InlineData(1250, "1 250 $")]
    [InlineData(14.6, "15 $")]
    [InlineData(-25, "-25 $")]
    public void FormatsAmountWithSymbol(double amount, string expected) =>
        Assert.Equal(expected, Money.Format(amount));

    [Fact]
    public void CoreSourcesDoNotHardcodeCurrencySymbol()
    {
        string core = FindCoreSources();
        var offenders = Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains('€'))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "символ валюты пишется только в Money: " + string.Join(", ", offenders));
    }

    private static string FindCoreSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "core", "Krntija.Core");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("не найден core/Krntija.Core от " + AppContext.BaseDirectory);
    }
}
