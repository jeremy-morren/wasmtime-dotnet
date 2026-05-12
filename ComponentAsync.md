# Component Async Notes

This file is the retained handoff for component async work after removing the live async surface from the repo.

The `tests/RustModules/WaitAsync` harness is intentionally retained as a repro for the desired guest shape. It is not a supported passing scenario in the current C API wrapper, but it should stay in the repo as the concrete target shape.

## Goal

The desired feature is narrow:

- a managed host callback does real async work
- a guest imports that callback as a WIT `async func`
- the guest can truly `await` it

Anything short of that is not the target.

## Current Repo State

The repo no longer ships any managed component async API.

- `ComponentFunction.Call(...)` is synchronous
- `ComponentLinker.Instantiate(...)` is synchronous
- there is no public `AddFunctionAsync(...)`
- there is no public `CallAsync(...)`
- there is no public `InstantiateAsync(...)`
- there is no internal component async polling helper or native shim left in source

The WaitAsync Rust harness is still present specifically to model the blocked guest-awaitable import shape.

This is intentional. The removed async surface did not provide the feature above.

## Why The Previous Async Surface Was Removed

The earlier `AddFunctionAsync(...)` API was only a Task-to-sync bridge.

What it actually did:

- accepted a managed `Task<ComponentValue[]>`
- synchronously blocked on that task with `GetAwaiter().GetResult()` inside a normal component host function
- exposed a synchronous import to the component type system

What it did not do:

- define a real WIT `async func` import
- let the guest await the host callback through the component model
- provide guest-visible concurrency semantics

Keeping that surface would have mislabeled a blocking bridge as async component support.

## Wasmtime Version Findings

The repo is currently pinned to Wasmtime `36.0.9` in `Directory.Build.props`.

Important version boundaries:

- Wasmtime Rust API in `44.0.1` has the concurrent component host-function feature via `func_wrap_concurrent` and `func_new_concurrent`
- those Rust APIs are the feature that matches the product goal: host async work that can be awaitable from the guest
- released Wasmtime C API `44.0.1` does not expose the component async linker APIs that were experimented with later
- the vendored `wasmtime-dev` C header in this repo adds `wasmtime_component_linker_instantiate_async` and `wasmtime_component_linker_instance_add_func_async`
- even that dev C API still does not expose a component-level concurrent host-function API equivalent to Rust's `func_wrap_concurrent`

That leaves the repo in a hard gap:

- the Rust API has the right conceptual feature
- the C API used by this wrapper does not expose the same feature

## What Was Confirmed Not To Work

The desired guest shape was tested with a component importing:

```wit
package wasmtime:wait-async;

interface host {
  callback: async func(url: string) -> string;
}

interface api {
  wait-on-callback: func(url: string) -> string;
}

world wait-async {
  import host;
  export api;
}
```

The guest implementation used `wit_bindgen::block_on(async { host::callback(url).await })` inside a synchronous export.

Observed outcomes:

- without component-model async enabled in Wasmtime, the component fails to load because the async component feature is required
- with the async component feature enabled through the experimental C API path, instantiation still fails with `type mismatch with async`
- swapping the managed bridge to use `wasmtime_component_linker_instance_add_func_async` did not fix the mismatch

Conclusion:

- the current C API path in this repo cannot register a host import that matches a true component `async func`

## What Would Need To Exist Upstream

For this repo to support the actual goal, Wasmtime's C API would need:

- a component-level concurrent host-function registration API equivalent to Rust's `func_wrap_concurrent` or `func_new_concurrent`
- correct type matching for imported WIT `async func` signatures
- a store/accessor model that is valid across await points, analogous to Rust's concurrent component API design
- if WIT `future<T>` values are expected to cross the managed boundary directly, additional future/stream producer-consumer C APIs

Until that exists, reintroducing managed async component APIs here would just recreate the same false surface.

## Unsafe Lifetime Findings From The Experiment

The async component C APIs that were prototyped retain native pointers after the P/Invoke call returns.

Confirmed lifetime constraints:

- async component call futures retain function, argument, and result pointers until the future is deleted
- async component instantiation futures retain linker, component, and instance-output pointers until the future is deleted
- passing managed stack or by-ref storage directly into those retained async C APIs can cause Windows access violations because the memory does not live long enough

The Windows fix for that experiment was:

- copy retained handles and output buffers to unmanaged memory for the full async lifetime
- zero-initialize retained unmanaged buffers before calling the async C API

This mattered for the prototype, but it still did not solve the `async func` type-matching gap above.

## Stabilization Decisions Already Proven Out

The repo-level stabilization call from this work was correct:

- keep store-aware component callbacks and resource helper APIs
- remove unfinished async component invocation and linker APIs instead of shipping known-bad or misleading behavior

The validation command recorded when that cut was made was:

- `dotnet test .\Wasmtime.Tests.csproj -v minimal` from `tests/`

That passed on `net48` and `net9.0` at the time with only the expected `memory64` skips.

## Adjacent Component Learnings Worth Keeping

These were discovered during nearby component work and are worth retaining because future async work will likely hit the same edges.

### ABI And Release Bundle Constraints

- the managed projection of `wasmtime_component_func_t` was once the wrong size; the correct v36 layout is 24 bytes, not 16 bytes
- the bad layout caused nested component export lookups to read the wrong function index and could trigger `AccessViolationException`
- debug size and offset guards were added to catch future ABI drift early
- the released `wasmtime-v36.0.9` C API bundle is not drop-in for this repo because the wrapper depends on preview2 and network entry points that are absent there

### Linker And Resource Identity Constraints

- `ComponentLinkerInstance.AddInstance` holds exclusive access to its parent until disposed, so sibling imported instances must be defined sequentially
- holding multiple child linker instances open at once caused native access violations during linker registration
- the managed host cannot currently reuse the native Core I/O resource types installed by `AddWasiP2()`
- that makes mixed managed/native WASI HTTP composition fail with `mismatched resource types` when a managed interface returns resources later consumed through separately imported native `wasi:io/*` interfaces

### WASI Environment Flake On Windows

- cross-target `dotnet test` runs on Windows exposed a net48-only environment inheritance mismatch
- the stable local fix was to replace `wasi_config_inherit_env` with explicit `wasi_config_set_env` from `Environment.GetEnvironmentVariables()`
- no matching upstream issue was found when this was investigated

## Recommendation If Async Work Resumes

Do not restart from the removed managed bridge.

Start from this question instead:

- does the target Wasmtime C API expose a true component concurrent host-function registration surface that matches Rust's guest-awaitable semantics?

If the answer is no, the remaining work is upstream C API enablement, not more .NET wrapper experimentation.