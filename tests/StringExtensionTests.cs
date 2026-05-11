using System.Text;

namespace Wasmtime.Tests;

public class StringExtensionTests
{
    [Fact]
    public void ItEncodesSmallStringsUsingTheUtf8StackallocPattern()
    {
        const string value = "Hello, é🙂";

        value.GetUtf8StackallocSize().Should().Be(64);
        AssertUtf8AllocationMatchesEncoding(value);
    }

    [Fact]
    public void ItEncodesMediumStringsUsingTheUtf8StackallocPattern()
    {
        var value = new string('a', 100);
        var stackallocSize = value.GetUtf8StackallocSize();

        stackallocSize.Should().BeGreaterThan(64);
        stackallocSize.Should().BeLessThan(512);
        AssertUtf8AllocationMatchesEncoding(value);
    }

    [Fact]
    public void ItEncodesLargeStringsUsingTheUtf8StackallocPattern()
    {
        var value = new string('a', 1000);

        value.GetUtf8StackallocSize().Should().Be(0);
        AssertUtf8AllocationMatchesEncoding(value);
    }

    private static void AssertUtf8AllocationMatchesEncoding(string value)
    {
        var expected = Encoding.UTF8.GetBytes(value);

        using var allocation = value.ToUTF8(stackalloc byte[value.GetUtf8StackallocSize()]);

        allocation.Length.Should().Be(expected.Length);
        allocation.Span.ToArray().Should().Equal(expected);
    }
}