using LayerLog.Core.Data;

namespace LayerLog.Core.Tests;

public class PrintDateTests
{
    [Theory]
    [InlineData("31/10/2026", 2026, 10, 31)]
    [InlineData("1/2/2026", 2026, 2, 1)]
    [InlineData(" 05/03/2026 ", 2026, 3, 5)]
    public void Parses_day_month_year(string text, int y, int m, int d) =>
        Assert.Equal(new DateOnly(y, m, d), PrintDate.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("31/02/2026")]
    [InlineData("2026-10-31")]
    [InlineData("ahir")]
    public void Rejects_anything_else(string text) => Assert.Null(PrintDate.Parse(text));

    [Fact]
    public void Formats_with_leading_zeros() =>
        Assert.Equal("05/03/2026", PrintDate.Format(new DateOnly(2026, 3, 5)));
}
