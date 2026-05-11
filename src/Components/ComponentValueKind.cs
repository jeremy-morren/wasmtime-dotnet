using System.Text;

// ReSharper disable CommentTypo

namespace Wasmtime.Components;

/// <summary>
/// Represents the discriminant used in <c>wasmtime_component_val_t::kind</c>.
/// </summary>
/// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>wasmtime_component_valkind_t</c>.</remarks>
public enum ComponentValueKind : byte
{
    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a Boolean value.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="bool"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_BOOL</c>.</remarks>
    Bool = 0,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a signed 8-bit integer.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="sbyte"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_S8</c>.</remarks>
    S8 = 1,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is an unsigned 8-bit integer.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="byte"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_U8</c>.</remarks>
    U8 = 2,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a signed 16-bit integer.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="short"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_S16</c>.</remarks>
    S16 = 3,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is an unsigned 16-bit integer.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="ushort"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_U16</c>.</remarks>
    U16 = 4,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a signed 32-bit integer.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="int"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_S32</c>.</remarks>
    S32 = 5,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is an unsigned 32-bit integer.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="uint"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_U32</c>.</remarks>
    U32 = 6,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a signed 64-bit integer.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="long"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_S64</c>.</remarks>
    S64 = 7,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is an unsigned 64-bit integer.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="ulong"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_U64</c>.</remarks>
    U64 = 8,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a 32-bit floating-point number.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="float"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_F32</c>.</remarks>
    F32 = 9,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a 64-bit floating-point number.
    /// </summary>
    /// <remarks>Corresponding managed type: <see langword="double"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_F64</c>.</remarks>
    F64 = 10,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a Unicode scalar value.
    /// </summary>
    /// <remarks>Corresponding managed type: <see cref="Rune"/>, not <see langword="char"/>. Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_CHAR</c>.</remarks>
    Char = 11,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a string value encoded with the component model's UTF-8 string representation.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_STRING</c>.</remarks>
    String = 12,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a dynamically sized ordered list of values of the same element type.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_LIST</c>.</remarks>
    List = 13,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a record value with named fields, similar to a struct or object with a fixed schema.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_RECORD</c>.</remarks>
    Record = 14,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a tuple value with a fixed number of positional elements.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_TUPLE</c>.</remarks>
    Tuple = 15,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a tagged union with a named case and an optional payload value.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_VARIANT</c>.</remarks>
    Variant = 16,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is an enum value chosen from a closed set of named cases with no payload.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_ENUM</c>.</remarks>
    Enum = 17,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is an optional value that is either <c>none</c> or <c>some</c> payload.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_OPTION</c>.</remarks>
    Option = 18,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a result value that distinguishes <c>ok</c> from <c>err</c> and can optionally carry a payload.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_RESULT</c>.</remarks>
    Result = 19,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a set of named Boolean flags where each name can be independently present or absent.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_FLAGS</c>.</remarks>
    Flags = 20,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a component-model resource handle that refers to host or guest state with ownership tracking.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_RESOURCE</c>.</remarks>
    Resource = 21,

    /// <summary>
    /// Value of <c>wasmtime_component_valkind_t</c> meaning that <c>wasmtime_component_val_t</c> is a collection of key/value pairs.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>WASMTIME_COMPONENT_MAP</c>.</remarks>
    Map = 22,
}