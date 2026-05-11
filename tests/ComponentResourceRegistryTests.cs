using Wasmtime.Components;

namespace Wasmtime.Tests
{
    public class ComponentResourceRegistryTests
    {
        [Fact]
        public void ItCreatesAndReadsOwnedResources()
        {
            using var engine = new Engine();
            using var store = new Store(engine);
            using var resourceType = new ComponentResourceType(11);
            var registry = new ComponentResourceRegistry(store);

            var resource = registry.CreateOwned(resourceType, "hello");

            resource.IsOwned.Should().BeTrue();
            resource.Type.Should().Be(resourceType.Id);
            registry.Contains(resource).Should().BeTrue();
            registry.Get<string>(resource).Should().Be("hello");
        }

        [Fact]
        public void ItPreventsReleasingAParentWithLiveChildren()
        {
            using var engine = new Engine();
            using var store = new Store(engine);
            using var parentType = new ComponentResourceType(21);
            using var childType = new ComponentResourceType(22);
            var registry = new ComponentResourceRegistry(store);

            var parent = registry.CreateOwned(parentType, "request");
            var child = registry.CreateBorrowed(childType, "headers", parent);

            Action releaseParent = () => registry.Release(parent);

            registry.Get<string>(child).Should().Be("headers");
            releaseParent.Should().Throw<InvalidOperationException>();

            registry.Release(child);
            releaseParent.Should().NotThrow();
        }

        [Fact]
        public void ItRejectsMismatchedResourceValueTypes()
        {
            using var engine = new Engine();
            using var store = new Store(engine);
            using var resourceType = new ComponentResourceType(31);
            var registry = new ComponentResourceRegistry(store);

            var resource = registry.CreateOwned(resourceType, 123);

            Action readWrongType = () => registry.Get<string>(resource);

            readWrongType.Should().Throw<InvalidOperationException>();
            registry.TryGet<string>(resource, out _).Should().BeFalse();
        }

        [Fact]
        public void ItCreatesStoreBoundDestructors()
        {
            using var engine = new Engine();
            using var store = new Store(engine);
            using var otherStore = new Store(engine);
            using var resourceType = new ComponentResourceType(41);
            var registry = new ComponentResourceRegistry(store);
            var resource = registry.CreateOwned(resourceType, 123u);
            var destructor = registry.CreateDestructor(resourceType);

            Action wrongStoreDrop = () => destructor(otherStore, resource.Representation);

            wrongStoreDrop.Should().Throw<InvalidOperationException>();

            destructor(store, resource.Representation);
            registry.TryGet<uint>(resource, out _).Should().BeFalse();
        }

        [Fact]
        public void ItAllocatesUniqueRepresentationsAcrossRegistriesForTheSameStore()
        {
            using var engine = new Engine();
            using var store = new Store(engine);
            using var resourceType = new ComponentResourceType(51);
            var firstRegistry = new ComponentResourceRegistry(store);
            var secondRegistry = new ComponentResourceRegistry(store);

            var first = firstRegistry.CreateOwned(resourceType, "first");
            var second = secondRegistry.CreateOwned(resourceType, "second");

            first.Representation.Should().NotBe(second.Representation);
            firstRegistry.Get<string>(first).Should().Be("first");
            secondRegistry.Get<string>(second).Should().Be("second");
        }

        [Fact]
        public void ItStopsContainingResourcesAfterRelease()
        {
            using var engine = new Engine();
            using var store = new Store(engine);
            using var resourceType = new ComponentResourceType(61);
            var registry = new ComponentResourceRegistry(store);

            var resource = registry.CreateOwned(resourceType, "hello");

            registry.Contains(resource).Should().BeTrue();

            registry.Release(resource);

            registry.Contains(resource).Should().BeFalse();
        }
    }
}