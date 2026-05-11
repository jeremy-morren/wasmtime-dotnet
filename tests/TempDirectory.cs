namespace Wasmtime.Tests;

internal class TempDirectory : IDisposable
{
    public TempDirectory ()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        if (Path == null) return;

        if (Directory.Exists(Path))
            Directory.Delete(Path, true);
        Path = null;
    }

    public string Path { get; private set; }
}