using Wasmtime.Components;

namespace Wasmtime.Tests
{
    public class ComponentSharedResourceTests
    {
        [Fact]
        public void ItCanReturnARegisteredSharedResourceAcrossImportedInstances()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);
            using var root = linker.GetRoot();
            using var poll = root.AddInstance("demo:poll/types@0.1.0");
            using var streams = root.AddInstance("demo:streams/ops@0.1.0");
            using var resourceType = new ComponentResourceType(101);
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_shared_resource_imports.wat"));
            var state = new SharedResourceState(store, resourceType);

            store.SetData(state);

            poll.AddResource("pollable", resourceType, (destructorStore, representation) =>
            {
                var sharedState = (SharedResourceState)destructorStore.GetData()!;
                sharedState.DropCount++;
                sharedState.Registry.Release(new ComponentResource(isOwned: true, representation, sharedState.ResourceType.Id));
            });

            streams.AddFunction("subscribe", (callbackStore, _) =>
            {
                var sharedState = (SharedResourceState)callbackStore.GetData()!;
                return new[] { ComponentValue.FromResource(sharedState.Registry.CreateOwned(sharedState.ResourceType, 123u)) };
            });

            var instance = linker.Instantiate(store, component);
            var run = instance.GetFunction(store, "run");

            run.Should().NotBeNull();

            Action act = () => run!.Call(store, 0);

            act.Should().NotThrow();
            state.DropCount.Should().Be(1);
        }

        private sealed class SharedResourceState
        {
            public SharedResourceState(Store store, ComponentResourceType resourceType)
            {
                Registry = new ComponentResourceRegistry(store);
                ResourceType = resourceType;
            }

            public int DropCount { get; set; }

            public ComponentResourceRegistry Registry { get; }

            public ComponentResourceType ResourceType { get; }
        }
    }
}