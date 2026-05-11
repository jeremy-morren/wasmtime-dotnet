using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Wasmtime.Components;

/// <summary>
/// Representation of a component in the component model. 
/// </summary>
/// <remarks>Defined at <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_t</c>.</remarks>
public class Component
    : IDisposable
{
    private readonly Handle _handle;

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

    internal Component(IntPtr handle)
    {
        _handle = new Handle(handle);
    }

    internal Component Clone()
    {
        return new Component(Native.wasmtime_component_clone(NativeHandle));
    }

    /// <inheritdoc/>
    /// <remarks>Calls <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_delete</c>.</remarks>
    public void Dispose()
    {
        _handle.Dispose();
    }

    /// <summary>
    /// Creates a <see cref="Component"/> given bytes.
    /// </summary>
    /// <param name="engine">The engine to use for the Component.</param>
    /// <param name="bytes">The bytes of the Component.</param>
    /// <returns>Returns a new <see cref="Component"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <exception cref="WasmtimeException">Thrown when the bytes are not a valid component.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_new</c>.</remarks>
    public static Component FromBytes(Engine engine, ReadOnlySpan<byte> bytes)
    {
        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        unsafe
        {
            fixed (byte* ptr = bytes)
            {
                var error = Native.wasmtime_component_new(engine.NativeHandle, ptr, (UIntPtr)bytes.Length, out var handle);
                if (error != IntPtr.Zero)
                {
                    throw new WasmtimeException($"WebAssembly component is not valid: {WasmtimeException.FromOwnedError(error).Message}");
                }

                return new Component(handle);
            }
        }
    }

    /// <summary>
    /// Creates a <see cref="Component"/> from a WebAssembly text format representation.
    /// </summary>
    /// <param name="engine">The engine to use for the component.</param>
    /// <param name="name">The name of the component.</param>
    /// <param name="text">The WebAssembly text format representation of the component.</param>
    /// <returns>Returns a new <see cref="Component"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/>, <paramref name="name"/>, or <paramref name="text"/> is null.</exception>
    public static Component FromText(Engine engine, string name, string text)
    {
        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        return FromBytes(engine, Module.Wat2Wasm(text));
    }

    /// <summary>
    /// Creates a <see cref="Component"/> from a WebAssembly text format file.
    /// </summary>
    /// <param name="engine">The engine to use for the component.</param>
    /// <param name="path">The path to the WebAssembly text format file.</param>
    /// <returns>Returns a new <see cref="Component"/>.</returns>
    public static Component FromTextFile(Engine engine, string path)
    {
        return FromText(engine, Path.GetFileNameWithoutExtension(path), File.ReadAllText(path));
    }

    /// <summary>
    /// Creates a <see cref="Component"/> from a WebAssembly text format stream.
    /// </summary>
    /// <param name="engine">The engine to use for the component.</param>
    /// <param name="name">The name of the component.</param>
    /// <param name="stream">The stream of the component data.</param>
    /// <returns>Returns a new <see cref="Component"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="stream"/> is null.</exception>
    public static Component FromTextStream(Engine engine, string name, Stream stream)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
        return FromText(engine, name, reader.ReadToEnd());
    }

    /// <summary>
    /// This function serializes compiled component artifacts as blob data. 
    /// </summary>
    /// <returns>If the conversion is successful, the serialized compiled component.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this component has been disposed.</exception>
    /// <exception cref="WasmtimeException">Thrown when native serialization fails.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_serialize</c>.</remarks>
    public byte[] Serialize()
    {
        var error = Native.wasmtime_component_serialize(NativeHandle, out var bytes);
        if (error != IntPtr.Zero)
        {
            throw WasmtimeException.FromOwnedError(error);
        }

        using (bytes)
            return bytes.ToArray();
    }

    /// <summary>
    /// Deserializes a previously serialized component from a span of bytes.
    /// </summary>
    /// <param name="engine">The engine to use to deserialize the component.</param>
    /// <param name="bytes">The previously serialized component bytes.</param>
    /// <returns>Returns the <see cref="Component" /> that was previously serialized.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <exception cref="WasmtimeException">Thrown when native deserialization fails.</exception>
    /// <remarks>
    /// Calls <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_deserialize</c>.
    ///
    /// The passed bytes must come from a previous call to <see cref="Component.Serialize" />.
    /// </remarks>
    public static Component Deserialize(Engine engine, ReadOnlySpan<byte> bytes)
    {
        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        unsafe
        {
            fixed (byte* ptr = bytes)
            {
                var error = Native.wasmtime_component_deserialize(engine.NativeHandle, ptr, (UIntPtr)bytes.Length, out var handle);
                if (error != IntPtr.Zero)
                {
                    throw WasmtimeException.FromOwnedError(error);
                }

                return new Component(handle);
            }
        }
    }

    /// <summary>
    /// Deserializes a previously serialized component from a file.
    /// </summary>
    /// <param name="engine">The engine to deserialize the component with.</param>
    /// <param name="path">The path to the previously serialized component.</param>
    /// <returns>Returns the <see cref="Component" /> that was previously serialized.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is null.</exception>
    /// <exception cref="WasmtimeException">Thrown when native deserialization fails.</exception>
    /// <remarks>
    /// Calls <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_deserialize_file</c>.
    ///
    /// The file's contents must come from a previous call to <see cref="Component.Serialize" />.
    /// </remarks>
    public static Component DeserializeFile(Engine engine, string path)
    {
        if (engine is null)
        {
            throw new ArgumentNullException(nameof(engine));
        }

        var error = Native.wasmtime_component_deserialize_file(engine.NativeHandle, path, out var handle);
        if (error != IntPtr.Zero)
        {
            throw WasmtimeException.FromOwnedError(error);
        }

        return new Component(handle);
    }

    /// <summary>
    /// Looks up an export of this component by name, optionally within a nested instance export.
    /// </summary>
    /// <param name="name">The export name.</param>
    /// <param name="instanceExportIndex">An optional containing instance export.</param>
    /// <returns>The export index if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this component has been disposed.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_get_export_index</c>.</remarks>
    public ComponentExport? GetExport(string name, ComponentExport? instanceExportIndex = null)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        using var nameBytes = name.ToUTF8(stackalloc byte[name.GetUtf8StackallocSize()]);

        unsafe
        {
            fixed (byte* namePointer = nameBytes.Span)
            {
                var containingInstanceExport = instanceExportIndex is null ? IntPtr.Zero : instanceExportIndex.NativeHandle.DangerousGetHandle();
                var ret = Native.wasmtime_component_get_export_index(NativeHandle, containingInstanceExport, namePointer, (nuint)nameBytes.Length);
                if (ret == IntPtr.Zero)
                {
                    return null;
                }

                return new ComponentExport(ret, pathSegments: ComponentExportPath.Combine(instanceExportIndex, name), owningComponent: Clone());
            }
        }
    }

    internal class Handle
        : SafeHandleZeroOrMinusOneIsInvalid
    {
        public Handle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            Native.wasmtime_component_delete(handle);
            return true;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "IdentifierTypo")]
    internal static class Native
    {
        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_clone(Handle component);

        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_new(Engine.Handle engine, byte* buf, nuint len, out IntPtr component_out);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_delete(IntPtr component);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_serialize(Handle component, out ByteArray ret);

        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_deserialize(Engine.Handle engine, byte* buf, nuint len, out IntPtr component_out);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_deserialize_file(Engine.Handle engine, [MarshalAs(Extensions.LPUTF8Str)] string path, out IntPtr component_out);

        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_get_export_index(Handle component, IntPtr instance_export_index, byte* name, nuint name_len);
    }
}