using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable IdentifierTypo

namespace Wasmtime.Components;

/// <summary>
/// Represents a managed projection of <c>wasmtime_component_val_t</c> for passing component values between .NET and the Wasmtime C API.
/// </summary>
public readonly struct ComponentValue
{
    private readonly bool _boolean;
    private readonly uint _scalar32;
    private readonly ulong _scalar64;
    private readonly float _f32;
    private readonly double _f64;
    private readonly string? _stringValue;
    private readonly bool _resultIsOk;
    private readonly ComponentResource _resource;
    private readonly object? _compoundValue;

    private ComponentValue(
        ComponentValueKind kind,
        bool boolean = false,
        uint scalar32 = default,
        ulong scalar64 = default,
        float f32 = default,
        double f64 = default,
        string? stringValue = null,
        bool resultIsOk = false,
        ComponentResource resource = default,
        object? compoundValue = null)
    {
        Kind = kind;
        _boolean = boolean;
        _scalar32 = scalar32;
        _scalar64 = scalar64;
        _f32 = f32;
        _f64 = f64;
        _stringValue = stringValue;
        _resultIsOk = resultIsOk;
        _resource = resource;
        _compoundValue = compoundValue;
    }

    /// <summary>
    /// Gets the discriminant of this component value.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/component/val.h</c> <c>wasmtime_component_val_t::kind</c>.</remarks>
    public ComponentValueKind Kind { get; }

    /// <summary>
    /// Creates a <c>bool</c> component value.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromBoolean(bool value) => new(ComponentValueKind.Bool, boolean: value);

    /// <summary>
    /// Creates an <c>s8</c> component value.
    /// </summary>
    /// <param name="value">The 8-bit signed integer value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromInt8(sbyte value) => new(ComponentValueKind.S8, scalar32: unchecked((uint)value));

    /// <summary>
    /// Creates a <c>u8</c> component value.
    /// </summary>
    /// <param name="value">The 8-bit unsigned integer value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromUInt8(byte value) => new(ComponentValueKind.U8, scalar32: value);

    /// <summary>
    /// Creates an <c>s16</c> component value.
    /// </summary>
    /// <param name="value">The 16-bit signed integer value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromInt16(short value) => new(ComponentValueKind.S16, scalar32: unchecked((uint)value));

    /// <summary>
    /// Creates a <c>u16</c> component value.
    /// </summary>
    /// <param name="value">The 16-bit unsigned integer value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromUInt16(ushort value) => new(ComponentValueKind.U16, scalar32: value);

    /// <summary>
    /// Creates an <c>s32</c> component value.
    /// </summary>
    /// <param name="value">The 32-bit signed integer value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromInt32(int value) => new(ComponentValueKind.S32, scalar32: unchecked((uint)value));

    /// <summary>
    /// Creates a <c>u32</c> component value.
    /// </summary>
    /// <param name="value">The 32-bit unsigned integer value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromUInt32(uint value) => new(ComponentValueKind.U32, scalar32: value);

    /// <summary>
    /// Creates an <c>s64</c> component value.
    /// </summary>
    /// <param name="value">The 64-bit signed integer value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromInt64(long value) => new(ComponentValueKind.S64, scalar64: unchecked((ulong)value));

    /// <summary>
    /// Creates a <c>u64</c> component value.
    /// </summary>
    /// <param name="value">The 64-bit unsigned integer value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromUInt64(ulong value) => new(ComponentValueKind.U64, scalar64: value);

    /// <summary>
    /// Creates a <c>f32</c> component value.
    /// </summary>
    /// <param name="value">The 32-bit floating-point value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromFloat32(float value) => new(ComponentValueKind.F32, f32: value);

    /// <summary>
    /// Creates a <c>f64</c> component value.
    /// </summary>
    /// <param name="value">The 64-bit floating-point value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromFloat64(double value) => new(ComponentValueKind.F64, f64: value);

    /// <summary>
    /// Creates a <c>char</c> component value from a single UTF-16 code unit.
    /// </summary>
    /// <param name="value">The UTF-16 code unit.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is a lone surrogate and therefore not a valid Unicode scalar value.</exception>
    public static ComponentValue FromChar(char value)
    {
        if (!Rune.TryCreate(value, out var rune))
        {
            throw new ArgumentException("The UTF-16 code unit must not be a lone surrogate.", nameof(value));
        }

        return FromRune(rune);
    }

    /// <summary>
    /// Creates a <c>char</c> component value from a Unicode scalar value.
    /// </summary>
    /// <param name="value">The Unicode scalar value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromRune(Rune value) => new(ComponentValueKind.Char, scalar32: checked((uint)value.Value));

    /// <summary>
    /// Creates a string component value.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public static ComponentValue FromString(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new ComponentValue(ComponentValueKind.String, stringValue: value);
    }

    /// <summary>
    /// Creates a list component value.
    /// </summary>
    /// <param name="values">The list elements.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    public static ComponentValue FromList(params ComponentValue[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        return new ComponentValue(ComponentValueKind.List, compoundValue: CopyArray(values));
    }

    /// <summary>
    /// Creates a record component value.
    /// </summary>
    /// <param name="fields">The record fields.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> is null.</exception>
    public static ComponentValue FromRecord(params ComponentRecordField[] fields)
    {
        if (fields is null)
        {
            throw new ArgumentNullException(nameof(fields));
        }

        ValidateRecordFields(fields, nameof(fields));
        return new ComponentValue(ComponentValueKind.Record, compoundValue: CopyArray(fields));
    }

    /// <summary>
    /// Creates a tuple component value.
    /// </summary>
    /// <param name="values">The tuple elements.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is null.</exception>
    public static ComponentValue FromTuple(params ComponentValue[] values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        return new ComponentValue(ComponentValueKind.Tuple, compoundValue: CopyArray(values));
    }

    /// <summary>
    /// Creates a variant component value.
    /// </summary>
    /// <param name="discriminant">The active case name.</param>
    /// <param name="value">The optional payload value.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="discriminant"/> is null.</exception>
    public static ComponentValue FromVariant(string discriminant, ComponentValue? value = null)
    {
        if (discriminant is null)
        {
            throw new ArgumentNullException(nameof(discriminant));
        }

        return new ComponentValue(ComponentValueKind.Variant, compoundValue: new VariantValue(discriminant, value));
    }

    /// <summary>
    /// Creates an enum component value.
    /// </summary>
    /// <param name="name">The selected enum case name.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public static ComponentValue FromEnum(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        return new ComponentValue(ComponentValueKind.Enum, stringValue: name);
    }

    /// <summary>
    /// Creates an option component value.
    /// </summary>
    /// <param name="value">The optional payload value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromOption(ComponentValue? value) => new(ComponentValueKind.Option, compoundValue: new OptionalValue(value));

    /// <summary>
    /// Creates a result component value without a payload.
    /// </summary>
    /// <param name="isOk">Whether the result is the <c>ok</c> discriminant.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromResult(bool isOk) => new(ComponentValueKind.Result, resultIsOk: isOk, compoundValue: new OptionalValue(null));

    /// <summary>
    /// Creates a result component value with an optional payload.
    /// </summary>
    /// <param name="isOk">Whether the result is the <c>ok</c> discriminant.</param>
    /// <param name="value">The optional payload value.</param>
    /// <returns>The component value.</returns>
    public static ComponentValue FromResult(bool isOk, ComponentValue? value) => new(ComponentValueKind.Result, resultIsOk: isOk, compoundValue: new OptionalValue(value));

    /// <summary>
    /// Creates a flags component value.
    /// </summary>
    /// <param name="names">The active flag names.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="names"/> is null.</exception>
    public static ComponentValue FromFlags(params string[] names)
    {
        if (names is null)
        {
            throw new ArgumentNullException(nameof(names));
        }

        ValidateStrings(names, nameof(names));
        return new ComponentValue(ComponentValueKind.Flags, compoundValue: CopyArray(names));
    }

    /// <summary>
    /// Creates a host-defined resource component value.
    /// </summary>
    /// <param name="resource">The resource value.</param>
    /// <returns>The component value.</returns>
    /// <remarks>
    /// Resource values that are lowered into guest code must be tracked by a <see cref="ComponentResourceRegistry"/>
    /// for the current <see cref="Store"/>.
    /// </remarks>
    public static ComponentValue FromResource(ComponentResource resource) => new(ComponentValueKind.Resource, resource: resource);

    /// <summary>
    /// Creates a map component value.
    /// </summary>
    /// <param name="entries">The map entries.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
    public static ComponentValue FromMap(params ComponentMapEntry[] entries)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        return new ComponentValue(ComponentValueKind.Map, compoundValue: CopyArray(entries));
    }

    /// <summary>
    /// Creates a component value from common CLR types.
    /// </summary>
    /// <param name="value">The CLR value to convert.</param>
    /// <returns>The component value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> cannot be represented as a component value.</exception>
    public static ComponentValue FromObject(object value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return value switch
        {
            ComponentValue componentValue => componentValue,
            bool boolean => FromBoolean(boolean),
            sbyte int8 => FromInt8(int8),
            byte uint8 => FromUInt8(uint8),
            short int16 => FromInt16(int16),
            ushort uint16 => FromUInt16(uint16),
            int int32 => FromInt32(int32),
            uint uint32 => FromUInt32(uint32),
            long int64 => FromInt64(int64),
            ulong uint64 => FromUInt64(uint64),
            float float32 => FromFloat32(float32),
            double float64 => FromFloat64(float64),
            Rune rune => FromRune(rune),
            char character => FromChar(character),
            string text => FromString(text),
            ComponentValue[] values => FromList(values),
            ComponentRecordField[] fields => FromRecord(fields),
            ComponentVariant variant => FromVariant(variant.Discriminant, variant.Value),
            ComponentResult result => FromResult(result.IsOk, result.Value),
            string[] names => FromFlags(names),
            ComponentMapEntry[] entries => FromMap(entries),
            ComponentResource resource => FromResource(resource),
            _ => throw new ArgumentException($"The CLR type '{value.GetType()}' is not supported for component values.", nameof(value)),
        };
    }

    /// <summary>
    /// Reads this component value as a <c>bool</c>.
    /// </summary>
    /// <returns>The boolean value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>bool</c>.</exception>
    public bool AsBoolean()
    {
        if (Kind != ComponentValueKind.Bool)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as bool.");
        }

        return _boolean;
    }

    /// <summary>
    /// Reads this component value as an <c>s8</c>.
    /// </summary>
    /// <returns>The 8-bit signed integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not an <c>s8</c>.</exception>
    public sbyte AsInt8()
    {
        if (Kind != ComponentValueKind.S8)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as s8.");
        }

        return unchecked((sbyte)_scalar32);
    }

    /// <summary>
    /// Reads this component value as a <c>u8</c>.
    /// </summary>
    /// <returns>The 8-bit unsigned integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>u8</c>.</exception>
    public byte AsUInt8()
    {
        if (Kind != ComponentValueKind.U8)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as u8.");
        }

        return unchecked((byte)_scalar32);
    }

    /// <summary>
    /// Reads this component value as an <c>s16</c>.
    /// </summary>
    /// <returns>The 16-bit signed integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not an <c>s16</c>.</exception>
    public short AsInt16()
    {
        if (Kind != ComponentValueKind.S16)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as s16.");
        }

        return unchecked((short)_scalar32);
    }

    /// <summary>
    /// Reads this component value as a <c>u16</c>.
    /// </summary>
    /// <returns>The 16-bit unsigned integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>u16</c>.</exception>
    public ushort AsUInt16()
    {
        if (Kind != ComponentValueKind.U16)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as u16.");
        }

        return unchecked((ushort)_scalar32);
    }

    /// <summary>
    /// Reads this component value as an <c>s32</c>.
    /// </summary>
    /// <returns>The 32-bit signed integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not an <c>s32</c>.</exception>
    public int AsInt32()
    {
        if (Kind != ComponentValueKind.S32)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as s32.");
        }

        return unchecked((int)_scalar32);
    }

    /// <summary>
    /// Reads this component value as a <c>u32</c>.
    /// </summary>
    /// <returns>The 32-bit unsigned integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>u32</c>.</exception>
    public uint AsUInt32()
    {
        if (Kind != ComponentValueKind.U32)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as u32.");
        }

        return _scalar32;
    }

    /// <summary>
    /// Reads this component value as an <c>s64</c>.
    /// </summary>
    /// <returns>The 64-bit signed integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not an <c>s64</c>.</exception>
    public long AsInt64()
    {
        if (Kind != ComponentValueKind.S64)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as s64.");
        }

        return unchecked((long)_scalar64);
    }

    /// <summary>
    /// Reads this component value as a <c>u64</c>.
    /// </summary>
    /// <returns>The 64-bit unsigned integer value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>u64</c>.</exception>
    public ulong AsUInt64()
    {
        if (Kind != ComponentValueKind.U64)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as u64.");
        }

        return _scalar64;
    }

    /// <summary>
    /// Reads this component value as a <c>f32</c>.
    /// </summary>
    /// <returns>The 32-bit floating-point value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>f32</c>.</exception>
    public float AsFloat32()
    {
        if (Kind != ComponentValueKind.F32)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as f32.");
        }

        return _f32;
    }

    /// <summary>
    /// Reads this component value as a <c>f64</c>.
    /// </summary>
    /// <returns>The 64-bit floating-point value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>f64</c>.</exception>
    public double AsFloat64()
    {
        if (Kind != ComponentValueKind.F64)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as f64.");
        }

        return _f64;
    }

    /// <summary>
    /// Reads this component value as a Unicode scalar value.
    /// </summary>
    /// <returns>The Unicode scalar value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>char</c>.</exception>
    public Rune AsRune()
    {
        if (Kind != ComponentValueKind.Char)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as char.");
        }

        return new Rune(checked((int)_scalar32));
    }

    /// <summary>
    /// Reads this component value as a single UTF-16 <see langword="char"/> when possible.
    /// </summary>
    /// <returns>The UTF-16 code unit.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a <c>char</c>.</exception>
    /// <exception cref="OverflowException">Thrown when the Unicode scalar value requires a surrogate pair in UTF-16 and therefore cannot be represented as a single <see langword="char"/>.</exception>
    /// <remarks>This can throw for valid component-model characters outside the Basic Multilingual Plane because .NET <see langword="char"/> stores only one UTF-16 code unit.</remarks>
    public char AsChar()
    {
        var rune = AsRune();
        return checked((char)rune.Value);
    }

    /// <summary>
    /// Reads this component value as a list.
    /// </summary>
    /// <returns>The list elements.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a list.</exception>
    public ComponentValue[] AsList()
    {
        if (Kind != ComponentValueKind.List)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as list.");
        }

        return CopyArray((ComponentValue[]?)_compoundValue ?? Array.Empty<ComponentValue>());
    }

    /// <summary>
    /// Reads this component value as a record.
    /// </summary>
    /// <returns>The record fields.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a record.</exception>
    public ComponentRecordField[] AsRecord()
    {
        if (Kind != ComponentValueKind.Record)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as record.");
        }

        return CopyArray((ComponentRecordField[]?)_compoundValue ?? Array.Empty<ComponentRecordField>());
    }

    /// <summary>
    /// Reads this component value as a tuple.
    /// </summary>
    /// <returns>The tuple elements.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a tuple.</exception>
    public ComponentValue[] AsTuple()
    {
        if (Kind != ComponentValueKind.Tuple)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as tuple.");
        }

        return CopyArray((ComponentValue[]?)_compoundValue ?? Array.Empty<ComponentValue>());
    }

    /// <summary>
    /// Reads this component value as a variant.
    /// </summary>
    /// <returns>The variant discriminant and optional payload.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a variant.</exception>
    public ComponentVariant AsVariant()
    {
        if (Kind != ComponentValueKind.Variant)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as variant.");
        }

        var variant = (VariantValue?)_compoundValue ?? throw new InvalidOperationException("The variant value is not initialized.");
        return new ComponentVariant(variant.Discriminant, variant.Payload.HasValue ? variant.Payload.Value : null);
    }

    /// <summary>
    /// Reads this component value as an enum case name.
    /// </summary>
    /// <returns>The enum case name.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not an enum.</exception>
    public string AsEnum()
    {
        if (Kind != ComponentValueKind.Enum)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as enum.");
        }

        return _stringValue ?? string.Empty;
    }

    /// <summary>
    /// Reads this component value as an option payload.
    /// </summary>
    /// <returns>The payload value, or <see langword="null"/> when the option is <c>none</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not an option.</exception>
    public ComponentValue? AsOption()
    {
        if (Kind != ComponentValueKind.Option)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as option.");
        }

        var option = (OptionalValue?)_compoundValue ?? new OptionalValue(null);
        return option.HasValue ? option.Value : null;
    }

    /// <summary>
    /// Reads this component value as a string.
    /// </summary>
    /// <returns>The string value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a string.</exception>
    public string AsString()
    {
        if (Kind != ComponentValueKind.String)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as string.");
        }

        return _stringValue ?? string.Empty;
    }

    /// <summary>
    /// Reads this component value as a payload-free result discriminant.
    /// </summary>
    /// <returns><see langword="true"/> when the result is <c>ok</c>; otherwise <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a result.</exception>
    public bool AsResultIsOk()
    {
        if (Kind != ComponentValueKind.Result)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as result.");
        }

        return _resultIsOk;
    }

    /// <summary>
    /// Reads this component value as a result discriminant and optional payload.
    /// </summary>
    /// <returns>The result discriminant and optional payload.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a result.</exception>
    public ComponentResult AsResult()
    {
        if (Kind != ComponentValueKind.Result)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as result.");
        }

        var payload = (OptionalValue?)_compoundValue ?? new OptionalValue(null);
        return new ComponentResult(_resultIsOk, payload.HasValue ? payload.Value : null);
    }

    /// <summary>
    /// Reads this component value as a flags value.
    /// </summary>
    /// <returns>The active flag names.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a flags value.</exception>
    public string[] AsFlags()
    {
        if (Kind != ComponentValueKind.Flags)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as flags.");
        }

        return CopyArray((string[]?)_compoundValue ?? Array.Empty<string>());
    }

    /// <summary>
    /// Reads this component value as a host-defined resource.
    /// </summary>
    /// <returns>The resource value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a resource.</exception>
    public ComponentResource AsResource()
    {
        if (Kind != ComponentValueKind.Resource)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as resource.");
        }

        return _resource;
    }

    /// <summary>
    /// Reads this component value as a map.
    /// </summary>
    /// <returns>The map entries.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this value is not a map.</exception>
    public ComponentMapEntry[] AsMap()
    {
        if (Kind != ComponentValueKind.Map)
        {
            throw new InvalidOperationException($"Cannot read '{Kind}' as map.");
        }

        return CopyArray((ComponentMapEntry[]?)_compoundValue ?? Array.Empty<ComponentMapEntry>());
    }

    internal ComponentNative.Value ToNative(IntPtr context)
    {
        switch (Kind)
        {
            case ComponentValueKind.Bool:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { Bool = _boolean ? (byte)1 : (byte)0 },
                };

            case ComponentValueKind.S8:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { S8 = unchecked((sbyte)_scalar32) },
                };

            case ComponentValueKind.U8:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { U8 = unchecked((byte)_scalar32) },
                };

            case ComponentValueKind.S16:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { S16 = unchecked((short)_scalar32) },
                };

            case ComponentValueKind.U16:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { U16 = unchecked((ushort)_scalar32) },
                };

            case ComponentValueKind.S32:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { S32 = unchecked((int)_scalar32) },
                };

            case ComponentValueKind.U32:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { U32 = _scalar32 },
                };

            case ComponentValueKind.S64:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { S64 = unchecked((long)_scalar64) },
                };

            case ComponentValueKind.U64:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { U64 = _scalar64 },
                };

            case ComponentValueKind.F32:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { F32 = _f32 },
                };

            case ComponentValueKind.F64:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { F64 = _f64 },
                };

            case ComponentValueKind.Char:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion { Character = _scalar32 },
                };

            case ComponentValueKind.String:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        String = CreateName(_stringValue ?? string.Empty),
                    },
                };

            case ComponentValueKind.List:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        List = CreateValueVector((ComponentValue[]?)_compoundValue ?? Array.Empty<ComponentValue>(), context, ValueVectorKind.List),
                    },
                };

            case ComponentValueKind.Record:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        Record = CreateRecordVector((ComponentRecordField[]?)_compoundValue ?? Array.Empty<ComponentRecordField>(), context),
                    },
                };

            case ComponentValueKind.Tuple:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        Tuple = CreateValueVector((ComponentValue[]?)_compoundValue ?? Array.Empty<ComponentValue>(), context, ValueVectorKind.Tuple),
                    },
                };

            case ComponentValueKind.Variant:
                var variant = (VariantValue?)_compoundValue ?? throw new InvalidOperationException("The variant value is not initialized.");
                var discriminant = CreateName(variant.Discriminant);
                try
                {
                    return new ComponentNative.Value
                    {
                        Kind = (byte)Kind,
                        Of = new ComponentNative.ValueUnion
                        {
                            Variant = new ComponentNative.ValueVariant
                            {
                                Discriminant = discriminant,
                                Value = variant.Payload.HasValue ? CreateOwnedNativeValue(context, variant.Payload.Value) : IntPtr.Zero,
                            },
                        },
                    };
                }
                catch
                {
                    Native.wasm_byte_vec_delete(ref discriminant);
                    throw;
                }

            case ComponentValueKind.Enum:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        Enumeration = CreateName(_stringValue ?? string.Empty),
                    },
                };

            case ComponentValueKind.Option:
                var option = (OptionalValue?)_compoundValue ?? new OptionalValue(null);
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        Option = option.HasValue ? CreateOwnedNativeValue(context, option.Value) : IntPtr.Zero,
                    },
                };

            case ComponentValueKind.Result:
                var result = (OptionalValue?)_compoundValue ?? new OptionalValue(null);
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        Result = new ComponentNative.ValueResult
                        {
                            IsOk = _resultIsOk,
                            Value = result.HasValue ? CreateOwnedNativeValue(context, result.Value) : IntPtr.Zero,
                        },
                    },
                };

            case ComponentValueKind.Flags:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        Flags = CreateFlagsVector((string[]?)_compoundValue ?? Array.Empty<string>()),
                    },
                };

            case ComponentValueKind.Resource:
                var store = new StoreContext(context).Store;
                if (!ComponentResourceRegistry.IsTracked(store, _resource))
                {
                    throw new InvalidOperationException($"The component resource '{_resource.Type}:{_resource.Representation}' is not registered for the current store.");
                }

                var hostResource = Native.wasmtime_component_resource_host_new(_resource.IsOwned, _resource.Representation, _resource.Type);
                if (hostResource == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create the host-defined resource value.");
                }

                try
                {
                    var error = Native.wasmtime_component_resource_host_to_any(context, hostResource, out var resourceAny);
                    if (error != IntPtr.Zero)
                    {
                        throw WasmtimeException.FromOwnedError(error);
                    }

                    return new ComponentNative.Value
                    {
                        Kind = (byte)Kind,
                        Of = new ComponentNative.ValueUnion
                        {
                            Resource = resourceAny,
                        },
                    };
                }
                finally
                {
                    Native.wasmtime_component_resource_host_delete(hostResource);
                }

            case ComponentValueKind.Map:
                return new ComponentNative.Value
                {
                    Kind = (byte)Kind,
                    Of = new ComponentNative.ValueUnion
                    {
                        Map = CreateMapVector((ComponentMapEntry[]?)_compoundValue ?? Array.Empty<ComponentMapEntry>(), context),
                    },
                };

            default:
                throw new NotSupportedException($"Component value kind '{Kind}' is not supported yet.");
        }
    }

    internal static ComponentValue FromNative(IntPtr context, ComponentNative.Value value) =>
        value.Kind switch
        {
            (byte)ComponentValueKind.Bool => FromBoolean(value.Of.Bool != 0),
            (byte)ComponentValueKind.S8 => FromInt8(value.Of.S8),
            (byte)ComponentValueKind.U8 => FromUInt8(value.Of.U8),
            (byte)ComponentValueKind.S16 => FromInt16(value.Of.S16),
            (byte)ComponentValueKind.U16 => FromUInt16(value.Of.U16),
            (byte)ComponentValueKind.S32 => FromInt32(value.Of.S32),
            (byte)ComponentValueKind.U32 => FromUInt32(value.Of.U32),
            (byte)ComponentValueKind.S64 => FromInt64(value.Of.S64),
            (byte)ComponentValueKind.U64 => FromUInt64(value.Of.U64),
            (byte)ComponentValueKind.F32 => FromFloat32(value.Of.F32),
            (byte)ComponentValueKind.F64 => FromFloat64(value.Of.F64),
            (byte)ComponentValueKind.Char => FromRune(new Rune(checked((int)value.Of.Character))),
            (byte)ComponentValueKind.String => FromString(value.Of.String.Data == IntPtr.Zero ? string.Empty : Extensions.PtrToStringUTF8(value.Of.String.Data, checked((int)value.Of.String.Size))),
            (byte)ComponentValueKind.List => FromList(ReadValueVector(context, value.Of.List)),
            (byte)ComponentValueKind.Record => FromRecord(ReadRecordVector(context, value.Of.Record)),
            (byte)ComponentValueKind.Tuple => FromTuple(ReadValueVector(context, value.Of.Tuple)),
            (byte)ComponentValueKind.Variant => FromVariant(
                ReadName(value.Of.Variant.Discriminant),
                value.Of.Variant.Value == IntPtr.Zero
                    ? null
                    : FromNative(context, Marshal.PtrToStructure<ComponentNative.Value>(value.Of.Variant.Value))),
            (byte)ComponentValueKind.Enum => FromEnum(ReadName(value.Of.Enumeration)),
            (byte)ComponentValueKind.Option => FromOption(
                value.Of.Option == IntPtr.Zero
                    ? null
                    : FromNative(context, Marshal.PtrToStructure<ComponentNative.Value>(value.Of.Option))),
            (byte)ComponentValueKind.Resource => FromHostResource(context, value.Of.Resource),
            (byte)ComponentValueKind.Result => FromResult(
                value.Of.Result.IsOk,
                value.Of.Result.Value == IntPtr.Zero
                    ? null
                    : FromNative(context, Marshal.PtrToStructure<ComponentNative.Value>(value.Of.Result.Value))),
            (byte)ComponentValueKind.Flags => FromFlags(ReadNameVector(value.Of.Flags)),
            (byte)ComponentValueKind.Map => FromMap(ReadMapVector(context, value.Of.Map)),
            _ => throw new NotSupportedException($"Component value kind '{(ComponentValueKind)value.Kind}' is not supported yet."),
        };

    private static ComponentValue FromHostResource(IntPtr context, IntPtr resourceAny)
    {
        var error = Native.wasmtime_component_resource_any_to_host(context, resourceAny, out var hostResource);
        if (error != IntPtr.Zero)
        {
            throw WasmtimeException.FromOwnedError(error);
        }

        try
        {
            return FromResource(
                new ComponentResource(
                    Native.wasmtime_component_resource_host_owned(hostResource),
                    Native.wasmtime_component_resource_host_rep(hostResource),
                    Native.wasmtime_component_resource_host_type(hostResource)));
        }
        finally
        {
            Native.wasmtime_component_resource_host_delete(hostResource);
        }
    }

    private static ComponentNative.WasmName CreateName(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Native.wasm_byte_vec_new(out var name, checked((nuint)bytes.Length), bytes);
        return name;
    }

    private static string ReadName(ComponentNative.WasmName name)
    {
        return name.Data == IntPtr.Zero ? string.Empty : Extensions.PtrToStringUTF8(name.Data, checked((int)name.Size));
    }

    private static ComponentNative.ValueVector CreateValueVector(ComponentValue[] values, IntPtr context, ValueVectorKind kind)
    {
        if (values.Length == 0)
        {
            switch (kind)
            {
                case ValueVectorKind.List:
                    Native.wasmtime_component_vallist_new_empty(out var vector);
                    return vector;
                default:
                    Native.wasmtime_component_valtuple_new_empty(out var tuple);
                    return tuple;
            }
        }

        var nativeValues = new ComponentNative.Value[values.Length];
        var builtCount = 0;
        try
        {
            for (var index = 0; index < values.Length; index++)
            {
                nativeValues[index] = values[index].ToNative(context);
                builtCount++;
            }

            switch (kind)
            {
                case ValueVectorKind.List:
                    Native.wasmtime_component_vallist_new(out var vector, checked((nuint)nativeValues.Length), nativeValues);
                    return vector;
                default:
                    Native.wasmtime_component_valtuple_new(out var tuple, checked((nuint)nativeValues.Length), nativeValues);
                    return tuple;
            }
        }
        catch
        {
            for (var index = 0; index < builtCount; index++)
            {
                Native.wasmtime_component_val_delete(ref nativeValues[index]);
            }

            throw;
        }
    }

    private static ComponentNative.ValueVector CreateRecordVector(ComponentRecordField[] fields, IntPtr context)
    {
        if (fields.Length == 0)
        {
            Native.wasmtime_component_valrecord_new_empty(out var empty);
            return empty;
        }

        var entries = new ComponentNative.RecordEntry[fields.Length];
        var builtCount = 0;
        try
        {
            for (var index = 0; index < fields.Length; index++)
            {
                entries[index].Name = CreateName(fields[index].Name);
                try
                {
                    entries[index].Value = fields[index].Value.ToNative(context);
                    builtCount++;
                }
                catch
                {
                    Native.wasm_byte_vec_delete(ref entries[index].Name);
                    throw;
                }
            }

            Native.wasmtime_component_valrecord_new(out var vector, checked((nuint)entries.Length), entries);
            return vector;
        }
        catch
        {
            for (var index = 0; index < builtCount; index++)
            {
                Native.wasm_byte_vec_delete(ref entries[index].Name);
                Native.wasmtime_component_val_delete(ref entries[index].Value);
            }

            throw;
        }
    }

    private static ComponentNative.ValueVector CreateFlagsVector(string[] names)
    {
        if (names.Length == 0)
        {
            Native.wasmtime_component_valflags_new_empty(out var empty);
            return empty;
        }

        var nativeNames = new ComponentNative.WasmName[names.Length];
        var builtCount = 0;
        try
        {
            for (var index = 0; index < names.Length; index++)
            {
                nativeNames[index] = CreateName(names[index]);
                builtCount++;
            }

            Native.wasmtime_component_valflags_new(out var vector, checked((nuint)nativeNames.Length), nativeNames);
            return vector;
        }
        catch
        {
            for (var index = 0; index < builtCount; index++)
            {
                Native.wasm_byte_vec_delete(ref nativeNames[index]);
            }

            throw;
        }
    }

    private static ComponentNative.ValueVector CreateMapVector(ComponentMapEntry[] entries, IntPtr context)
    {
        if (entries.Length == 0)
        {
            Native.wasmtime_component_valmap_new_empty(out var empty);
            return empty;
        }

        var nativeEntries = new ComponentNative.MapEntry[entries.Length];
        var builtCount = 0;
        try
        {
            for (var index = 0; index < entries.Length; index++)
            {
                nativeEntries[index].Key = entries[index].Key.ToNative(context);
                try
                {
                    nativeEntries[index].Value = entries[index].Value.ToNative(context);
                    builtCount++;
                }
                catch
                {
                    Native.wasmtime_component_val_delete(ref nativeEntries[index].Key);
                    throw;
                }
            }

            Native.wasmtime_component_valmap_new(out var vector, checked((nuint)nativeEntries.Length), nativeEntries);
            return vector;
        }
        catch
        {
            for (var index = 0; index < builtCount; index++)
            {
                Native.wasmtime_component_val_delete(ref nativeEntries[index].Key);
                Native.wasmtime_component_val_delete(ref nativeEntries[index].Value);
            }

            throw;
        }
    }

    private static IntPtr CreateOwnedNativeValue(IntPtr context, ComponentValue value)
    {
        var nativeValue = value.ToNative(context);
        return Native.wasmtime_component_val_new(ref nativeValue);
    }

    private static ComponentValue[] ReadValueVector(IntPtr context, ComponentNative.ValueVector vector)
    {
        var length = checked((int)vector.Size);
        var values = new ComponentValue[length];
        unsafe
        {
            var data = (ComponentNative.Value*)vector.Data;
            for (var index = 0; index < length; index++)
            {
                values[index] = FromNative(context, data[index]);
            }
        }

        return values;
    }

    private static ComponentRecordField[] ReadRecordVector(IntPtr context, ComponentNative.ValueVector vector)
    {
        var length = checked((int)vector.Size);
        var fields = new ComponentRecordField[length];
        unsafe
        {
            var data = (ComponentNative.RecordEntry*)vector.Data;
            for (var index = 0; index < length; index++)
            {
                fields[index] = new ComponentRecordField(ReadName(data[index].Name), FromNative(context, data[index].Value));
            }
        }

        return fields;
    }

    private static string[] ReadNameVector(ComponentNative.ValueVector vector)
    {
        var length = checked((int)vector.Size);
        var names = new string[length];
        unsafe
        {
            var data = (ComponentNative.WasmName*)vector.Data;
            for (var index = 0; index < length; index++)
            {
                names[index] = ReadName(data[index]);
            }
        }

        return names;
    }

    private static ComponentMapEntry[] ReadMapVector(IntPtr context, ComponentNative.ValueVector vector)
    {
        var length = checked((int)vector.Size);
        var entries = new ComponentMapEntry[length];
        unsafe
        {
            var data = (ComponentNative.MapEntry*)vector.Data;
            for (var index = 0; index < length; index++)
            {
                entries[index] = new ComponentMapEntry(FromNative(context, data[index].Key), FromNative(context, data[index].Value));
            }
        }

        return entries;
    }

    private static T[] CopyArray<T>(T[] values)
    {
        var copy = new T[values.Length];
        Array.Copy(values, copy, values.Length);
        return copy;
    }

    private static void ValidateStrings(string[] values, string parameterName)
    {
        if (values.Any(value => value is null))
            throw new ArgumentNullException(parameterName, "String values must not contain null elements.");
    }

    private static void ValidateRecordFields(ComponentRecordField[] fields, string parameterName)
    {
        foreach (var f in fields)
        {
            if (f.Name is null)
                throw new ArgumentNullException(parameterName, "Record field names must not be null.");
        }
    }

    private enum ValueVectorKind
    {
        List,
        Tuple,
    }

    private sealed class OptionalValue
    {
        public OptionalValue(ComponentValue? value)
        {
            HasValue = value.HasValue;
            Value = value.GetValueOrDefault();
        }

        public bool HasValue { get; }

        public ComponentValue Value { get; }
    }

    private sealed class VariantValue
    {
        public VariantValue(string discriminant, ComponentValue? value)
        {
            Discriminant = discriminant;
            Payload = new OptionalValue(value);
        }

        public string Discriminant { get; }

        public OptionalValue Payload { get; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "IdentifierTypo")]
    internal static class Native
    {
        [DllImport(Engine.LibraryName, EntryPoint = "wasm_byte_vec_new")]
        public static extern void wasm_byte_vec_new(out ComponentNative.WasmName output, nuint size, [In] byte[] bytes);

        [DllImport(Engine.LibraryName, EntryPoint = "wasm_byte_vec_delete")]
        public static extern void wasm_byte_vec_delete(ref ComponentNative.WasmName name);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_vallist_new_empty(out ComponentNative.ValueVector output);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_vallist_new(out ComponentNative.ValueVector output, nuint size, [In] ComponentNative.Value[] values);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_valrecord_new_empty(out ComponentNative.ValueVector output);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_valrecord_new(out ComponentNative.ValueVector output, nuint size, [In] ComponentNative.RecordEntry[] values);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_valtuple_new_empty(out ComponentNative.ValueVector output);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_valtuple_new(out ComponentNative.ValueVector output, nuint size, [In] ComponentNative.Value[] values);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_valflags_new_empty(out ComponentNative.ValueVector output);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_valflags_new(out ComponentNative.ValueVector output, nuint size, [In] ComponentNative.WasmName[] names);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_valmap_new_empty(out ComponentNative.ValueVector output);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_valmap_new(out ComponentNative.ValueVector output, nuint size, [In] ComponentNative.MapEntry[] entries);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_val_new(ref ComponentNative.Value value);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_val_delete(ref ComponentNative.Value value);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_resource_host_new([MarshalAs(UnmanagedType.I1)] bool owned, uint representation, uint type);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_resource_host_delete(IntPtr resource);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_resource_host_to_any(IntPtr context, IntPtr resource, out IntPtr resource_any);

        [DllImport(Engine.LibraryName)]
        public static extern IntPtr wasmtime_component_resource_any_to_host(IntPtr context, IntPtr resource_any, out IntPtr host_resource);

        [DllImport(Engine.LibraryName)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool wasmtime_component_resource_host_owned(IntPtr resource);

        [DllImport(Engine.LibraryName)]
        public static extern uint wasmtime_component_resource_host_rep(IntPtr resource);

        [DllImport(Engine.LibraryName)]
        public static extern uint wasmtime_component_resource_host_type(IntPtr resource);

        [DllImport(Engine.LibraryName)]
        public static extern void wasmtime_component_resource_any_delete(IntPtr resource_any);
    }
}

public readonly struct ComponentRecordField
{
    public ComponentRecordField(string name, ComponentValue value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
    }

    public string Name { get; }

    public ComponentValue Value { get; }
}

public readonly struct ComponentVariant
{
    public ComponentVariant(string discriminant, ComponentValue? value)
    {
        Discriminant = discriminant ?? throw new ArgumentNullException(nameof(discriminant));
        Value = value;
    }

    public string Discriminant { get; }

    public ComponentValue? Value { get; }
}

public readonly struct ComponentResult
{
    public ComponentResult(bool isOk, ComponentValue? value)
    {
        IsOk = isOk;
        Value = value;
    }

    public bool IsOk { get; }

    public ComponentValue? Value { get; }
}

public readonly struct ComponentMapEntry
{
    public ComponentMapEntry(ComponentValue key, ComponentValue value)
    {
        Key = key;
        Value = value;
    }

    public ComponentValue Key { get; }

    public ComponentValue Value { get; }
}

internal static class ComponentNative
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WasmName
    {
        public nuint Size;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ValueVector
    {
        public nuint Size;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ValueVariant
    {
        public WasmName Discriminant;
        public IntPtr Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ValueResult
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool IsOk;
        public IntPtr Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RecordEntry
    {
        public WasmName Name;
        public Value Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MapEntry
    {
        public Value Key;
        public Value Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct ValueUnion
    {
        [FieldOffset(0)]
        public byte Bool;

        [FieldOffset(0)]
        public sbyte S8;

        [FieldOffset(0)]
        public byte U8;

        [FieldOffset(0)]
        public short S16;

        [FieldOffset(0)]
        public ushort U16;

        [FieldOffset(0)]
        public int S32;

        [FieldOffset(0)]
        public uint U32;

        [FieldOffset(0)]
        public long S64;

        [FieldOffset(0)]
        public ulong U64;

        [FieldOffset(0)]
        public float F32;

        [FieldOffset(0)]
        public double F64;

        [FieldOffset(0)]
        public uint Character;

        [FieldOffset(0)]
        public WasmName String;

        [FieldOffset(0)]
        public ValueVector List;

        [FieldOffset(0)]
        public ValueVector Record;

        [FieldOffset(0)]
        public ValueVector Tuple;

        [FieldOffset(0)]
        public ValueVariant Variant;

        [FieldOffset(0)]
        public WasmName Enumeration;

        [FieldOffset(0)]
        public IntPtr Option;

        [FieldOffset(0)]
        public ValueResult Result;

        [FieldOffset(0)]
        public ValueVector Flags;

        [FieldOffset(0)]
        public ValueVector Map;

        [FieldOffset(0)]
        public IntPtr Resource;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Value
    {
        public byte Kind;
        public ValueUnion Of;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Instance
    {
        static Instance()
        {
            Debug.Assert(Marshal.SizeOf<Instance>() == 16);
            Debug.Assert(Marshal.OffsetOf<Instance>(nameof(StoreId)).ToInt32() == 0);
            Debug.Assert(Marshal.OffsetOf<Instance>(nameof(PrivateData)).ToInt32() == 8);
        }

        public ulong StoreId;
        public uint PrivateData;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Function
    {
        static Function()
        {
            Debug.Assert(Marshal.SizeOf<Function>() == 24);
            Debug.Assert(Marshal.OffsetOf<Function>(nameof(Instance)).ToInt32() == 0);
            Debug.Assert(Marshal.OffsetOf<Function>(nameof(PrivateData)).ToInt32() == 16);
        }

        public Instance Instance;
        public uint PrivateData;
    }

}