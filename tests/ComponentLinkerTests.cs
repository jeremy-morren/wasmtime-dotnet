using Wasmtime.Components;
// ReSharper disable AccessToDisposedClosure

namespace Wasmtime.Tests
{
    /// <summary>
    /// Exercises component linker configuration, instantiation, and host callback integration.
    /// </summary>
    public class ComponentLinkerTests
    {
        [Fact]
        public void ItAddsWasiPreview2CliDefinitions()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);

            linker.AddWasiP2();
        }

        [Fact]
        public void ItAddsWasiHttpDefinitionsAfterWasiPreview2()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);

            linker.AddWasiP2();
            linker.AddWasiHttp();
        }

        [Fact]
        public void ItInitializesWasiHttpInTheStore()
        {
            using var engine = new Engine();
            using var store = new Store(engine);

            store.SetWasiConfiguration(new WasiConfiguration());
            store.SetWasiHttp();
        }

        [Fact]
        public void ItConfiguresComponentModelOptions()
        {
            using var config = new Config();

            config.WithComponentModel(true);
        }

        [Fact]
        public void ItAddsHostCallbacksToTheComponentLinkerRoot()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);
            using var root = linker.GetRoot();

            root.AddFunction("wasm-callback", arguments =>
            {
                arguments.Should().ContainSingle().Which.AsString().Should().Be("hello");
                return new[] { ComponentValue.FromInt32(1) };
            });
        }

        [Fact]
        public void ItAddsHostCallbacksToANestedComponentLinkerInstance()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);
            using var root = linker.GetRoot();
            using var handler = root.AddInstance("demo:callbacks/handler@0.1.0");
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_instance_callback.wat"));

            handler.AddFunction("wasm-callback", arguments =>
            {
                arguments.Should().ContainSingle();
                return new[] { ComponentValue.FromInt32(arguments[0].AsInt32() + 1) };
            });

            var instance = linker.Instantiate(store, component);
            var callback = instance.GetFunction(store, "call-wasm-callback");
            callback.Should().NotBeNull();

            var results = callback!.Call(store, 1, ComponentValue.FromInt32(41));

            results.Should().ContainSingle().Which.AsInt32().Should().Be(42);
        }

        [Fact]
        public void ItAddsStoreAwareHostCallbacksToANestedComponentLinkerInstance()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine, 1);
            using var root = linker.GetRoot();
            using var handler = root.AddInstance("demo:callbacks/handler@0.1.0");
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_instance_callback.wat"));

            handler.AddFunction("wasm-callback", (callbackStore, arguments) =>
            {
                callbackStore.Should().BeSameAs(store);
                callbackStore.GetData().Should().Be(1);
                callbackStore.SetData(arguments[0].AsInt32() + 1);
                return new[] { ComponentValue.FromInt32(arguments[0].AsInt32() + 1) };
            });

            var instance = linker.Instantiate(store, component);
            var callback = instance.GetFunction(store, "call-wasm-callback");
            callback.Should().NotBeNull();

            var results = callback!.Call(store, 1, ComponentValue.FromInt32(41));

            results.Should().ContainSingle().Which.AsInt32().Should().Be(42);
            store.GetData().Should().Be(42);
        }

        [Fact]
        public void ItAllowsShadowingNestedComponentLinkerInstances()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_instance_callback.wat"));

            linker.AllowShadowing(true);

            using (var firstRoot = linker.GetRoot())
            using (var firstHandler = firstRoot.AddInstance("demo:callbacks/handler@0.1.0"))
            {
                firstHandler.AddFunction("wasm-callback", _ => new[] { ComponentValue.FromInt32(-1) });
            }

            using (var secondRoot = linker.GetRoot())
            using (var secondHandler = secondRoot.AddInstance("demo:callbacks/handler@0.1.0"))
            {
                secondHandler.AddFunction("wasm-callback", arguments => new[] { ComponentValue.FromInt32(arguments[0].AsInt32() + 1) });
            }

            var instance = linker.Instantiate(store, component);
            var callback = instance.GetFunction(store, "call-wasm-callback");
            callback.Should().NotBeNull();

            var results = callback!.Call(store, 1, ComponentValue.FromInt32(41));

            results.Should().ContainSingle().Which.AsInt32().Should().Be(42);
        }

        [Fact]
        public void ItAddsCoreModulesToANestedComponentLinkerInstance()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);
            using var root = linker.GetRoot();
            using var package = root.AddInstance("x:y/z");
            using var module = Module.FromTextFile(engine, Path.Combine("Modules", "component_module_import.wat"));
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_importing_core_module.wat"));

            package.AddModule("mod", module);

            var instance = linker.Instantiate(store, component);
            var callback = instance.GetFunction(store, "call-function");
            callback.Should().NotBeNull();

            var results = callback!.Call(store, 1, ComponentValue.FromInt32(41));

            results.Should().ContainSingle().Which.AsInt32().Should().Be(42);
        }

        [Fact]
        public void ItCreatesAndClonesComponentResourceTypes()
        {
            using var resourceType = new ComponentResourceType(1);
            using var resourceTypeClone = resourceType.Clone();
            using var differentResourceType = new ComponentResourceType(2);

            resourceType.Equals(resourceTypeClone).Should().BeTrue();
            resourceType.Equals(differentResourceType).Should().BeFalse();
        }

        [Fact]
        public void ItAddsHostResourceTypesToANestedComponentLinkerInstance()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var root = linker.GetRoot();
            using var handler = root.AddInstance("demo:resources/handler@0.1.0");
            using var resourceType = new ComponentResourceType(1);

            handler.AddResource("resource", resourceType);
        }

        [Fact]
        public void ItAddsResourceConstructorsMethodsAndStaticsToANestedComponentLinkerInstance()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);
            using var root = linker.GetRoot();
            using var handler = root.AddInstance("demo:resources/handler@0.1.0");
            using var resourceType = new ComponentResourceType(1);
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_resource_imports.wat"));
            var state = new ResourceTestState(store, resourceType);

            store.SetData(state);

            handler.AddResource("resource", resourceType, (destructorStore, representation) =>
            {
                var resourceState = (ResourceTestState)destructorStore.GetData()!;
                resourceState.DropCount++;
                resourceState.Registry.Release(new ComponentResource(isOwned: true, representation, resourceState.ResourceType.Id));
            });
            handler.AddResourceConstructor("resource", (callbackStore, arguments) =>
            {
                var resourceState = (ResourceTestState)callbackStore.GetData()!;
                uint value = arguments[0].AsUInt32();
                resourceState.LastValue = value;
                return new[] { ComponentValue.FromResource(resourceState.Registry.CreateOwned(resourceState.ResourceType, value)) };
            });
            handler.AddResourceMethod("resource", "get", (callbackStore, arguments) =>
            {
                var resourceState = (ResourceTestState)callbackStore.GetData()!;
                uint value = resourceState.Registry.Get<uint>(arguments[0].AsResource());
                return new[] { ComponentValue.FromUInt32(value) };
            });
            handler.AddResourceStatic("resource", "last", (callbackStore, _) =>
            {
                var resourceState = (ResourceTestState)callbackStore.GetData()!;
                return new[] { ComponentValue.FromUInt32(resourceState.LastValue) };
            });

            var instance = linker.Instantiate(store, component);
            var run = instance.GetFunction(store, "run");
            run.Should().NotBeNull();

            var results = run!.Call(store, 1);

            results.Should().ContainSingle().Which.AsUInt32().Should().Be(246);
            state.DropCount.Should().Be(1);
            state.LastValue.Should().Be(123);
        }

        [Fact]
        public void ItValidatesAddResourceArguments()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var root = linker.GetRoot();
            using var resourceType = new ComponentResourceType(1);

            var addWithEmptyName = () => root.AddResource(string.Empty, resourceType);
            var addWithNullType = () => root.AddResource("resource", null!);

            addWithEmptyName.Should().Throw<ArgumentException>();
            addWithNullType.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ItValidatesResourceFunctionHelperArguments()
        {
            using var engine = new Engine();
            using var linker = new ComponentLinker(engine);
            using var root = linker.GetRoot();

            Action addConstructor = () => root.AddResourceConstructor(string.Empty, new ComponentCallback(_ => Array.Empty<ComponentValue>()));
            Action addStatic = () => root.AddResourceStatic("resource", string.Empty, new ComponentCallback(_ => Array.Empty<ComponentValue>()));
            Action addMethod = () => root.AddResourceMethod(string.Empty, "get", new ComponentCallback(_ => Array.Empty<ComponentValue>()));

            addConstructor.Should().Throw<ArgumentException>();
            addStatic.Should().Throw<ArgumentException>();
            addMethod.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ItResolvesNestedCommandExportsFromAComponentInstance()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_smoke.wat"));

            linker.AddWasiP2();
            linker.AddWasiHttp();

            store.SetWasiConfiguration(
                new WasiConfiguration()
                    .WithInheritedNetwork()
                    .WithIpNameLookup());
            store.SetWasiHttp();

            using var runInterface = component.GetExport("wasi:cli/run@0.2.0");
            runInterface.Should().NotBeNull();

            var instance = linker.Instantiate(store, component);
            using var runExport = instance.GetExport(store, "run", runInterface);
            runExport.Should().NotBeNull();

            var runFunction = instance.GetFunction(store, "run", runInterface);
            runFunction.Should().NotBeNull();
        }

        [Fact]
        public void ItInvokesNestedCommandExportsFromAComponentInstance()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_smoke.wat"));

            linker.AddWasiP2();
            linker.AddWasiHttp();

            store.SetWasiConfiguration(
                new WasiConfiguration()
                    .WithInheritedStandardInput()
                    .WithInheritedStandardOutput()
                    .WithInheritedStandardError()
                    .WithInheritedNetwork()
                    .WithIpNameLookup());
            store.SetWasiHttp();

            using var runInterface = component.GetExport("wasi:cli/run@0.2.0");
            runInterface.Should().NotBeNull();

            var instance = linker.Instantiate(store, component);
            var runFunction = instance.GetFunction(store, "run", runInterface);
            runFunction.Should().NotBeNull();

            var results = runFunction!.Call(store, 1);

            var result = results.Should().ContainSingle().Which;
            result.Kind.Should().Be(ComponentValueKind.Result);
            result.AsResultIsOk().Should().BeTrue();
        }

        [Fact]
        public void ItInvokesNestedCommandExportsFromAComponentLoadedFromBytes()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);

            byte[] componentBytes = Module.Wat2Wasm(File.ReadAllText(Path.Combine("Modules", "component_smoke.wat")));
            using var component = Component.FromBytes(engine, componentBytes);

            linker.AddWasiP2();
            linker.AddWasiHttp();

            store.SetWasiConfiguration(
                new WasiConfiguration()
                    .WithInheritedStandardInput()
                    .WithInheritedStandardOutput()
                    .WithInheritedStandardError()
                    .WithInheritedNetwork()
                    .WithIpNameLookup());
            store.SetWasiHttp();

            using var runInterface = component.GetExport("wasi:cli/run@0.2.0");
            runInterface.Should().NotBeNull();

            var instance = linker.Instantiate(store, component);
            var runFunction = instance.GetFunction(store, "run", runInterface);
            runFunction.Should().NotBeNull();

            var results = runFunction!.Call(store, 1);

            var result = results.Should().ContainSingle().Which;
            result.Kind.Should().Be(ComponentValueKind.Result);
            result.AsResultIsOk().Should().BeTrue();
        }

        [Fact]
        public void ItInvokesComponentHostCallbacks()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var linker = new ComponentLinker(engine);
            using var store = new Store(engine);
            using var root = linker.GetRoot();
            using var component = Component.FromTextFile(engine, Path.Combine("Modules", "component_callback.wat"));

            int invocationCount = 0;
            int? observedValue = null;

            root.AddFunction("wasm-callback", arguments =>
            {
                invocationCount++;
                observedValue = arguments[0].AsInt32();
                return new[] { ComponentValue.FromInt32(arguments[0].AsInt32() + 1) };
            });

            var instance = linker.Instantiate(store, component);
            var callback = instance.GetFunction(store, "call-wasm-callback");
            callback.Should().NotBeNull();

            var results = callback!.Call(store, 1, ComponentValue.FromInt32(41));

            invocationCount.Should().Be(1);
            observedValue.Should().Be(41);
            results.Should().ContainSingle().Which.AsInt32().Should().Be(42);
        }

        private sealed class ResourceTestState
        {
            public ResourceTestState(Store store, ComponentResourceType resourceType)
            {
                Registry = new ComponentResourceRegistry(store);
                ResourceType = resourceType;
            }

            public ComponentResourceRegistry Registry { get; }

            public ComponentResourceType ResourceType { get; }

            public uint LastValue { get; set; }

            public int DropCount { get; set; }
        }

    }
}