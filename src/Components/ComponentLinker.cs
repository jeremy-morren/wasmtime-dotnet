using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// ReSharper disable CollectionNeverQueried.Local

namespace Wasmtime.Components;

/// <summary>
/// A type used to instantiate a <see cref="Component"/>.
/// </summary>
/// <remarks>Defined at <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_t</c>.</remarks>
public class ComponentLinker
    : IDisposable
{
    private readonly Handle _handle;
    private readonly List<object> _callbackRegistrations = new();

    /// <summary>
    /// Creates a new component linker for the specified engine.
    /// </summary>
    /// <param name="engine">The compilation environment and configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_new</c>.</remarks>
    public ComponentLinker(Engine engine)
    {
        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        _handle = new Handle(Native.wasmtime_component_linker_new(engine.NativeHandle));
    }

    internal Handle NativeHandle
    {
        get
        {
            if (_handle.IsInvalid || _handle.IsClosed)
            {
                throw new ObjectDisposedException(typeof(Module).FullName);
            }

            return _handle;
        }
    }

    internal ComponentLinker(IntPtr handle)
    {
        _handle = new Handle(handle);
    }

    /// <summary>
    /// Returns the root instance of this linker, used to define names into the root namespace.
    /// </summary>
    /// <returns>The root instance of this linker.</returns>
    /// <remarks>
    /// Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_root</c>.
    ///
    /// This acquires exclusive access to the linker. The linker must not be accessed by anything
    /// until the returned <see cref="ComponentLinkerInstance"/> is disposed.
    ///
    /// Combined with <see cref="ComponentLinkerInstance"/> and <see cref="ComponentResourceRegistry"/>,
    /// this API can be used to define fully managed component interface worlds without relying on
    /// Wasmtime's built-in WASI definitions.
    /// </remarks>
    public ComponentLinkerInstance GetRoot()
    {
        return new ComponentLinkerInstance(this, Native.wasmtime_component_linker_root(NativeHandle));
    }

    /// <summary>
    /// Configures whether this linker allows later definitions to shadow previous definitions.
    /// </summary>
    /// <param name="allow">Whether later definitions are allowed to shadow previous definitions.</param>
    /// <remarks>
    /// Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_allow_shadowing</c>.
    ///
    /// By default this setting is <see langword="false"/>.
    /// </remarks>
    public void AllowShadowing(bool allow)
    {
        Native.wasmtime_component_linker_allow_shadowing(NativeHandle, allow);
    }

    internal void RegisterCallback(object callbackRegistration)
    {
        _callbackRegistrations.Add(callbackRegistration);
    }

    /// <inheritdoc/>
    /// <remarks>Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_delete</c>.</remarks>
    public void Dispose()
    {
        _handle.Dispose();
    }

    /// <summary>
    /// Adds synchronous WASI preview2 CLI definitions to this component linker.
    /// </summary>
    /// <exception cref="WasmtimeException">Thrown when the native linker rejects the WASI definitions.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_add_wasip2</c>.</remarks>
    public void AddWasiP2()
    {
        var error = Native.wasmtime_component_linker_add_wasip2(NativeHandle);
        if (error != IntPtr.Zero)
        {
            throw WasmtimeException.FromOwnedError(error);
        }
    }

    /// <summary>
    /// Adds WASI HTTP interfaces into this linker.
    /// </summary>
    /// <exception cref="WasmtimeException">Thrown when the native linker rejects the WASI HTTP definitions.</exception>
    /// <remarks>
    /// Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_add_wasi_http</c>.
    ///
    /// This adds <c>wasi:http/types</c> and <c>wasi:http/outgoing-handler</c> to the linker.
    /// WASI must be added first via <see cref="AddWasiP2"/>.
    /// </remarks>
    public void AddWasiHttp()
    {
        var error = Native.wasmtime_component_linker_add_wasi_http(NativeHandle);
        if (error != IntPtr.Zero)
        {
            throw WasmtimeException.FromOwnedError(error);
        }
    }

    /// <summary>
    /// Instantiates a component instance in the given store.
    /// </summary>
    /// <param name="store">The store in which the instance should be created.</param>
    /// <param name="component">The component to instantiate.</param>
    /// <returns>The instantiated component instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> or <paramref name="component"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the linker or component has been disposed.</exception>
    /// <exception cref="WasmtimeException">Thrown when native instantiation fails.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/linker.h</c> <c>wasmtime_component_linker_instantiate</c>.</remarks>
    public ComponentInstance Instantiate(Store store, Component component)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        if (component is null)
        {
            throw new ArgumentNullException(nameof(component));
        }

        var error = Native.wasmtime_component_linker_instantiate(
            NativeHandle, store.Context.handle, component.NativeHandle, out var instance);
        if (error != IntPtr.Zero)
        {
            throw WasmtimeException.FromOwnedError(error);
        }

        return new ComponentInstance(instance);
    }

    internal class Handle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public Handle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            Native.wasmtime_component_linker_delete(handle);
            return true;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "IdentifierTypo")]
    internal static class Native
    {
        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_new(Engine.Handle engine);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_root(Handle linker);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_linker_allow_shadowing(Handle linker, [MarshalAs(UnmanagedType.I1)] bool allow);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_instantiate(Handle linker, IntPtr context, Component.Handle component, out ComponentNative.Instance instance_out);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_linker_delete(IntPtr /* wasmtime_component_linker_t* */ linker);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_add_wasip2(Handle linker);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_linker_add_wasi_http(Handle linker);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_linker_instance_delete(IntPtr /* wasmtime_component_linker_instance_t* */ linker_instance);

    }
}