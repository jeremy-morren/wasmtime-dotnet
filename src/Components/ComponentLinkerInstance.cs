using System;
using System.Runtime.InteropServices;

namespace Wasmtime.Components;

/// <summary>
/// Type of the callback used in <see cref="ComponentLinkerInstance.AddFunction(string, ComponentCallback)"/>.
/// </summary>
/// <param name="arguments">The component values passed to the callback.</param>
/// <returns>The component values returned by the callback.</returns>
public delegate ComponentValue[] ComponentCallback(ComponentValue[] arguments);

/// <summary>
/// Type of the callback used in <see cref="ComponentLinkerInstance.AddFunction(string, ComponentStoreCallback)"/>.
/// </summary>
/// <param name="store">The store associated with the invocation.</param>
/// <param name="arguments">The component values passed to the callback.</param>
/// <returns>The component values returned by the callback.</returns>
public delegate ComponentValue[] ComponentStoreCallback(Store store, ComponentValue[] arguments);

/// <summary>
/// Type of the callback used when a host-defined resource is dropped.
/// </summary>
/// <param name="store">The store associated with the resource drop.</param>
/// <param name="resource">The underlying resource representation.</param>
/// <remarks>Defined at <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_resource_destructor_t</c>.</remarks>
public delegate void ComponentResourceDestructor(Store store, uint resource);

/// <summary>
/// Structure representing an instance being defined within a linker.
/// </summary>
/// <remarks>
/// The methods on this type are the public building blocks for defining component-model imports,
/// including fully managed interface worlds with host-defined resources.
/// </remarks>
public sealed class ComponentLinkerInstance : IDisposable
{
    private readonly ComponentLinker _linker;
    private readonly IntPtr _handle;

    internal ComponentLinkerInstance(ComponentLinker linker, IntPtr handle)
    {
        _linker = linker ?? throw new ArgumentNullException(nameof(linker));
        _handle = handle;
    }

    /// <summary>
    /// Defines a nested instance within this instance.
    /// </summary>
    /// <param name="name">The new instance name.</param>
    /// <returns>The newly defined nested instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="WasmtimeException">Thrown when the native linker rejects the instance definition.</exception>
    /// <remarks>
    /// Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_instance_add_instance</c>.
    ///
    /// This can be used to describe arbitrarily nested levels of instances within a linker to satisfy
    /// nested instance exports of components.
    ///
    /// This acquires exclusive access to this linker instance. This instance must not be accessed by
    /// anything until the returned <see cref="ComponentLinkerInstance"/> is disposed.
    /// </remarks>
    public ComponentLinkerInstance AddInstance(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("The instance name cannot be null or empty.", nameof(name));
        }

        using var nameBytes = name.ToUTF8(stackalloc byte[name.GetUtf8StackallocSize()]);
        unsafe
        {
            fixed (byte* namePtr = nameBytes.Span)
            {
                var error = Native.wasmtime_component_linker_instance_add_instance(
                    _handle,
                    namePtr,
                    checked((nuint)nameBytes.Length),
                    out var instanceHandle);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }

                return new ComponentLinkerInstance(_linker, instanceHandle);
            }
        }
    }

    /// <summary>
    /// Defines a <see cref="Module"/> within this instance.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <param name="module">The core WebAssembly module.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="module"/> is null.</exception>
    /// <exception cref="WasmtimeException">Thrown when the native linker rejects the module definition.</exception>
    /// <remarks>
    /// Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_instance_add_module</c>.
    ///
    /// This can be used to provide a core WebAssembly module as an import to a component.
    /// The module provided is saved within the linker for the specified <paramref name="name"/>
    /// in this instance.
    /// </remarks>
    public void AddModule(string name, Module module)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("The module name cannot be null or empty.", nameof(name));
        }

        if (module is null)
        {
            throw new ArgumentNullException(nameof(module));
        }

        using var nameBytes = name.ToUTF8(stackalloc byte[name.GetUtf8StackallocSize()]);
        unsafe
        {
            fixed (byte* namePtr = nameBytes.Span)
            {
                var error = Native.wasmtime_component_linker_instance_add_module(
                    _handle,
                    namePtr,
                    checked((nuint)nameBytes.Length),
                    module.NativeHandle);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }
        }
    }

    /// <summary>
    /// Defines a function within this instance.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="callback">The callback invoked when this function is called.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is null.</exception>
    /// <exception cref="WasmtimeException">Thrown when the native linker rejects the function definition.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_instance_add_func</c>.</remarks>
    public void AddFunction(string name, ComponentCallback callback)
        => AddFunction(name, (_, arguments) => callback(arguments));

    /// <summary>
    /// Defines a function within this instance.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <param name="callback">The callback invoked when this function is called.</param>
    public void AddFunction(string name, ComponentStoreCallback callback)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("The function name cannot be null or empty.", nameof(name));
        }

        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        var registration = new CallbackRegistration(callback);
        using var nameBytes = name.ToUTF8(stackalloc byte[name.GetUtf8StackallocSize()]);
        unsafe
        {
            fixed (byte* namePtr = nameBytes.Span)
            {
                var error = Native.wasmtime_component_linker_instance_add_func(
                    _handle,
                    namePtr,
                    checked((nuint)nameBytes.Length),
                    registration.NativeCallback,
                    IntPtr.Zero,
                    IntPtr.Zero);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }
        }

        _linker.RegisterCallback(registration);
    }

    /// <summary>
    /// Defines a resource constructor within this instance.
    /// </summary>
    public void AddResourceConstructor(string resourceName, ComponentCallback callback)
        => AddFunction(GetResourceConstructorName(resourceName), callback);

    /// <summary>
    /// Defines a resource constructor within this instance.
    /// </summary>
    public void AddResourceConstructor(string resourceName, ComponentStoreCallback callback)
        => AddFunction(GetResourceConstructorName(resourceName), callback);

    /// <summary>
    /// Defines a resource static function within this instance.
    /// </summary>
    public void AddResourceStatic(string resourceName, string functionName, ComponentCallback callback)
        => AddFunction(GetResourceStaticName(resourceName, functionName), callback);

    /// <summary>
    /// Defines a resource static function within this instance.
    /// </summary>
    public void AddResourceStatic(string resourceName, string functionName, ComponentStoreCallback callback)
        => AddFunction(GetResourceStaticName(resourceName, functionName), callback);

    /// <summary>
    /// Defines a resource method within this instance.
    /// </summary>
    public void AddResourceMethod(string resourceName, string methodName, ComponentCallback callback)
        => AddFunction(GetResourceMethodName(resourceName, methodName), callback);

    /// <summary>
    /// Defines a resource method within this instance.
    /// </summary>
    public void AddResourceMethod(string resourceName, string methodName, ComponentStoreCallback callback)
        => AddFunction(GetResourceMethodName(resourceName, methodName), callback);

    /// <summary>
    /// Defines a resource type within this instance.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="resourceType">The host-defined resource type.</param>
    /// <param name="destructor">The optional callback invoked when a guest drops a resource of this type.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="resourceType"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when <paramref name="resourceType"/> has been disposed.</exception>
    /// <exception cref="WasmtimeException">Thrown when the native linker rejects the resource definition.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_instance_add_resource</c>.</remarks>
    public void AddResource(string name, ComponentResourceType resourceType, ComponentResourceDestructor? destructor = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("The resource name cannot be null or empty.", nameof(name));
        }

        if (resourceType is null)
        {
            throw new ArgumentNullException(nameof(resourceType));
        }

        var registration = destructor is null ? null : new ResourceDestructorRegistration(destructor);
        using var nameBytes = name.ToUTF8(stackalloc byte[name.GetUtf8StackallocSize()]);
        unsafe
        {
            fixed (byte* namePtr = nameBytes.Span)
            {
                var error = Native.wasmtime_component_linker_instance_add_resource(
                    _handle,
                    namePtr,
                    checked((nuint)nameBytes.Length),
                    resourceType.NativeHandle,
                    registration?.NativeDestructor,
                    IntPtr.Zero,
                    IntPtr.Zero);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }
            }
        }

        if (registration is not null)
        {
            _linker.RegisterCallback(registration);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_instance_delete</c>.</remarks>
    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            Native.wasmtime_component_linker_instance_delete(_handle);
        }
    }

    private static string GetResourceConstructorName(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName))
        {
            throw new ArgumentException("The resource name cannot be null or empty.", nameof(resourceName));
        }

        return $"[constructor]{resourceName}";
    }

    private static string GetResourceStaticName(string resourceName, string functionName)
    {
        if (string.IsNullOrEmpty(resourceName))
        {
            throw new ArgumentException("The resource name cannot be null or empty.", nameof(resourceName));
        }

        if (string.IsNullOrEmpty(functionName))
        {
            throw new ArgumentException("The resource function name cannot be null or empty.", nameof(functionName));
        }

        return $"[static]{resourceName}.{functionName}";
    }

    private static string GetResourceMethodName(string resourceName, string methodName)
    {
        if (string.IsNullOrEmpty(resourceName))
        {
            throw new ArgumentException("The resource name cannot be null or empty.", nameof(resourceName));
        }

        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentException("The resource method name cannot be null or empty.", nameof(methodName));
        }

        return $"[method]{resourceName}.{methodName}";
    }

    private static unsafe ComponentValue[] ReadArguments(IntPtr context, ComponentNative.Value* arguments, nuint argumentCount)
    {
        var managedArguments = new ComponentValue[checked((int)argumentCount)];
        for (var index = 0; index < managedArguments.Length; index++)
        {
            managedArguments[index] = ComponentValue.FromNative(context, arguments[index]);
        }

        return managedArguments;
    }

    private static unsafe void WriteResults(
        IntPtr context, ComponentNative.Value* results, nuint resultCount, ComponentValue[]? managedResults)
    {
        managedResults ??= Array.Empty<ComponentValue>();
        if (managedResults.Length != checked((int)resultCount))
        {
            throw new InvalidOperationException($"The component callback returned {managedResults.Length} results, but {resultCount} were expected.");
        }

        var builtResultCount = 0;
        var success = false;
        try
        {
            for (var index = 0; index < managedResults.Length; index++)
            {
                results[index] = managedResults[index].ToNative(context);
                builtResultCount++;
            }

            success = true;
        }
        finally
        {
            if (!success)
            {
                for (var index = 0; index < builtResultCount; index++)
                {
                    ComponentValue.Native.wasmtime_component_val_delete(ref results[index]);
                }
            }
        }
    }

    private sealed class CallbackRegistration
    {
        private readonly ComponentStoreCallback _callback;

        public CallbackRegistration(ComponentStoreCallback callback)
        {
            _callback = callback;
            NativeCallback = Invoke;
        }

        public Native.ComponentCallback NativeCallback { get; }

        private IntPtr Invoke(IntPtr data, IntPtr context, IntPtr type, IntPtr arguments, nuint argumentCount, IntPtr results, nuint resultCount)
        {
            _ = data;
            _ = type;

            try
            {
                unsafe
                {
                    var managedArguments = ReadArguments(context, (ComponentNative.Value*)arguments, argumentCount);
                    var managedResults = _callback(new StoreContext(context).Store, managedArguments);
                    WriteResults(context, (ComponentNative.Value*)results, resultCount, managedResults);
                }
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                return Function.HandleCallbackException(ex);
            }
        }
    }

    private sealed class ResourceDestructorRegistration
    {
        private readonly ComponentResourceDestructor _destructor;

        public ResourceDestructorRegistration(ComponentResourceDestructor destructor)
        {
            _destructor = destructor;
            NativeDestructor = Invoke;
        }

        public Native.ComponentResourceDestructor NativeDestructor { get; }

        private IntPtr Invoke(IntPtr data, IntPtr context, uint resource)
        {
            _ = data;

            try
            {
                _destructor(new StoreContext(context).Store, resource);
                return IntPtr.Zero;
            }
            catch (Exception ex)
            {
                return Function.HandleCallbackException(ex);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static unsafe class Native
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ComponentCallback(IntPtr data, IntPtr context, IntPtr type, IntPtr args, nuint argCount, IntPtr results, nuint resultCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ComponentResourceDestructor(IntPtr data, IntPtr context, uint resource);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_instance_add_func(IntPtr linker_instance, byte* name, nuint name_len, ComponentCallback callback, IntPtr data, IntPtr finalizer);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_instance_add_instance(IntPtr linker_instance, byte* name, nuint name_len, out IntPtr linker_instance_out);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_instance_add_module(IntPtr linker_instance, byte* name, nuint name_len, Module.Handle module);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_instance_add_resource(IntPtr linker_instance, byte* name, nuint name_len, ComponentResourceType.Handle resource_type, ComponentResourceDestructor? destructor, IntPtr data, IntPtr finalizer);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_linker_instance_delete(IntPtr linker_instance);
    }
}