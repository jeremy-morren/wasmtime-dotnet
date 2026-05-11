using System.Text;
using Wasmtime.Components;

namespace Wasmtime.Tests
{
    /// <summary>
    /// Exercises low-level component value construction, conversion, and native round-trip marshalling.
    /// </summary>
    public class ComponentValueTests
    {
        private static void AssertRoundTrip(
            ComponentValue value,
            ComponentValueKind expectedKind,
            Action<ComponentNative.Value> assertNative,
            Action<ComponentValue> assertRoundTrip)
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var store = new Store(engine);
            var native = default(ComponentNative.Value);
            var initialized = false;

            try
            {
                native = value.ToNative(store.Context.handle);
                initialized = true;
                var roundTrip = ComponentValue.FromNative(store.Context.handle, native);

                Assert.Equal((byte)expectedKind, native.Kind);
                assertNative(native);
                assertRoundTrip(roundTrip);
            }
            finally
            {
                if (initialized)
                {
                    ComponentValue.Native.wasmtime_component_val_delete(ref native);
                }
            }
        }

        [Fact]
        public void ItRoundTripsBooleanValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromBoolean(true),
                ComponentValueKind.Bool,
                native => Assert.Equal((byte)1, native.Of.Bool),
                roundTrip => Assert.True(roundTrip.AsBoolean()));
        }

        [Fact]
        public void ItRoundTripsS8ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromInt8(-12),
                ComponentValueKind.S8,
                native => Assert.Equal((sbyte)-12, native.Of.S8),
                roundTrip => Assert.Equal((sbyte)-12, roundTrip.AsInt8()));
        }

        [Fact]
        public void ItRoundTripsU8ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromUInt8(240),
                ComponentValueKind.U8,
                native => Assert.Equal((byte)240, native.Of.U8),
                roundTrip => Assert.Equal((byte)240, roundTrip.AsUInt8()));
        }

        [Fact]
        public void ItRoundTripsS16ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromInt16(-1234),
                ComponentValueKind.S16,
                native => Assert.Equal((short)-1234, native.Of.S16),
                roundTrip => Assert.Equal((short)-1234, roundTrip.AsInt16()));
        }

        [Fact]
        public void ItRoundTripsU16ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromUInt16(54321),
                ComponentValueKind.U16,
                native => Assert.Equal((ushort)54321, native.Of.U16),
                roundTrip => Assert.Equal((ushort)54321, roundTrip.AsUInt16()));
        }

        [Fact]
        public void ItRoundTripsS32ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromInt32(1234),
                ComponentValueKind.S32,
                native => Assert.Equal(1234, native.Of.S32),
                roundTrip => Assert.Equal(1234, roundTrip.AsInt32()));
        }

        [Fact]
        public void ItRoundTripsU32ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromUInt32(uint.MaxValue - 10),
                ComponentValueKind.U32,
                native => Assert.Equal(uint.MaxValue - 10, native.Of.U32),
                roundTrip => Assert.Equal(uint.MaxValue - 10, roundTrip.AsUInt32()));
        }

        [Fact]
        public void ItRoundTripsS64ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromInt64(-1234567890123456789L),
                ComponentValueKind.S64,
                native => Assert.Equal(-1234567890123456789L, native.Of.S64),
                roundTrip => Assert.Equal(-1234567890123456789L, roundTrip.AsInt64()));
        }

        [Fact]
        public void ItRoundTripsU64ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromUInt64(ulong.MaxValue - 10),
                ComponentValueKind.U64,
                native => Assert.Equal(ulong.MaxValue - 10, native.Of.U64),
                roundTrip => Assert.Equal(ulong.MaxValue - 10, roundTrip.AsUInt64()));
        }

        [Fact]
        public void ItRoundTripsF32ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromFloat32(123.5f),
                ComponentValueKind.F32,
                native => Assert.Equal(123.5f, native.Of.F32),
                roundTrip => Assert.Equal(123.5f, roundTrip.AsFloat32()));
        }

        [Fact]
        public void ItRoundTripsF64ValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromFloat64(-9876.5d),
                ComponentValueKind.F64,
                native => Assert.Equal(-9876.5d, native.Of.F64),
                roundTrip => Assert.Equal(-9876.5d, roundTrip.AsFloat64()));
        }

        [Fact]
        public void ItRoundTripsRuneValuesThroughTheNativeShape()
        {
            var value = new Rune(0x1F984);

            AssertRoundTrip(
                ComponentValue.FromRune(value),
                ComponentValueKind.Char,
                native => Assert.Equal((uint)value.Value, native.Of.Character),
                roundTrip => Assert.Equal(value, roundTrip.AsRune()));
        }

        [Fact]
        public void ItRoundTripsBmpCharValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromChar('A'),
                ComponentValueKind.Char,
                native => Assert.Equal((uint)'A', native.Of.Character),
                roundTrip =>
                {
                    roundTrip.AsRune().Should().Be(new Rune('A'));
                    roundTrip.AsChar().Should().Be('A');
                });
        }

        [Fact]
        public void ItRoundTripsStringValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromString("hello component"),
                ComponentValueKind.String,
                native => Assert.Equal((nuint)15, native.Of.String.Size),
                roundTrip => Assert.Equal("hello component", roundTrip.AsString()));
        }

        [Fact]
        public void ItRoundTripsListValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromList(ComponentValue.FromInt32(1), ComponentValue.FromString("two")),
                ComponentValueKind.List,
                native => Assert.Equal((nuint)2, native.Of.List.Size),
                roundTrip =>
                {
                    var values = roundTrip.AsList();
                    values.Should().HaveCount(2);
                    values[0].AsInt32().Should().Be(1);
                    values[1].AsString().Should().Be("two");
                });
        }

        [Fact]
        public void ItRoundTripsRecordValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromRecord(
                    new ComponentRecordField("count", ComponentValue.FromInt32(2)),
                    new ComponentRecordField("label", ComponentValue.FromString("two"))),
                ComponentValueKind.Record,
                native => Assert.Equal((nuint)2, native.Of.Record.Size),
                roundTrip =>
                {
                    var fields = roundTrip.AsRecord();
                    fields.Should().HaveCount(2);
                    fields[0].Name.Should().Be("count");
                    fields[0].Value.AsInt32().Should().Be(2);
                    fields[1].Name.Should().Be("label");
                    fields[1].Value.AsString().Should().Be("two");
                });
        }

        [Fact]
        public void ItRoundTripsTupleValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromTuple(ComponentValue.FromBoolean(true), ComponentValue.FromInt32(3)),
                ComponentValueKind.Tuple,
                native => Assert.Equal((nuint)2, native.Of.Tuple.Size),
                roundTrip =>
                {
                    var values = roundTrip.AsTuple();
                    values.Should().HaveCount(2);
                    values[0].AsBoolean().Should().BeTrue();
                    values[1].AsInt32().Should().Be(3);
                });
        }

        [Fact]
        public void ItRoundTripsVariantValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromVariant("ok", ComponentValue.FromInt32(7)),
                ComponentValueKind.Variant,
                native => native.Of.Variant.Value.Should().NotBe(IntPtr.Zero),
                roundTrip =>
                {
                    var variant = roundTrip.AsVariant();
                    variant.Discriminant.Should().Be("ok");
                    variant.Value.Should().NotBeNull();
                    variant.Value!.Value.AsInt32().Should().Be(7);
                });
        }

        [Fact]
        public void ItRoundTripsEnumValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromEnum("ready"),
                ComponentValueKind.Enum,
                native => Assert.Equal((nuint)5, native.Of.Enumeration.Size),
                roundTrip => roundTrip.AsEnum().Should().Be("ready"));
        }

        [Fact]
        public void ItRoundTripsOptionValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromOption(ComponentValue.FromString("value")),
                ComponentValueKind.Option,
                native => native.Of.Option.Should().NotBe(IntPtr.Zero),
                roundTrip => roundTrip.AsOption()!.Value.AsString().Should().Be("value"));
        }

        [Fact]
        public void ItRoundTripsEmptyOptionValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromOption(null),
                ComponentValueKind.Option,
                native => native.Of.Option.Should().Be(IntPtr.Zero),
                roundTrip => roundTrip.AsOption().Should().BeNull());
        }

        [Fact]
        public void ItCreatesComponentValuesFromCommonClrTypes()
        {
            ComponentValue.FromObject(true).AsBoolean().Should().BeTrue();
            ComponentValue.FromObject((sbyte)-12).AsInt8().Should().Be((sbyte)-12);
            ComponentValue.FromObject((byte)240).AsUInt8().Should().Be((byte)240);
            ComponentValue.FromObject((short)-1234).AsInt16().Should().Be((short)-1234);
            ComponentValue.FromObject((ushort)54321).AsUInt16().Should().Be((ushort)54321);
            ComponentValue.FromObject(1234).AsInt32().Should().Be(1234);
            ComponentValue.FromObject(uint.MaxValue - 10).AsUInt32().Should().Be(uint.MaxValue - 10);
            ComponentValue.FromObject(-1234567890123456789L).AsInt64().Should().Be(-1234567890123456789L);
            ComponentValue.FromObject(ulong.MaxValue - 10).AsUInt64().Should().Be(ulong.MaxValue - 10);
            ComponentValue.FromObject(123.5f).AsFloat32().Should().Be(123.5f);
            ComponentValue.FromObject(-9876.5d).AsFloat64().Should().Be(-9876.5d);
            ComponentValue.FromObject(new Rune(0x1F984)).AsRune().Should().Be(new Rune(0x1F984));
            ComponentValue.FromObject('A').AsChar().Should().Be('A');
            ComponentValue.FromObject('A').AsRune().Should().Be(new Rune('A'));
            ComponentValue.FromObject("hello component").AsString().Should().Be("hello component");
            ComponentValue.FromObject(new[] { ComponentValue.FromInt32(1), ComponentValue.FromInt32(2) })
                .AsList()
                .Select(value => value.AsInt32())
                .Should().Equal(1, 2);
            ComponentValue.FromObject(new[]
                {
                    new ComponentRecordField("count", ComponentValue.FromInt32(2)),
                    new ComponentRecordField("label", ComponentValue.FromString("two")),
                })
                .AsRecord()
                .Select(field => field.Name)
                .Should().Equal("count", "label");
            ComponentValue.FromObject(new ComponentVariant("ok", ComponentValue.FromInt32(7)))
                .AsVariant().Value!.Value.AsInt32().Should().Be(7);
            ComponentValue.FromObject(new ComponentResult(false, ComponentValue.FromString("nope")))
                .AsResult().Value!.Value.AsString().Should().Be("nope");
            ComponentValue.FromObject(new[] { "read", "write" }).AsFlags().Should().Equal("read", "write");
            ComponentValue.FromObject(new[]
                {
                    new ComponentMapEntry(ComponentValue.FromString("one"), ComponentValue.FromInt32(1)),
                    new ComponentMapEntry(ComponentValue.FromString("two"), ComponentValue.FromInt32(2)),
                })
                .AsMap()
                .Select(entry => entry.Key.AsString())
                .Should().Equal("one", "two");

            var resource = new ComponentResource(isOwned: true, representation: 1234, type: 7);
            var resourceValue = ComponentValue.FromObject(resource).AsResource();
            resourceValue.IsOwned.Should().BeTrue();
            resourceValue.Representation.Should().Be((uint)1234);
            resourceValue.Type.Should().Be((uint)7);
        }

        [Fact]
        public void ItReturnsExistingComponentValuesFromObject()
        {
            var value = ComponentValue.FromInt32(1234);

            ComponentValue.FromObject(value).AsInt32().Should().Be(1234);
        }

        [Fact]
        public void ItRejectsNullObjects()
        {
            Action action = () => ComponentValue.FromObject(null!);

            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("value");
        }

        [Fact]
        public void ItRejectsUnsupportedObjectTypes()
        {
            Action action = () => ComponentValue.FromObject(UnixEpoch);

            action.Should().Throw<ArgumentException>()
                .WithParameterName("value")
                .WithMessage("*System.DateTime*");
        }

        [Fact]
        public void ItRejectsLoneSurrogatesWhenCreatingCharValues()
        {
            Action directAction = () => ComponentValue.FromChar('\ud800');
            Action objectAction = () => ComponentValue.FromObject('\ud800');

            directAction.Should().Throw<ArgumentException>()
                .WithParameterName("value")
                .WithMessage("*lone surrogate*");

            objectAction.Should().Throw<ArgumentException>()
                .WithParameterName("value")
                .WithMessage("*lone surrogate*");
        }

        [Fact]
        public void ItThrowsWhenReadingNonBmpRunesAsSingleChars()
        {
            var value = ComponentValue.FromRune(new Rune(0x1F984));

            Action action = () => value.AsChar();

            action.Should().Throw<OverflowException>();
        }

        [Fact]
        public void ItRejectsReadingTheWrongShape()
        {
            var value = ComponentValue.FromString("hello component");

            Assert.Throws<InvalidOperationException>(() => value.AsInt32());
        }

        [Fact]
        public void ItRoundTripsResultValuesWithoutPayloadThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromResult(true),
                ComponentValueKind.Result,
                native => Assert.True(native.Of.Result.IsOk),
                roundTrip => Assert.True(roundTrip.AsResultIsOk()));
        }

        [Fact]
        public void ItRoundTripsResultValuesWithPayloadThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromResult(isOk: false, ComponentValue.FromString("nope")),
                ComponentValueKind.Result,
                native =>
                {
                    native.Of.Result.IsOk.Should().BeFalse();
                    native.Of.Result.Value.Should().NotBe(IntPtr.Zero);
                },
                roundTrip =>
                {
                    var result = roundTrip.AsResult();
                    result.IsOk.Should().BeFalse();
                    result.Value.Should().NotBeNull();
                    result.Value!.Value.AsString().Should().Be("nope");
                });
        }

        [Fact]
        public void ItRoundTripsFlagsValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromFlags("read", "write"),
                ComponentValueKind.Flags,
                native => Assert.Equal((nuint)2, native.Of.Flags.Size),
                roundTrip => roundTrip.AsFlags().Should().Equal("read", "write"));
        }

        [Fact]
        public void ItRoundTripsResourceValuesThroughTheNativeShape()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var store = new Store(engine);
            using var resourceType = new ComponentResourceType(7);
            var registry = new ComponentResourceRegistry(store);
            var value = ComponentValue.FromResource(registry.CreateOwned(resourceType, 1234u));
            var native = default(ComponentNative.Value);
            var initialized = false;

            try
            {
                native = value.ToNative(store.Context.handle);
                initialized = true;

                var roundTrip = ComponentValue.FromNative(store.Context.handle, native);
                var resource = roundTrip.AsResource();

                Assert.Equal((byte)ComponentValueKind.Resource, native.Kind);
                Assert.NotEqual(IntPtr.Zero, native.Of.Resource);
                Assert.True(resource.IsOwned);
                Assert.Equal((uint)0, resource.Representation);
                Assert.Equal(resourceType.Id, resource.Type);
            }
            finally
            {
                if (initialized)
                {
                    ComponentValue.Native.wasmtime_component_val_delete(ref native);
                }
            }
        }

        [Fact]
        public void ItRejectsLoweringUntrackedResourceValues()
        {
            using var config = new Config().WithComponentModel(true);
            using var engine = new Engine(config);
            using var store = new Store(engine);
            var value = ComponentValue.FromResource(new ComponentResource(isOwned: true, representation: 1234, type: 7));

            Action lower = () => value.ToNative(store.Context.handle);

            lower.Should().Throw<InvalidOperationException>()
                .WithMessage("*not registered for the current store*");
        }

        [Fact]
        public void ItRoundTripsMapValuesThroughTheNativeShape()
        {
            AssertRoundTrip(
                ComponentValue.FromMap(
                    new ComponentMapEntry(ComponentValue.FromString("one"), ComponentValue.FromInt32(1)),
                    new ComponentMapEntry(ComponentValue.FromString("two"), ComponentValue.FromInt32(2))),
                ComponentValueKind.Map,
                native => Assert.Equal((nuint)2, native.Of.Map.Size),
                roundTrip =>
                {
                    var entries = roundTrip.AsMap();
                    entries.Should().HaveCount(2);
                    entries[0].Key.AsString().Should().Be("one");
                    entries[0].Value.AsInt32().Should().Be(1);
                    entries[1].Key.AsString().Should().Be("two");
                    entries[1].Value.AsInt32().Should().Be(2);
                });
        }

        [Fact]
        public void ItRejectsReadingResourceValuesAsTheWrongShape()
        {
            var value = ComponentValue.FromResource(new ComponentResource(isOwned: false, representation: 1, type: 2));

            Assert.Throws<InvalidOperationException>(() => value.AsString());
        }
        

        private static DateTime UnixEpoch { get; } = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }
}