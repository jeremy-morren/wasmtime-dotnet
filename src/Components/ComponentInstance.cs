using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace Wasmtime.Components;

/// <summary>
/// Represents an instantiated component.
/// </summary>
/// <remarks>Defined at <c>include/wasmtime/component/instance.h</c> <c>wasmtime_component_instance_t</c>.</remarks>
public class ComponentInstance
{
    private ComponentNative.Instance _instance;

    internal ComponentInstance(ComponentNative.Instance instance)
    {
        _instance = instance;
    }

    /// <summary>
    /// Looks up an exported item by name within this component instance.
    /// </summary>
    /// <param name="store">The store that owns this instance.</param>
    /// <param name="name">The export name.</param>
    /// <returns>The export index if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> or <paramref name="name"/> is null.</exception>
    public ComponentExport? GetExport(Store store, string name)
    {
        return GetExport(store, name, null);
    }

    /// <summary>
    /// Looks up an exported item by name within this component instance or a nested exported instance.
    /// </summary>
    /// <param name="store">The store that owns this instance.</param>
    /// <param name="name">The export name.</param>
    /// <param name="instanceExportIndex">An optional containing instance export.</param>
    /// <returns>The export index if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> or <paramref name="name"/> is null.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/instance.h</c> <c>wasmtime_component_instance_get_export_index</c>.</remarks>
    public ComponentExport? GetExport(Store store, string name, ComponentExport? instanceExportIndex)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        var resolvedContainingExport = ResolveInstanceExport(store, instanceExportIndex);

        using var nameBytes = name.ToUTF8(stackalloc byte[name.GetUtf8StackallocSize()]);

        try
        {
            unsafe
            {
                fixed (byte* namePointer = nameBytes.Span)
                {
                    var containingInstanceExport = resolvedContainingExport is null
                        ? IntPtr.Zero
                        : resolvedContainingExport.NativeHandle.DangerousGetHandle();
                    var exportIndex = Native.wasmtime_component_instance_get_export_index(
                        ref _instance,
                        store.Context.handle,
                        containingInstanceExport,
                        namePointer,
                        (nuint)nameBytes.Length);
                    return exportIndex == IntPtr.Zero
                        ? null
                        : new ComponentExport(exportIndex, isInstanceBound: true, pathSegments: ComponentExportPath.Combine(instanceExportIndex, name));
                }
            }
        }
        finally
        {
            if (!ReferenceEquals(resolvedContainingExport, instanceExportIndex))
            {
                resolvedContainingExport?.Dispose();
            }
        }
    }

    /// <summary>
    /// Looks up an exported function by name within this component instance.
    /// </summary>
    /// <param name="store">The store that owns this instance.</param>
    /// <param name="name">The function name.</param>
    /// <returns>The function if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> or <paramref name="name"/> is null.</exception>
    public ComponentFunction? GetFunction(Store store, string name)
    {
        return GetFunction(store, name, null);
    }

    /// <summary>
    /// Looks up an exported function by name within this component instance or a nested exported instance.
    /// </summary>
    /// <param name="store">The store that owns this instance.</param>
    /// <param name="name">The function name.</param>
    /// <param name="instanceExportIndex">An optional containing instance export.</param>
    /// <returns>The function if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> or <paramref name="name"/> is null.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/instance.h</c> <c>wasmtime_component_instance_get_export_index</c> and <c>wasmtime_component_instance_get_func</c>.</remarks>
    public ComponentFunction? GetFunction(Store store, string name, ComponentExport? instanceExportIndex)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        using var exportIndex = GetExport(store, name, instanceExportIndex);
        return exportIndex is null ? null : GetFunction(store, exportIndex);
    }

    /// <summary>
    /// Looks up an exported function by a previously resolved export index.
    /// </summary>
    /// <param name="store">The store that owns this instance.</param>
    /// <param name="exportIndex">The export index of the function.</param>
    /// <returns>The function if found; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> or <paramref name="exportIndex"/> is null.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/instance.h</c> <c>wasmtime_component_instance_get_func</c>.</remarks>
    public ComponentFunction? GetFunction(Store store, ComponentExport exportIndex)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        if (exportIndex is null)
        {
            throw new ArgumentNullException(nameof(exportIndex));
        }

        var resolvedExportIndex = ResolveInstanceExport(store, exportIndex);
        try
        {
            if (resolvedExportIndex is null)
            {
                return null;
            }

            if (!Native.wasmtime_component_instance_get_func(ref _instance, store.Context.handle, resolvedExportIndex.NativeHandle.DangerousGetHandle(), out var function))
            {
                return null;
            }

            return new ComponentFunction(function, resolvedExportIndex.Clone());
        }
        finally
        {
            if (!ReferenceEquals(resolvedExportIndex, exportIndex))
            {
                resolvedExportIndex?.Dispose();
            }
        }
    }

    private ComponentExport? ResolveInstanceExport(Store store, ComponentExport? exportIndex)
    {
        if (exportIndex is null || exportIndex.IsInstanceBound || exportIndex.PathSegments is null || exportIndex.PathSegments.Length == 0)
        {
            return exportIndex;
        }

        ComponentExport? current = null;

        try
        {
            var maxSize = exportIndex.PathSegments.Max(s => s.GetUtf8StackallocSize());
            Span<byte> segmentBytes = stackalloc byte[maxSize];
            for (var index = 0; index < exportIndex.PathSegments.Length; index++)
            {
                var segment = exportIndex.PathSegments[index];
                using var nameBytes = segment.ToUTF8(segmentBytes);

                unsafe
                {
                    fixed (byte* namePointer = nameBytes.Span)
                    {
                        var parentHandle = current is null ? IntPtr.Zero : current.NativeHandle.DangerousGetHandle();
                        var resolvedHandle = Native.wasmtime_component_instance_get_export_index(
                            ref _instance,
                            store.Context.handle,
                            parentHandle,
                            namePointer,
                            (nuint)nameBytes.Length);
                        if (resolvedHandle == IntPtr.Zero)
                        {
                            current?.Dispose();
                            return null;
                        }

                        current?.Dispose();
                        current = new ComponentExport(resolvedHandle, isInstanceBound: true,
                            pathSegments: ComponentExportPath.Take(exportIndex.PathSegments, index + 1));
                    }

                }
            }

            return current;
        }
        catch
        {
            current?.Dispose();
            throw;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "IdentifierTypo")]
    internal static class Native
    {
        [DllImport(Engine.LibraryName)]
        public static extern unsafe IntPtr wasmtime_component_instance_get_export_index(ref ComponentNative.Instance instance, IntPtr context, IntPtr instance_export_index, byte* name, nuint name_len);

        [DllImport(Engine.LibraryName)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool wasmtime_component_instance_get_func(ref ComponentNative.Instance instance, IntPtr context, IntPtr export_index, out ComponentNative.Function func_out);
    }
}

internal static class ComponentExportPath
{
    public static string[] Combine(ComponentExport? parent, string name)
    {
        if (parent?.PathSegments is null || parent.PathSegments.Length == 0)
        {
            return new[] { name };
        }

        var result = new string[parent.PathSegments.Length + 1];
        Array.Copy(parent.PathSegments, result, parent.PathSegments.Length);
        result[^1] = name;
        return result;
    }

    public static string[] Take(string[] pathSegments, int length)
    {
        var result = new string[length];
        Array.Copy(pathSegments, result, length);
        return result;
    }
}