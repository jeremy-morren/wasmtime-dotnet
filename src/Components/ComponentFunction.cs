using System;
using System.Runtime.InteropServices;

namespace Wasmtime.Components;

/// <summary>
/// Represents a Wasmtime component function.
/// </summary>
/// <remarks>Defined at <c>include/wasmtime/component/func.h</c> <c>wasmtime_component_func_t</c>.</remarks>
public class ComponentFunction
{
    private ComponentNative.Function _function;
    private readonly ComponentExport? _retainedExportIndex;

    internal ComponentFunction(ComponentNative.Function function, ComponentExport? retainedExportIndex = null)
    {
        _function = function;
        _retainedExportIndex = retainedExportIndex;
    }

    /// <summary>
    /// Invokes the component function with the given arguments and returns the results.
    /// </summary>
    /// <param name="store">The store that owns this function.</param>
    /// <param name="resultCount">The exact number of results expected from the call.</param>
    /// <param name="arguments">The arguments to pass to the function.</param>
    /// <returns>The component function results.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="resultCount"/> is negative.</exception>
    /// <exception cref="WasmtimeException">Thrown when native invocation fails.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/func.h</c> <c>wasmtime_component_func_call</c>.</remarks>
    public ComponentValue[] Call(Store store, int resultCount, params ComponentValue[]? arguments)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        Extensions.ThrowIfNegative(resultCount, nameof(resultCount));

        arguments ??= Array.Empty<ComponentValue>();

        var nativeArguments = new ComponentNative.Value[arguments.Length];
        var nativeResults = new ComponentNative.Value[resultCount];
        var builtArgumentCount = 0;

        try
        {
            for (var index = 0; index < arguments.Length; index++)
            {
                nativeArguments[index] = arguments[index].ToNative(store.Context.handle);
                builtArgumentCount++;
            }

            unsafe
            {
                fixed (ComponentNative.Value* nativeArgumentsPointer = nativeArguments)
                fixed (ComponentNative.Value* nativeResultsPointer = nativeResults)
                {
                    var error = Native.wasmtime_component_func_call(
                        ref _function,
                        store.Context.handle,
                        (IntPtr)nativeArgumentsPointer,
                        (nuint)nativeArguments.Length,
                        (IntPtr)nativeResultsPointer,
                        (nuint)nativeResults.Length);
                    if (error != IntPtr.Zero)
                    {
                        throw WasmtimeException.FromOwnedError(error);
                    }
                }
            }

            var results = new ComponentValue[nativeResults.Length];
            for (var index = 0; index < nativeResults.Length; index++)
            {
                results[index] = ComponentValue.FromNative(store.Context.handle, nativeResults[index]);
            }

            return results;
        }
        finally
        {
            for (var index = 0; index < builtArgumentCount; index++)
            {
                ComponentValue.Native.wasmtime_component_val_delete(ref nativeArguments[index]);
            }

            for (var index = 0; index < nativeResults.Length; index++)
            {
                ComponentValue.Native.wasmtime_component_val_delete(ref nativeResults[index]);
            }
        }
    }

    /// <summary>
    /// Invokes the deprecated post-return hook for this function.
    /// </summary>
    /// <param name="store">The store that owns this function.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
    /// <exception cref="WasmtimeException">Thrown when the native call reports an error.</exception>
    /// <remarks>Calls <c>include/wasmtime/component/func.h</c> <c>wasmtime_component_func_post_return</c>.</remarks>
    public void PostReturn(Store store)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        var error = Native.wasmtime_component_func_post_return(ref _function, store.Context.handle);
        if (error != IntPtr.Zero)
        {
            throw WasmtimeException.FromOwnedError(error);
        }
    }

    private static class Native
    {
        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_func_call(ref ComponentNative.Function func, IntPtr context, IntPtr args, nuint args_size, IntPtr results, nuint results_size);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_func_post_return(ref ComponentNative.Function func, IntPtr context);
    }
}