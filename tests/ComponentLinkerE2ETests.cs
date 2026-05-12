using System.Net;
using Wasmtime.Components;

namespace Wasmtime.Tests;

public class ComponentLinkerE2ETests
{
    [Theory]
    [InlineData("wasm-bytes")]
    [InlineData("wat-string")]
    public void ItAllowsCoreWasmP2Components(string loadPath)
    {
        using var tempDir = new TempDirectory();
        File.WriteAllText(Path.Combine(tempDir.Path, "alpha.txt"), "alpha");
        File.WriteAllText(Path.Combine(tempDir.Path, "beta.txt"), "beta");

        using var config = new Config().WithComponentModel(true);
        using var engine = new Engine(config);
        using var linker = new ComponentLinker(engine);
        linker.AddWasiP2();

        using var store = new Store(engine);

        store.SetWasiConfiguration(new WasiConfiguration()
            .WithArgs("cli_echo.wasm", "first", "second")
            .WithEnvironmentVariable("CLI_ECHO_TEST", "present")
            .WithInheritedNetwork()
            .WithIpNameLookup()
            .WithPreopenedDirectory(
                tempDir.Path,
                "/sandbox",
                WasiDirectoryPermissions.Read,
                WasiFilePermissions.Read));

        using var component = LoadCliEchoComponent(engine, loadPath);
        using var api = component.GetExport("wasmtime:cli-echo/api");
        var instance = linker.Instantiate(store, component);

        api.Should().NotBeNull();

        var listFiles = instance.GetFunction(store, "list-files", api);
        var getEnvVars = instance.GetFunction(store, "get-env-vars", api);
        var getArgs = instance.GetFunction(store, "get-args", api);
        var getInitialCwd = instance.GetFunction(store, "get-initial-cwd", api);
        var getCurrentTime = instance.GetFunction(store, "get-current-time", api);
        var getMonotonicTime = instance.GetFunction(store, "get-monotonic-time", api);
        var getRandomU64 = instance.GetFunction(store, "get-random-u64", api);
        var resolveAddresses = instance.GetFunction(store, "resolve-addresses", api);

        listFiles.Should().NotBeNull();
        getEnvVars.Should().NotBeNull();
        getArgs.Should().NotBeNull();
        getInitialCwd.Should().NotBeNull();
        getCurrentTime.Should().NotBeNull();
        getMonotonicTime.Should().NotBeNull();
        getRandomU64.Should().NotBeNull();
        resolveAddresses.Should().NotBeNull();

        static string DescribeArgument(ComponentValue value)
        {
            return value.Kind switch
            {
                ComponentValueKind.String => $"string:{value.AsString()}",
                ComponentValueKind.S64 => $"s64:{value.AsInt64()}",
                ComponentValueKind.U64 => $"u64:{value.AsUInt64()}",
                ComponentValueKind.Bool => $"bool:{value.AsBoolean()}",
                _ => value.Kind.ToString(),
            };
        }

        ComponentValue InvokeSingleResult(string functionName, ComponentFunction function, params ComponentValue[] arguments)
        {
            try
            {
                var results = function.Call(store, 1, arguments);
                results.Should().ContainSingle();
                function.PostReturn(store);
                return results[0];
            }
            catch (Exception ex)
            {
                var formattedArguments = arguments.Length == 0
                    ? "<none>"
                    : string.Join(", ", Array.ConvertAll(arguments, DescribeArgument));

                throw new InvalidOperationException(
                    $"Calling `wasmtime:cli-echo/api#{functionName}` failed. Argument count: {arguments.Length}. Arguments: {formattedArguments}.",
                    ex);
            }
        }

        var currentTime = InvokeSingleResult("get-current-time", getCurrentTime!).AsInt64();
        currentTime.Should().BeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds());
        currentTime.Should().BeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds());

        var firstMonotonicTime = InvokeSingleResult("get-monotonic-time", getMonotonicTime!).AsUInt64();
        var secondMonotonicTime = InvokeSingleResult("get-monotonic-time", getMonotonicTime!).AsUInt64();
        secondMonotonicTime.Should().BeGreaterThanOrEqualTo(firstMonotonicTime);

        _ = InvokeSingleResult("get-random-u64", getRandomU64!).AsUInt64();

        InvokeSingleResult("get-initial-cwd", getInitialCwd!).AsOption().Should().BeNull();

        var listedFiles = InvokeSingleResult("list-files", listFiles!, ComponentValue.FromString("/sandbox")).AsList();
        listedFiles.Should().HaveCount(2);
        listedFiles[0].AsString().Should().Be("alpha.txt");
        listedFiles[1].AsString().Should().Be("beta.txt");

        var envVars = InvokeSingleResult("get-env-vars", getEnvVars!).AsList();
        envVars.Should().Contain(value => value.AsString() == "CLI_ECHO_TEST=present");

        var args = InvokeSingleResult("get-args", getArgs!).AsList();
        args.Should().HaveCount(3);
        args[0].AsString().Should().Be("cli_echo.wasm");
        args[1].AsString().Should().Be("first");
        args[2].AsString().Should().Be("second");

        var addresses = InvokeSingleResult("resolve-addresses", resolveAddresses!, ComponentValue.FromString("localhost")).AsList();
        addresses.Should().NotBeEmpty();
        foreach (var value in addresses)
        {
            var address = value.AsString();
            address.StartsWith("error:", StringComparison.Ordinal).Should().BeFalse();
            IPAddress.TryParse(address, out _).Should().BeTrue();
        }
    }

    private static Component LoadCliEchoComponent(Engine engine, string loadPath)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var wasmPath = Path.Combine(baseDirectory, "RustModules/cli_echo.wasm");
        var watPath = Path.Combine(baseDirectory, "RustModules/cli_echo.wat");

        switch (loadPath)
        {
            case "wasm-bytes":
                return Component.FromBytes(engine, File.ReadAllBytes(wasmPath));

            case "wat-string":
                return Component.FromText(engine, "cli_echo", File.ReadAllText(watPath));

            default:
                throw new ArgumentOutOfRangeException(nameof(loadPath), loadPath, "Unknown component load path.");
        }
    }
}