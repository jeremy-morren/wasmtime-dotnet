using Wasmtime.Components;

namespace Wasmtime.Tests;

public class WasiHostResourceRegistrationTests
{
    [Fact]
    public void ItAddsAHostResourceToAWasiPollInstance()
    {
        using var config = new Config().WithComponentModel(true);
        using var engine = new Engine(config);
        using var linker = new ComponentLinker(engine);
        using var root = linker.GetRoot();
        using var poll = root.AddInstance("wasi:io/poll@0.2.3");
        using var resourceType = new ComponentResourceType(1);

        Action addResource = () => poll.AddResource("pollable", resourceType);

        addResource.Should().NotThrow();
    }

    [Fact]
    public void ItAddsAHostResourceToWasiHttpTypes()
    {
        using var config = new Config().WithComponentModel(true);
        using var engine = new Engine(config);
        using var linker = new ComponentLinker(engine);
        using var root = linker.GetRoot();
        using var httpTypes = root.AddInstance("wasi:http/types@0.2.0");
        using var resourceType = new ComponentResourceType(1);

        Action addResource = () => httpTypes.AddResource("fields", resourceType);

        addResource.Should().NotThrow();
    }

    [Fact]
    public void ItAddsWasiHttpResourceFunctions()
    {
        using var config = new Config().WithComponentModel(true);
        using var engine = new Engine(config);
        using var linker = new ComponentLinker(engine);
        using var root = linker.GetRoot();
        using var httpTypes = root.AddInstance("wasi:http/types@0.2.0");
        using var resourceType = new ComponentResourceType(1);

        httpTypes.AddResource("fields", resourceType);

        Action addConstructor = () => httpTypes.AddResourceConstructor("fields", _ => new[] { ComponentValue.FromResource(new ComponentResource(true, 1, resourceType.Id)) });
        Action addMethod = () => httpTypes.AddResourceMethod("fields", "clone", arguments => new[] { arguments[0] });
        Action addStatic = () => httpTypes.AddResourceStatic("fields", "from-list", _ => new[] { ComponentValue.FromResource(new ComponentResource(true, 2, resourceType.Id)) });

        addConstructor.Should().NotThrow();
        addMethod.Should().NotThrow();
        addStatic.Should().NotThrow();
    }
}