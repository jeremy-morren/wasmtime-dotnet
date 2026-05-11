using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Wasmtime.Components;

/// <summary>
/// Tracks host-defined component resources for a single <see cref="Store"/>.
/// </summary>
/// <remarks>
/// This type is intended to support user-defined component hosts built on top of the public
/// component model APIs, including fully managed interface worlds such as custom WASI Preview 2
/// implementations. A registry manages store-bound resource identities, parent-child lifetimes,
/// and destructor integration for host-defined resources.
/// </remarks>
public sealed class ComponentResourceRegistry
{
    private static readonly ConditionalWeakTable<Store, StoreResourceState> StoreStates = new();

    private readonly object _gate = new();
    private readonly Dictionary<ResourceKey, Entry> _entries = new();
    private readonly StoreResourceState _storeState;

    /// <summary>
    /// Creates a resource registry bound to a single <see cref="Store"/>.
    /// </summary>
    /// <param name="store">The store that owns the tracked resources.</param>
    public ComponentResourceRegistry(Store store)
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        _storeState = StoreStates.GetValue(store, static _ => new StoreResourceState());
    }

    /// <summary>
    /// Gets the store that owns the tracked resources.
    /// </summary>
    public Store Store { get; }

    internal static bool IsTracked(Store store, ComponentResource resource)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        return StoreStates.TryGetValue(store, out var state)
            && state.Contains(new ResourceKey(resource.Type, resource.Representation));
    }

    /// <summary>
    /// Creates an owned resource value tracked by this registry.
    /// </summary>
    public ComponentResource CreateOwned<T>(ComponentResourceType resourceType, T value)
        => Create(resourceType, value, isOwned: true, parent: null);

    /// <summary>
    /// Creates an owned child resource value tracked by this registry.
    /// </summary>
    public ComponentResource CreateOwned<T>(ComponentResourceType resourceType, T value, ComponentResource parent)
        => Create(resourceType, value, isOwned: true, parent);

    /// <summary>
    /// Creates a borrowed resource value tracked by this registry.
    /// </summary>
    public ComponentResource CreateBorrowed<T>(ComponentResourceType resourceType, T value)
        => Create(resourceType, value, isOwned: false, parent: null);

    /// <summary>
    /// Creates a borrowed child resource value tracked by this registry.
    /// </summary>
    public ComponentResource CreateBorrowed<T>(ComponentResourceType resourceType, T value, ComponentResource parent)
        => Create(resourceType, value, isOwned: false, parent);

    /// <summary>
    /// Gets the managed value associated with the specified resource.
    /// </summary>
    public T Get<T>(ComponentResource resource)
    {
        if (!TryGet(resource, out T? value))
        {
            throw new InvalidOperationException($"The component resource '{resource.Type}:{resource.Representation}' is not registered as '{typeof(T)}'.");
        }

        return value!;
    }

    /// <summary>
    /// Attempts to get the managed value associated with the specified resource.
    /// </summary>
    public bool TryGet<T>(ComponentResource resource, out T value)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(new ResourceKey(resource.Type, resource.Representation), out var entry) && entry.Value is T typedValue)
            {
                value = typedValue;
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Returns whether the specified resource is currently tracked by this registry.
    /// </summary>
    public bool Contains(ComponentResource resource)
    {
        lock (_gate)
        {
            return _entries.ContainsKey(new ResourceKey(resource.Type, resource.Representation));
        }
    }

    /// <summary>
    /// Releases the specified resource.
    /// </summary>
    public void Release(ComponentResource resource)
    {
        ResourceKey key;

        lock (_gate)
        {
            key = new ResourceKey(resource.Type, resource.Representation);
            if (!_entries.TryGetValue(key, out var entry))
            {
                throw new InvalidOperationException($"The component resource '{resource.Type}:{resource.Representation}' is not registered.");
            }

            if (entry.LiveChildCount != 0)
            {
                throw new InvalidOperationException($"The component resource '{resource.Type}:{resource.Representation}' still has live child resources.");
            }

            _entries.Remove(key);

            if (entry.Parent is { } parentKey && _entries.TryGetValue(parentKey, out var parentEntry))
            {
                parentEntry.LiveChildCount--;
            }
        }

        _storeState.Release(key);
    }

    /// <summary>
    /// Creates a destructor callback that releases tracked resources of the given type.
    /// </summary>
    /// <remarks>
    /// This is useful when wiring a host-defined resource into <see cref="ComponentLinkerInstance.AddResource"/>
    /// for a fully managed component interface implementation.
    /// </remarks>
    public ComponentResourceDestructor CreateDestructor(ComponentResourceType resourceType)
    {
        if (resourceType is null)
        {
            throw new ArgumentNullException(nameof(resourceType));
        }

        var resourceTypeId = resourceType.Id;
        return (store, representation) =>
        {
            if (!ReferenceEquals(store, Store))
            {
                throw new InvalidOperationException("The resource was dropped from a different store than the registry that created it.");
            }

            Release(new ComponentResource(isOwned: true, representation, resourceTypeId));
        };
    }

    private ComponentResource Create<T>(ComponentResourceType resourceType, T value, bool isOwned, ComponentResource? parent)
    {
        if (resourceType is null)
        {
            throw new ArgumentNullException(nameof(resourceType));
        }

        ResourceKey? parentKey = null;
        ResourceKey key;
        var parentIncremented = false;

        lock (_gate)
        {
            if (parent is { } parentResource)
            {
                parentKey = new ResourceKey(parentResource.Type, parentResource.Representation);
                if (!_entries.TryGetValue(parentKey.Value, out var parentEntry))
                {
                    throw new InvalidOperationException($"The parent component resource '{parentResource.Type}:{parentResource.Representation}' is not registered.");
                }

                parentEntry.LiveChildCount++;
                parentIncremented = true;
            }

            key = new ResourceKey(resourceType.Id, _storeState.AllocateRepresentation(resourceType.Id));

            try
            {
                _entries.Add(
                    key,
                    new Entry(value!, typeof(T), parentKey));
            }
            catch
            {
                if (parentIncremented &&
                    parentKey is { } rollbackParentKey &&
                    _entries.TryGetValue(rollbackParentKey, out var rollbackParentEntry))
                {
                    rollbackParentEntry.LiveChildCount--;
                }

                throw;
            }
        }

        try
        {
            _storeState.Track(key);
        }
        catch
        {
            lock (_gate)
            {
                _entries.Remove(key);

                if (parentIncremented &&
                    parentKey is { } rollbackParentKey &&
                    _entries.TryGetValue(rollbackParentKey, out var rollbackParentEntry))
                {
                    rollbackParentEntry.LiveChildCount--;
                }
            }

            throw;
        }

        return new ComponentResource(isOwned, key.Representation, resourceType.Id);
    }

    private readonly struct ResourceKey : IEquatable<ResourceKey>
    {
        public ResourceKey(uint type, uint representation)
        {
            Type = type;
            Representation = representation;
        }

        public uint Type { get; }

        public uint Representation { get; }

        public bool Equals(ResourceKey other)
            => Type == other.Type && Representation == other.Representation;

        public override bool Equals(object? obj)
            => obj is ResourceKey other && Equals(other);

        public override int GetHashCode()
            => unchecked(((int)Type * 397) ^ (int)Representation);
    }

    private sealed class Entry
    {
        public Entry(object value, Type valueType, ResourceKey? parent)
        {
            Value = value;
            ValueType = valueType;
            Parent = parent;
        }

        public object Value { get; }

        public Type ValueType { get; }

        public ResourceKey? Parent { get; }

        public int LiveChildCount { get; set; }
    }

    private sealed class StoreResourceState
    {
        private readonly object _gate = new();
        private readonly Dictionary<uint, uint> _nextRepresentations = new();
        private readonly HashSet<ResourceKey> _trackedResources = new();

        public uint AllocateRepresentation(uint resourceType)
        {
            lock (_gate)
            {
                var representation = _nextRepresentations.TryGetValue(resourceType, out var nextRepresentation)
                    ? checked(nextRepresentation + 1)
                    : 0;
                _nextRepresentations[resourceType] = representation;
                return representation;
            }
        }

        public void Track(ResourceKey key)
        {
            lock (_gate)
            {
                if (!_trackedResources.Add(key))
                {
                    throw new InvalidOperationException($"The component resource '{key.Type}:{key.Representation}' is already registered for this store.");
                }
            }
        }

        public void Release(ResourceKey key)
        {
            lock (_gate)
            {
                if (!_trackedResources.Remove(key))
                {
                    throw new InvalidOperationException($"The component resource '{key.Type}:{key.Representation}' is not registered for this store.");
                }
            }
        }

        public bool Contains(ResourceKey key)
        {
            lock (_gate)
            {
                return _trackedResources.Contains(key);
            }
        }
    }
}