namespace Wasmtime.Components;

/// <summary>
/// Represents a host-defined component resource value.
/// </summary>
/// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>wasmtime_component_resource_host_t</c>.</remarks>
public readonly struct ComponentResource
{
    /// <summary>
    /// Creates a host-defined component resource value.
    /// </summary>
    /// <param name="isOwned">Whether the resource is an <c>own</c> or a <c>borrow</c> in the component model.</param>
    /// <param name="representation">The host-defined 32-bit resource representation.</param>
    /// <param name="type">The host-defined 32-bit resource type identifier.</param>
    public ComponentResource(bool isOwned, uint representation, uint type)
    {
        IsOwned = isOwned;
        Representation = representation;
        Type = type;
    }

    /// <summary>
    /// Gets whether this resource is an <c>own</c> or a <c>borrow</c> in the component model.
    /// </summary>
    public bool IsOwned { get; }

    /// <summary>
    /// Gets the host-defined 32-bit resource representation.
    /// </summary>
    public uint Representation { get; }

    /// <summary>
    /// Gets the host-defined 32-bit resource type identifier.
    /// </summary>
    public uint Type { get; }
}