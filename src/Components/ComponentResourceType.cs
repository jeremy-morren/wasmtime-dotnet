using System;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Wasmtime.Components;

/// <summary>
/// Represents a host-defined component resource type.
/// </summary>
/// <remarks>Defined at <c>include/wasmtime/component/types/resource.h</c> <c>wasmtime_component_resource_type_t</c>.</remarks>
public sealed class ComponentResourceType : IEquatable<ComponentResourceType>, IDisposable
{
    private readonly Handle handle;
    private readonly uint id;

    /// <summary>
    /// Creates a new host-defined component resource type.
    /// </summary>
    /// <param name="type">The host-defined resource type identifier.</param>
    /// <exception cref="InvalidOperationException">Thrown when the native resource type cannot be created.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/types/resource.h</c> <c>wasmtime_component_resource_type_new_host</c>.</remarks>
    public ComponentResourceType(uint type)
    {
        id = type;
        IntPtr resourceType = Native.wasmtime_component_resource_type_new_host(type);
        if (resourceType == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create the component resource type.");
        }

        handle = new Handle(resourceType);
    }

    private ComponentResourceType(IntPtr handle, uint id)
    {
        this.id = id;
        this.handle = new Handle(handle);
    }

    /// <summary>
    /// Gets the host-defined resource type identifier.
    /// </summary>
    public uint Id => id;

    internal Handle NativeHandle
    {
        get
        {
            if (handle.IsInvalid || handle.IsClosed)
            {
                throw new ObjectDisposedException(typeof(ComponentResourceType).FullName);
            }

            return handle;
        }
    }

    /// <summary>
    /// Creates an owned copy of this resource type.
    /// </summary>
    /// <returns>The cloned resource type.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when this resource type has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the native resource type cannot be cloned.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/types/resource.h</c> <c>wasmtime_component_resource_type_clone</c>.</remarks>
    public ComponentResourceType Clone()
    {
        IntPtr clone = Native.wasmtime_component_resource_type_clone(NativeHandle);
        if (clone == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to clone the component resource type.");
        }

        return new ComponentResourceType(clone, id);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when this or <paramref name="other"/> has been disposed.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/types/resource.h</c> <c>wasmtime_component_resource_type_equal</c>.</remarks>
    public bool Equals(ComponentResourceType? other)
    {
        if (other is null)
        {
            return false;
        }

        return Native.wasmtime_component_resource_type_equal(NativeHandle, other.NativeHandle);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ComponentResourceType other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return unchecked((int)id);
    }

    /// <inheritdoc/>
    /// <remarks>Calls <c>include/wasmtime/component/types/resource.h</c> <c>wasmtime_component_resource_type_delete</c>.</remarks>
    public void Dispose()
    {
        handle.Dispose();
    }

    internal sealed class Handle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public Handle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            Native.wasmtime_component_resource_type_delete(handle);
            return true;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "IdentifierTypo")]
    internal static class Native
    {
        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_resource_type_new_host(uint type);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_resource_type_clone(Handle type);

        [DllImport(Engine.LibraryName)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool wasmtime_component_resource_type_equal(Handle left, Handle right);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_resource_type_delete(IntPtr type);
    }
}