using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Wasmtime;

internal static class StringExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TemporaryAllocation ToUTF8(this string value, Span<byte> bytes)
    {
        return TemporaryAllocation.FromString(value, bytes);
    }

    /// <summary>
    /// Gets the size of the stackalloc array for a utf8-string.
    /// Use with <see cref="ToUTF8"/>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetUtf8StackallocSize(this string value)
    {
        const int maxSize = 64;
        var size = value.Length * 2; // UTF-8 can be up to 4 bytes per char, but most will be 1 or 2

        return size <= maxSize
            // Small enough to allow stackalloc
            ? size
            // Too large for stackalloc, ArrayPool.Rent should be used instead
            : 0;
    }
}

internal readonly ref struct TemporaryAllocation
{
    public readonly Span<byte> Span;

    private readonly byte[]? _rented;

    public int Length => Span.Length;

    private TemporaryAllocation(Span<byte> span, byte[]? rented)
    {
        Span = span;
        _rented = rented;
    }

    public static TemporaryAllocation FromString(string str, Span<byte> output)
    {
        var length = Encoding.UTF8.GetByteCount(str);

        if (length <= output.Length)
        {
            Encoding.UTF8.GetBytes(str, output);
            return new TemporaryAllocation(output[..length], null);
        }

        var rented = ArrayPool<byte>.Shared.Rent(length);
        Encoding.UTF8.GetBytes(str, rented);
        return new TemporaryAllocation(rented.AsSpan()[..length], rented);
    }

    /// <summary>
    /// Recycle rented memory
    /// </summary>
    public void Dispose()
    {
        if (_rented != null)
        {
            ArrayPool<byte>.Shared.Return(_rented);
        }
    }
}