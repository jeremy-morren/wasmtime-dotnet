using Wasmtime.Components;

namespace Wasmtime.Tests
{
    /// <summary>
    /// Exercises component serialization and deserialization behavior.
    /// </summary>
    public class ComponentSerializationTests
    {
        [Fact]
        public void ItDeserializesAComponentFromAUnicodeFilePath()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var original = Component.FromText(engine, "test", "(component)");

            var bytes = original.Serialize();
            bytes.Should().NotBeNull();
            bytes.Length.Should().NotBe(0);

            var path = Path.Combine(Path.GetTempPath(), $"wasmtime-dotnet-こんにちは-{Guid.NewGuid():N}.cwasm");
            File.WriteAllBytes(path, bytes);

            try
            {
                using var deserialized = Component.DeserializeFile(engine, path);
                deserialized.Should().NotBeNull();
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}