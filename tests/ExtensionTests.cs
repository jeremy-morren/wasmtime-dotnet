using System.Runtime.InteropServices;
using System.Text;

namespace Wasmtime.Tests
{
    public class ExtensionTests
    {
        [Fact]
        public void ItGetsZeroUtf8BytesForAnEmptyString()
        {
            Span<byte> buffer = stackalloc byte[0];

            Encoding.UTF8.GetBytes(string.Empty, buffer).Should().Be(0);
        }

        [Fact]
        public void ItGetsUtf8BytesFromCharacterSpans()
        {
            const string value = "A\u00E9\U0001F984";
            Span<byte> buffer = stackalloc byte[Encoding.UTF8.GetByteCount(value)];

            var written = Encoding.UTF8.GetBytes(value.AsSpan(), buffer);

            written.Should().Be(Encoding.UTF8.GetByteCount(value));
            buffer[..written].ToArray().Should().Equal(Encoding.UTF8.GetBytes(value));
        }

        [Fact]
        public void ItGetsStringsFromUtf8ByteSpans()
        {
            const string value = "A\u00E9\U0001F984";
            var bytes = Encoding.UTF8.GetBytes(value);
            ReadOnlySpan<byte> readOnlyBytes = bytes;

            Encoding.UTF8.GetString(Span<byte>.Empty).Should().BeEmpty();
            Encoding.UTF8.GetString(bytes.AsSpan()).Should().Be(value);
            Encoding.UTF8.GetString(readOnlyBytes).Should().Be(value);
        }

        [Fact]
        public void ItRecognizesValueTupleTypes()
        {
            typeof(ValueTuple<int, int>).IsTupleType().Should().BeTrue();
            typeof(Tuple<int, int>).IsTupleType().Should().BeTrue();
            typeof(string).IsTupleType().Should().BeFalse();
        }

        [Fact]
        public void ItRoundTripsSingleBitPatterns()
        {
            const int bits = unchecked((int)0xBF800000);
            const int nanBits = unchecked((int)0x7FC00000);

            Extensions.Int32BitsToSingle(bits).Should().Be(-1.0f);
            Extensions.SingleToInt32Bits(-1.0f).Should().Be(bits);

            var nan = Extensions.Int32BitsToSingle(nanBits);
            float.IsNaN(nan).Should().BeTrue();
            Extensions.SingleToInt32Bits(nan).Should().Be(nanBits);
        }

        [Fact]
        public void ItDecodesUtf8StringsFromPointers()
        {
            var bytes = Encoding.UTF8.GetBytes("prefix-\u00E9-suffix");
            var ptr = Marshal.AllocHGlobal(bytes.Length);

            try
            {
                Marshal.Copy(bytes, 0, ptr, bytes.Length);

                Extensions.PtrToStringUTF8(ptr, bytes.Length).Should().Be("prefix-\u00E9-suffix");
                Extensions.PtrToStringUTF8(ptr, 6).Should().Be("prefix");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}