using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Wasmtime
{
    /// <summary>
    /// Represents the code associated with a trap.
    /// </summary>
    /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>wasmtime_trap_code_t</c> and <c>wasmtime_trap_code_enum</c>.</remarks>
    public enum TrapCode
    {
        /// <summary>
        /// The trap has no associated trap code.
        /// </summary>
        /// <remarks>No direct C ABI constant. This is a managed sentinel used when <c>wasmtime_trap_code</c> does not return a trap code.</remarks>
        Undefined = -1,
        /// <summary>The trap was the result of exhausting the available stack space.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_STACK_OVERFLOW</c>.</remarks>
        StackOverflow = 0,
        /// <summary>The trap was the result of an out-of-bounds memory access.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_MEMORY_OUT_OF_BOUNDS</c>.</remarks>
        MemoryOutOfBounds = 1,
        /// <summary>The trap was the result of a wasm atomic operation that was presented with a misaligned linear-memory address.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_HEAP_MISALIGNED</c>.</remarks>
        HeapMisaligned = 2,
        /// <summary>The trap was the result of an out-of-bounds access to a table.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_TABLE_OUT_OF_BOUNDS</c>.</remarks>
        TableOutOfBounds = 3,
        /// <summary>The trap was the result of an indirect call to a null table entry.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_INDIRECT_CALL_TO_NULL</c>.</remarks>
        IndirectCallToNull = 4,
        /// <summary>The trap was the result of a signature mismatch on indirect call.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_BAD_SIGNATURE</c>.</remarks>
        BadSignature = 5,
        /// <summary>The trap was the result of an integer arithmetic operation that overflowed.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_INTEGER_OVERFLOW</c>.</remarks>
        IntegerOverflow = 6,
        /// <summary>The trap was the result of an integer division by zero.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_INTEGER_DIVISION_BY_ZERO</c>.</remarks>
        IntegerDivisionByZero = 7,
        /// <summary>The trap was the result of a failed float-to-int conversion.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_BAD_CONVERSION_TO_INTEGER</c>.</remarks>
        BadConversionToInteger = 8,
        /// <summary>The trap was the result of executing the `unreachable` instruction.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_UNREACHABLE_CODE_REACHED</c>.</remarks>
        Unreachable = 9,
        /// <summary>The trap was the result of interrupting execution.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_INTERRUPT</c>.</remarks>
        Interrupt = 10,
        /// <summary>
        /// This trap code was removed upstream and is kept only as a compatibility alias.
        /// </summary>
        /// <remarks>No current C ABI constant. This obsolete alias preserves the historical managed value 11.</remarks>
        [Obsolete("This trap code was removed upstream. Use the current trap codes defined by the vendored Wasmtime C API.")]
        AlwaysTrapAdapter = 11,
        /// <summary>The trap was the result of running out of the configured fuel amount.</summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_OUT_OF_FUEL</c>.</remarks>
        OutOfFuel = 11,
        /// <summary>
        /// The trap was the result of atomic wait operations on non-shared memory.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_ATOMIC_WAIT_NON_SHARED_MEMORY</c>.</remarks>
        AtomicWaitNonSharedMemory = 12,
        /// <summary>
        /// The trap was the result of a call to a null reference.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_NULL_REFERENCE</c>.</remarks>
        NullReference = 13,
        /// <summary>
        /// The trap was the result of an attempt to access beyond the bounds of an array.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_ARRAY_OUT_OF_BOUNDS</c>.</remarks>
        ArrayOutOfBounds = 14,
        /// <summary>
        /// The trap was the result of an allocation that was too large to succeed.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_ALLOCATION_TOO_LARGE</c>.</remarks>
        AllocationTooLarge = 15,
        /// <summary>
        /// The trap was the result of an attempt to cast a reference to a type that it is not an instance of.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_CAST_FAILURE</c>.</remarks>
        CastFailure = 16,
        /// <summary>
        /// The trap was the result of a component calling another component that would have violated the reentrance rules.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_CANNOT_ENTER_COMPONENT</c>.</remarks>
        CannotEnterComponent = 17,
        /// <summary>
        /// The trap was the result of an async-lifted export failing to return a valid async result.
        /// </summary>
        /// <remarks>
        /// An async-lifted export failed to produce a result by calling `task.return` before returning `STATUS_DONE`
        /// and/or after all host tasks completed.
        /// Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_NO_ASYNC_RESULT</c>.
        /// </remarks>
        NoAsyncResult = 18,
        /// <summary>
        /// The trap was the result of suspending to a tag for which there is no active handler.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_UNHANDLED_TAG</c>.</remarks>
        UnhandledTag = 19,
        /// <summary>
        /// The trap was the result of attempting to resume a continuation twice.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_CONTINUATION_ALREADY_CONSUMED</c>.</remarks>
        ContinuationAlreadyConsumed = 20,
        /// <summary>
        /// The trap was the result of a Pulley opcode executed at runtime when the opcode was disabled at compile time.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_DISABLED_OPCODE</c>.</remarks>
        DisabledOpCode = 21,
        /// <summary>
        /// The trap was the result of the async event loop deadlocking.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_ASYNC_DEADLOCK</c>.</remarks>
        AsyncDeadlock = 22,
        /// <summary>
        /// The trap was the result of a component calling out when it was not allowed to.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_CANNOT_LEAVE_COMPONENT</c>.</remarks>
        CannotLeaveComponent = 23,
        /// <summary>
        /// The trap was the result of a synchronous task attempting a potentially blocking call.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_CANNOT_BLOCK_SYNC_TASK</c>.</remarks>
        CannotBlockSyncTask = 24,
        /// <summary>
        /// The trap was the result of lifting a `char` with an invalid bit pattern.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_INVALID_CHAR</c>.</remarks>
        InvalidChar = 25,
        /// <summary>
        /// The trap was the result of a fused adapter string-encoding completion assertion.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_DEBUG_ASSERT_STRING_ENCODING_FINISHED</c>.</remarks>
        DebugAssertStringEncodingFinished = 26,
        /// <summary>
        /// The trap was the result of a fused adapter string-encoding assertion.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_DEBUG_ASSERT_EQUAL_CODE_UNITS</c>.</remarks>
        DebugAssertEqualCodeUnits = 27,
        /// <summary>
        /// The trap was the result of a fused adapter pointer-alignment assertion.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_DEBUG_ASSERT_POINTER_ALIGNED</c>.</remarks>
        DebugAssertPointerAligned = 28,
        /// <summary>
        /// The trap was the result of a fused adapter upper-bits assertion.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_DEBUG_ASSERT_UPPER_BITS_UNSET</c>.</remarks>
        DebugAssertUpperBitsUnset = 29,
        /// <summary>
        /// The trap was the result of string access beyond the bounds of memory.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_STRING_OUT_OF_BOUNDS</c>.</remarks>
        StringOutOfBounds = 30,
        /// <summary>
        /// The trap was the result of list access beyond the bounds of memory.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_LIST_OUT_OF_BOUNDS</c>.</remarks>
        ListOutOfBounds = 31,
        /// <summary>
        /// The trap was the result of an invalid discriminant during lowering.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_INVALID_DISCRIMINANT</c>.</remarks>
        InvalidDiscriminant = 32,
        /// <summary>
        /// The trap was the result of an unaligned pointer during lifting or lowering.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_UNALIGNED_POINTER</c>.</remarks>
        UnalignedPointer = 33,
        /// <summary>
        /// The trap was the result of a task cancellation state violation.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_TASK_CANCEL_NOT_CANCELLED</c>.</remarks>
        TaskCancelNotCancelled = 34,
        /// <summary>
        /// The trap was the result of cancelling or returning a task twice.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_TASK_CANCEL_OR_RETURN_TWICE</c>.</remarks>
        TaskCancelOrReturnTwice = 35,
        /// <summary>
        /// The trap was the result of cancelling a subtask after it reached a terminal state.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_SUBTASK_CANCEL_AFTER_TERMINAL</c>.</remarks>
        SubtaskCancelAfterTerminal = 36,
        /// <summary>
        /// The trap was the result of an invalid task return.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_TASK_RETURN_INVALID</c>.</remarks>
        TaskReturnInvalid = 37,
        /// <summary>
        /// The trap was the result of dropping a waitable set that still had waiters.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_WAITABLE_SET_DROP_HAS_WAITERS</c>.</remarks>
        WaitableSetDropHasWaiters = 38,
        /// <summary>
        /// The trap was the result of dropping a subtask before it resolved.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_SUBTASK_DROP_NOT_RESOLVED</c>.</remarks>
        SubtaskDropNotResolved = 39,
        /// <summary>
        /// The trap was the result of an invalid type passed to `thread.new.indirect`.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_THREAD_NEW_INDIRECT_INVALID_TYPE</c>.</remarks>
        ThreadNewIndirectInvalidType = 40,
        /// <summary>
        /// The trap was the result of an uninitialized function passed to `thread.new.indirect`.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_THREAD_NEW_INDIRECT_UNINITIALIZED</c>.</remarks>
        ThreadNewIndirectUninitialized = 41,
        /// <summary>
        /// The trap was the result of backpressure overflow.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_BACKPRESSURE_OVERFLOW</c>.</remarks>
        BackpressureOverflow = 42,
        /// <summary>
        /// The trap was the result of unsupported callback code.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_UNSUPPORTED_CALLBACK_CODE</c>.</remarks>
        UnsupportedCallbackCode = 43,
        /// <summary>
        /// The trap was the result of attempting to resume a thread that cannot be resumed.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_CANNOT_RESUME_THREAD</c>.</remarks>
        CannotResumeThread = 44,
        /// <summary>
        /// The trap was the result of concurrent future or stream operations.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_CONCURRENT_FUTURE_STREAM_OP</c>.</remarks>
        ConcurrentFutureStreamOperation = 45,
        /// <summary>
        /// The trap was the result of a reference count overflow.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_REFERENCE_COUNT_OVERFLOW</c>.</remarks>
        ReferenceCountOverflow = 46,
        /// <summary>
        /// The trap was the result of a stream operation that was too large.
        /// </summary>
        /// <remarks>Defined at <c>include/wasmtime/trap.h</c> <c>WASMTIME_TRAP_CODE_STREAM_OP_TOO_BIG</c>.</remarks>
        StreamOperationTooBig = 47
    }

    /// <summary>
    /// Represents a WebAssembly trap frame.
    /// </summary>
    [Serializable]
    public class TrapFrame
    {
        internal unsafe TrapFrame(IntPtr frame)
        {
            FunctionOffset = Native.wasm_frame_func_offset(frame);
            FunctionName = null;
            ModuleOffset = Native.wasm_frame_module_offset(frame);
            ModuleName = null;

            var bytes = Native.wasmtime_frame_func_name(frame);
            if (bytes != null && checked((int)bytes->size) > 0)
            {
                FunctionName = Encoding.UTF8.GetString(bytes->data, (int)bytes->size);
            }

            bytes = Native.wasmtime_frame_module_name(frame);
            if (bytes != null && checked((int)bytes->size) > 0)
            {
                ModuleName = Encoding.UTF8.GetString(bytes->data, (int)bytes->size);
            }
        }

        /// <summary>
        /// Gets the frame's byte offset from the start of the function.
        /// </summary>
        public nuint FunctionOffset { get; private set; }

        /// <summary>
        /// Gets the frame's function name.
        /// </summary>
        public string? FunctionName { get; private set; }

        /// <summary>
        /// Gets the frame's module offset from the start of the module.
        /// </summary>
        public nuint ModuleOffset { get; private set; }

        /// <summary>
        /// Gets the frame's module name.
        /// </summary>
        public string? ModuleName { get; private set; }

        private static class Native
        {
            [DllImport(Engine.LibraryName)]
            public static extern nuint wasm_frame_func_offset(IntPtr frame);

            [DllImport(Engine.LibraryName)]
            public static extern nuint wasm_frame_module_offset(IntPtr frame);

            [DllImport(Engine.LibraryName)]
            public static extern unsafe ByteArray* wasmtime_frame_func_name(IntPtr frame);

            [DllImport(Engine.LibraryName)]
            public static extern unsafe ByteArray* wasmtime_frame_module_name(IntPtr frame);
        }
    }

    /// <summary>
    /// Provides access to a WebAssembly trap result
    /// </summary>
    public readonly ref struct TrapAccessor
    {
        private readonly IntPtr _trap;

        internal TrapAccessor(IntPtr trap)
        {
            _trap = trap;
        }

        internal void Dispose()
        {
            TrapException.Native.wasm_trap_delete(_trap);
        }

        /// <summary>
        /// Get the TrapCode
        /// </summary>
        public TrapCode TrapCode
        {
            get
            {
                if (TrapException.Native.wasmtime_trap_code(_trap, out var code))
                {
                    return (TrapCode)code;
                }
                return TrapCode.Undefined;
            }
        }

        /// <summary>
        /// Get the message string
        /// </summary>
        public string Message
        {
            get
            {
                TrapException.Native.wasm_trap_message(_trap, out var bytes);
                using (bytes)
                {
                    unsafe
                    {
                        var byteSpan = new ReadOnlySpan<byte>(bytes.data, checked((int)bytes.size));

                        var indexOfNull = byteSpan.LastIndexOf((byte)0);
                        if (indexOfNull != -1)
                        {
                            byteSpan = byteSpan[..indexOfNull];
                        }

                        var message = Encoding.UTF8.GetString(byteSpan);
                        return message;
                    }
                }
            }
        }

        /// <summary>
        /// Copy the bytes of the message into a span
        /// </summary>
        /// <param name="output">Destination span to write bytes to</param>
        /// <param name="offset">Offset in the source span to begin copying from</param>
        /// <returns>The total length of the source span</returns>
        public int GetMessageBytes(Span<byte> output, int offset = 0)
        {
            TrapException.Native.wasm_trap_message(_trap, out var bytes);
            using (bytes)
            {
                unsafe
                {
                    var byteSpan = new ReadOnlySpan<byte>(bytes.data, checked((int)bytes.size));

                    var indexOfNull = byteSpan.LastIndexOf((byte)0);
                    if (indexOfNull != -1)
                    {
                        byteSpan = byteSpan[..indexOfNull];
                    }

                    var totalBytes = byteSpan.Length;
                    byteSpan = byteSpan[offset..];

                    if (byteSpan.Length > output.Length)
                    {
                        byteSpan[..output.Length].CopyTo(output);
                    }
                    else
                    {
                        byteSpan.CopyTo(output);
                    }

                    return totalBytes;
                }
            }
        }

        /// <summary>
        /// Get the trap frames
        /// </summary>
        /// <returns>A list of stack frames indicating where the trap occured, the first item in the list represents the innermost stack frame</returns>
        public List<TrapFrame> GetFrames()
        {
            TrapException.Native.wasm_trap_trace(_trap, out var frames);
            using (frames)
            {
                return TrapException.GetFrames(frames);
            }
        }

        /// <summary>
        /// Get a TrapException which contains all information about this trap
        /// </summary>
        /// <returns>A TrapException object which contains all the information about this trap in one convenient wrapper</returns>
        public TrapException GetException()
        {
            return TrapException.FromOwnedTrap(_trap, delete: false);
        }
    }

    /// <summary>
    /// The exception for WebAssembly traps.
    /// </summary>
    [Serializable]
    public class TrapException : WasmtimeException
    {
        /// <inheritdoc/>
        public TrapException() { }

        /// <inheritdoc/>
        public TrapException(string message) : base(message) { }

        /// <inheritdoc/>
        public TrapException(string message, Exception? inner) : base(message, inner) { }

        /// <summary>
        /// Indentifies which type of trap this is.
        /// </summary>
        public TrapCode Type { get; private set; }

        internal TrapException(string message, IReadOnlyList<TrapFrame>? frames, TrapCode type, Exception? innerException = null)
            : base(message, innerException)
        {
            Type = type;
            Frames = frames;
        }

        internal static TrapException FromOwnedTrap(IntPtr trap, bool delete = true)
        {
            var accessor = new TrapAccessor(trap);
            try
            {
                var trappedException = new TrapException(accessor.Message, accessor.GetFrames(), accessor.TrapCode);
                return trappedException;
            }
            finally
            {
                if (delete)
                    accessor.Dispose();
            }
        }

        internal static List<TrapFrame> GetFrames(Native.FrameArray frames)
        {
            int framesSize = checked((int)frames.size);
            var trapFrames = new List<TrapFrame>(framesSize);
            for (var i = 0; i < framesSize; ++i)
            {
                unsafe
                {
                    trapFrames.Add(new TrapFrame(frames.data[i]));
                }
            }

            return trapFrames;
        }

        internal new static class Native
        {
            [StructLayout(LayoutKind.Sequential)]
            public unsafe struct FrameArray : IDisposable
            {
                public nuint size;
                public IntPtr* data;

                public void Dispose()
                {
                    Native.wasm_frame_vec_delete(this);
                }
            }

            [DllImport(Engine.LibraryName)]
            public static extern void wasm_trap_message(IntPtr trap, out ByteArray message);

            [DllImport(Engine.LibraryName)]
            public static extern void wasm_trap_trace(IntPtr trap, out FrameArray frames);

            [DllImport(Engine.LibraryName)]
            public static extern void wasm_trap_delete(IntPtr trap);

            [DllImport(Engine.LibraryName)]
            public static extern void wasm_frame_vec_delete(in FrameArray vec);

            [DllImport(Engine.LibraryName)]
            [return: MarshalAs(UnmanagedType.I1)]
            internal static extern bool wasmtime_trap_code(IntPtr trap, out byte exitCode);
        }
    }
}
