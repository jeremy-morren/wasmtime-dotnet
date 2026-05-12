# Component Async Handoff

This file records the async component work that was prototyped and then removed from the product code so it can be reintroduced later without redoing the investigation.

## Goal

The target design was:

- expose component async exports as `Task<T>`-style managed APIs
- support async component instantiation
- support cooperative cancellation for a specific guest execution
- keep as much async machinery in native code as possible to avoid repeated managed/native polling and Windows threading issues

The `HttpRequest` Rust guest module under `tests/RustModules/HttpRequest` was kept in the repo and was used as the end-to-end async guest during this work.

## Wasmtime Version Findings

- Public async component C API exists in the Wasmtime versions examined here and upgrading from Wasmtime v36 to v44 would not by itself make .NET async component support trivial.
- Relevant C exports that were verified in the headers:
  - `wasmtime_component_func_call_async`
  - `wasmtime_component_linker_instantiate_async`
  - `wasmtime_call_future_poll`
  - `wasmtime_call_future_delete`
  - `wasmtime_context_fuel_async_yield_interval`

## Intended Managed API Shape

The managed API that was prototyped was:

- `ComponentFunction.CallAsync(Store store, int resultCount, params ComponentValue[]? arguments)`
- `ComponentFunction.CallAsync(Store store, int resultCount, CancellationToken cancellationToken, params ComponentValue[]? arguments)`
- `ComponentLinker.InstantiateAsync(Store store, Component component)`
- `ComponentLinker.InstantiateAsync(Store store, Component component, CancellationToken cancellationToken)`
- `Store.SetFuelAsyncYieldInterval(ulong interval)`

The cancellation model was explicitly cooperative, not preemptive. The plan was to document that cancellation is only observed when Wasmtime yields back to the host.

## Key ABI And Lifetime Findings

The most important discovery was that the async component C API retains host-provided pointers after the initial call returns.

- `wasmtime_component_func_call_async` retains:
  - the function handle pointer
  - the args buffer pointer
  - the results buffer pointer
- `wasmtime_component_linker_instantiate_async` retains:
  - the linker pointer
  - the component pointer
  - the output instance pointer

That means stack-pinned buffers or temporary managed allocations are unsafe for the full async lifetime.

The main Windows crash root cause was retained-pointer lifetime bugs, not symbol lookup. The fix was:

- allocate retained handles and value buffers in unmanaged memory
- zero-initialize retained unmanaged buffers before Wasmtime writes into them
- free them only after the async future completes or is cancelled

There was also a dedicated ABI validation test for component layout assumptions. The test checked the size and offsets of:

- `ComponentNative.WasmName`
- `ComponentNative.ValueVector`
- `ComponentNative.ValueVariant`
- `ComponentNative.ValueResult`
- `ComponentNative.ValueUnion`
- `ComponentNative.Value`
- `ComponentNative.RecordEntry`
- `ComponentNative.MapEntry`
- `ComponentNative.Instance`
- `ComponentNative.Function`

That ABI test passed and helped rule out struct-layout mismatch as the reason for the Windows access violation.

## Rust Host Investigation

Wasmtime Rust internals were inspected in the local `wasmtime-44.0.1` source. The important conclusion was that Rust hosts do not expose manual future polling/delete to users in the same way the C API does.

Relevant internals inspected:

- `Store::on_fiber`
- `FiberFuture`
- Windows fiber switching internals

Takeaway:

- Rust keeps the awkward async future lifecycle inside native code.
- Reproducing that behavior from .NET by manually polling from managed code was fragile, especially on Windows.
- A native shim was the better direction if async component support is retried.

## Native Shim Design

A Rust `cdylib` shim was prototyped under `src/NativeShim`.

Purpose:

- accept raw Wasmtime C function pointers from C#
- wrap the Wasmtime async future lifecycle in native Rust
- block inside native code until completion or cancellation
- avoid repeated managed/native boundary crossings during future polling

The exported shim surface was:

- `wasmtime_dotnet_initialize`
- `wasmtime_dotnet_component_func_call_async_blocking`
- `wasmtime_dotnet_component_linker_instantiate_async_blocking`

The shim tracked four status codes:

- completed
- cancelled
- wasmtime error
- shim error

## Windows Loading Strategy That Worked

Earlier attempts tried to pass a Wasmtime library path from C# into Rust so the shim could load it. That path was abandoned.

Why path handoff was not the preferred design:

- the managed side had already reached Wasmtime entry points, so the library was necessarily already loaded by the process
- re-loading by path created unnecessary complexity around path discovery, duplicate-loading concerns, and platform-specific search behavior
- the shim only needed a few stable function entry points, not an independent library-loading policy

The working Windows-only approach was:

- let .NET load `wasmtime.dll` normally
- use Win32 export lookup from the already loaded module
- pass raw function pointers into the shim during `wasmtime_dotnet_initialize`

The C# side used:

- `GetModuleHandle("wasmtime.dll")`
- `GetProcAddress(...)`

The important assumption was that by the time async component code ran, a normal Wasmtime P/Invoke had already happened somewhere in the process. At that point `wasmtime.dll` should already be present in the process module table, so resolving the existing loaded module was simpler and safer than asking Rust to locate the DLL again.

The concrete resolution flow was:

1. Trigger normal Wasmtime loading through the existing managed/native surface.
2. Call `GetModuleHandle("wasmtime.dll")` to get the module handle for the already loaded DLL.
3. Fail fast if the module handle is null, because that means the assumption about prior loading was wrong.
4. Call `GetProcAddress` for each required async export.
5. Pass those raw function pointers into the shim once via `wasmtime_dotnet_initialize`.

The exact managed helper that was prototyped looked like this in behavior:

- call `GetModuleHandle("wasmtime.dll")`
- if it returns `IntPtr.Zero`, throw an `InvalidOperationException`
- call `GetProcAddress(module, exportName)`
- if that returns `IntPtr.Zero`, throw an `InvalidOperationException`
- otherwise return the function pointer for shim initialization

This means a future implementation does not need to discover the filesystem path to `wasmtime.dll` at all on Windows if it can guarantee the DLL has already been loaded.

Fallbacks that were discussed but not used in the final working prototype:

- `Process.GetCurrentProcess().Modules`
  - plausible for inspection or diagnostics
  - less direct than `GetModuleHandle`
  - not necessary once normal Wasmtime loading had already occurred
- assembly-based path discovery from the managed package layout
  - possible, but unnecessary for the function-pointer handoff design
  - more brittle once different runtime asset layouts are involved
- passing a DLL path to Rust and letting Rust load the library itself
  - this was explored and intentionally abandoned

The function pointers passed were for:

- `wasmtime_component_func_call_async`
- `wasmtime_component_linker_instantiate_async`
- `wasmtime_call_future_poll`
- `wasmtime_call_future_delete`

This was Windows-only at the point the work was stopped. Cross-platform build and packaging was explored, but the runtime implementation itself was only considered stable on Windows.

## Cancellation Plan

The chosen cancellation story for the prototype was fuel-based async yielding.

Design intent:

- add `CancellationToken` to the managed async APIs
- support cancellation through a native cancellation flag checked during native polling
- make cancellation responsive by configuring fuel-based async yields on the specific store

Required store configuration:

- `Config.WithFuelConsumption(true)`
- async-enabled component config
- `Store.SetFuelAsyncYieldInterval(nonZeroInterval)`

How fuel and epoch interruption fit:

- Fuel async yielding was chosen for cancellation of a specific guest invocation because it naturally creates periodic yield points for the async future.
- Epoch interruption is broader and engine-driven; it is better for deadline-style interruption across stores, not as clean for per-call cancellation tied to a specific `CancellationToken`.
- Any future implementation should document clearly that cancellation is cooperative and observed at Wasmtime yield boundaries, not instant.

## Rust Guest Module For End-To-End Testing

The async guest used during this work remains in the repo at `tests/RustModules/HttpRequest`.

It was designed to prove real async behavior by making an outbound WASI HTTP request instead of simulating async locally.

Guest details:

- target: `wasm32-wasip2`
- toolchain pinned to Rust `1.95.0`
- notable dependencies:
  - `wit-bindgen = "0.57.1"`
  - `wasi = "=0.13.1"`
  - `serde`
  - `serde_json`
  - `url`

The guest exposed an async world roughly shaped like:

```wit
package wasmtime:http-request;

world api {
  export send-request: async func(method: string, url: string, body: option<string>) -> result<string, string>;
}
```

Implementation notes:

- it built an outbound HTTP request with WASI HTTP
- it used `wit_bindgen::yield_async().await`
- it serialized the response into JSON so tests could assert status/body easily

## End-To-End Test Design

The removed end-to-end test file was `tests/ComponentLinkerAsyncE2ETests.cs`.

It covered two main paths:

1. Successful async instantiate + async call
2. Cancellation of an async guest call

Test setup:

- create a `TcpListener` on loopback
- serve a simple HTTP response from a local fake server
- configure the linker with:
  - `AddWasiP2()`
  - `AddWasiHttp()`
- configure the store with:
  - inherited network access
  - WASI HTTP enabled
- load `RustModules/http_request.wasm`
- instantiate the component
- call the exported `send-request` function

The cancellation test used a delayed HTTP response and a short `CancellationTokenSource` timeout, expecting `OperationCanceledException`.

## Build And Pack Work That Was Prototyped

The removed implementation also added build and packaging support for the shim in `src/Wasmtime.csproj`.

What was added during the prototype:

- per-RID Rust shim metadata for Windows, Linux, and macOS
- `cargo build` integration for the shim
- copy-to-output integration for the host RID shim
- pack integration to place native assets under `runtimes/<rid>/native/`

Important packaging lesson:

- `TfmSpecificPackageFile` was the correct SDK-supported mechanism for native files during pack.

Important archive-extraction lesson:

- `tar -xf` worked in the current environment for `.tar.xz` archives where the earlier `xz | tar` pipeline had been problematic.

Because the async component implementation was removed, these build and pack changes were also removed from product code.

## Validated States Before Removal

Before rollback, the Windows implementation had been validated with focused tests on:

- `net9.0`
- `net48`
- `netcoreapp3.0`

The validation used focused async E2E tests against the `HttpRequest` guest.

There was also a successful `dotnet pack` validation that confirmed native Wasmtime assets and the host-built shim could be included in the package, but that pack work was part of the removed prototype.

## Why The Work Was Removed

The implementation path worked, but it introduced substantial complexity:

- new public managed APIs
- new native C# interop surface
- a Rust shim with platform-specific loading rules
- build and package integration complexity
- subtle lifetime requirements around retained async pointers

The code was removed in favor of this handoff document so the next implementation can restart from the validated findings rather than from scratch.

## Recommended Restart Plan

If this work is resumed, start in this order:

1. Restore the ABI contract test first.
2. Reintroduce a native shim rather than managed future polling.
3. Start Windows-only again using function-pointer handoff from the already loaded `wasmtime.dll`.
4. Reintroduce `InstantiateAsync` and `CallAsync` only after the shim path is stable.
5. Add fuel-based cancellation and document the cooperative semantics clearly.
6. Reuse the existing `tests/RustModules/HttpRequest` guest for end-to-end coverage.
7. Only then add cross-platform runtime loading and package integration.