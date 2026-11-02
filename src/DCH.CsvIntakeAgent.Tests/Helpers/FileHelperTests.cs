using System;
using DCH.CsvIntakeAgent.Helpers;
using FluentAssertions;
using Xunit;

namespace DCH.CsvIntakeAgent.Tests.Helpers;

public class FileHelperTests
{

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    public void FormatFileSize_WithBytesUnder1024_ShouldReturnBytesFormat(long bytes, string expected)
    {
        // Act
        var result = FileHelper.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1024, "1 KB")]
    [InlineData(2048, "2 KB")]
    [InlineData(5120, "5 KB")]
    [InlineData(10240, "10 KB")]
    [InlineData(1048575, "1024 KB")]
    public void FormatFileSize_WithKilobytes_ShouldReturnKBFormat(long bytes, string expected)
    {
        // Act
        var result = FileHelper.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1048576, "1 MB")]        // 1024 * 1024
    [InlineData(2097152, "2 MB")]       // 2 * 1024 * 1024
    [InlineData(10485760, "10 MB")]     // 10 * 1024 * 1024
    [InlineData(52428800, "50 MB")]     // 50 * 1024 * 1024
    [InlineData(104857600, "100 MB")]   // 100 * 1024 * 1024
    [InlineData(1073741823, "1024 MB")] // Just under 1GB
    public void FormatFileSize_WithMegabytes_ShouldReturnMBFormat(long bytes, string expected)
    {
        // Act
        var result = FileHelper.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1073741824, "1 GB")]        // 1024 * 1024 * 1024
    [InlineData(2147483648, "2 GB")]       // 2 * 1024 * 1024 * 1024
    [InlineData(10737418240, "10 GB")]     // 10 * 1024 * 1024 * 1024
    [InlineData(107374182400, "100 GB")]   // 100 * 1024 * 1024 * 1024
    [InlineData(long.MaxValue, "8589934592 GB")] // Maximum long value
    public void FormatFileSize_WithGigabytes_ShouldReturnGBFormat(long bytes, string expected)
    {
        // Act
        var result = FileHelper.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatFileSize_WithZero_ShouldReturnZeroBytes()
    {
        // Act
        var result = FileHelper.FormatFileSize(0);

        // Assert
        result.Should().Be("0 B");
    }

    [Fact]
    public void FormatFileSize_WithExactlyOneKB_ShouldReturnOneKB()
    {
        // Act
        var result = FileHelper.FormatFileSize(1024);

        // Assert
        result.Should().Be("1 KB");
    }

    [Fact]
    public void FormatFileSize_WithExactlyOneMB_ShouldReturnOneMB()
    {
        // Act
        var result = FileHelper.FormatFileSize(1048576); // 1024 * 1024

        // Assert
        result.Should().Be("1 MB");
    }

    [Fact]
    public void FormatFileSize_WithExactlyOneGB_ShouldReturnOneGB()
    {
        // Act
        var result = FileHelper.FormatFileSize(1073741824); // 1024 * 1024 * 1024

        // Assert
        result.Should().Be("1 GB");
    }

    [Theory]
    [InlineData(1025, "1 KB")]
    [InlineData(1049600, "1 MB")]     // 1024KB + 1024B
    [InlineData(1074790400, "1 GB")]  // 1GB + 1MB
    public void FormatFileSize_WithBoundaryValues_ShouldRoundCorrectly(long bytes, string expected)
    {
        // Act
        var result = FileHelper.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1536, 1.5)]      // 1.5 KB should show one decimal
    [InlineData(1587, 1.55)]     // 1.55 KB should show two decimals
    [InlineData(1589, 1.55)]     // Should round down to two decimals
    [InlineData(1591, 1.55)]     // Should round down to two decimals
    [InlineData(1596, 1.56)]     // Should round up to two decimals
    public void FormatFileSize_ShouldFormatDecimalsCorrectly(long bytes, double expectedValue)
    {
        // Act
        var result = FileHelper.FormatFileSize(bytes);

        // Assert - Extract the numeric part and compare as double to avoid culture issues
        var numericPart = result[..result.IndexOf(" KB", StringComparison.Ordinal)];
        var actualValue = double.Parse(numericPart);
        actualValue.Should().BeApproximately(expectedValue, 0.01);
        result.Should().EndWith(" KB");
    }

    [Fact]
    public void FormatFileSize_WithVeryLargeNumber_ShouldNotExceedGBFormat()
    {
        // Arrange
        const long veryLargeNumber = 5497558138880; // 5TB in bytes

        // Act
        var result = FileHelper.FormatFileSize(veryLargeNumber);

        // Assert
        result.Should().EndWith(" GB");
        result.Should().NotContain("TB");
    }

    [Theory]
    [InlineData(999, "999 B")]
    [InlineData(1000, "1000 B")]
    [InlineData(1023, "1023 B")]
    public void FormatFileSize_JustUnder1024_ShouldStayInBytes(long bytes, string expected)
    {
        // Act
        var result = FileHelper.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

}