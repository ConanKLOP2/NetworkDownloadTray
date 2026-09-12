using NetworkDownloadTray.Services;

namespace NetworkDownloadTray.Tests;

public sealed class DownloadSpeedCalculatorTests
{
    [Fact]
    public void ConvertsBytesPerSecondToMegabits()
    {
        double result = DownloadSpeedCalculator.BytesPerSecondToMegabits(1_000_000, 1);
        Assert.Equal(8, result, precision: 8);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(100, 0)]
    public void InvalidInputReturnsZero(long bytes, double seconds)
    {
        Assert.Equal(0, DownloadSpeedCalculator.BytesPerSecondToMegabits(bytes, seconds));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(302.49, "302")]
    [InlineData(302.5, "303")]
    public void FormatsWithoutDecimals(double value, string expected)
    {
        Assert.Equal(expected, DownloadSpeedCalculator.FormatMegabits(value));
    }
}
