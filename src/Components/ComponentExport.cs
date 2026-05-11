using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace Wasmtime.Components;

/// <summary>
/// Represents a known export of a component.
/// </summary>
/// <remarks>Defined at <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_export_index_t</c>.</remarks>
public class ComponentExport
    : IDisposable
{
    private readonly Handle _handle;
    private readonly Component? _owningComponent;
    private readonly string[]? _pathSegments;

    internal bool IsInstanceBound { get; }

    internal Component? OwningComponent => _owningComponent;

    internal string[]? PathSegments => _pathSegments;

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

    internal ComponentExport(IntPtr handle, bool isInstanceBound = false, string[]? pathSegments = null, Component? owningComponent = null)
    {
        _handle = new Handle(handle);
        IsInstanceBound = isInstanceBound;
        _pathSegments = pathSegments;
        _owningComponent = owningComponent;
    }

    /// <summary>
    /// Creates an owned copy of this export index.
    /// </summary>
    /// <returns>The cloned export index.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the native export index cannot be cloned.</exception>
    public ComponentExport Clone()
    {
        IntPtr clone = Native.wasmtime_component_export_index_clone(NativeHandle.DangerousGetHandle());
        if (clone == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to clone the component export index.");
        }

        return new ComponentExport(clone, IsInstanceBound, _pathSegments is null ? null : (string[])_pathSegments.Clone(), _owningComponent?.Clone());
    }

    /// <inheritdoc/>
    /// <remarks>Calls <c>include/wasmtime/component/component.h</c> <c>wasmtime_component_export_index_delete</c>.</remarks>
    public void Dispose()
    {
        _owningComponent?.Dispose();
        _handle.Dispose();
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
            Native.wasmtime_component_export_index_delete(handle);
            return true;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "IdentifierTypo")]
    internal static class Native
    {
        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_export_index_clone(IntPtr /* const wasmtime_component_export_index_t* */ export_index);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_export_index_delete(IntPtr /* wasmtime_component_export_index_t* */ export_index);
    }
}