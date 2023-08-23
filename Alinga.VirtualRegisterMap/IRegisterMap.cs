using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


namespace Alinga.VirtualRegisterMap
{
    [Flags]
    public enum IORequestFlags : uint
    {
        None = 0,
        NoAddressIncrement = 1
    }

    public interface IRegisterRead
    {
        void Read(UInt32 offset, Span<byte> output, IORequestFlags flags);
    }

    public interface IRegisterWrite
    {
        void Write(UInt32 offset, ReadOnlySpan<byte> input, IORequestFlags flags);
    }

    public interface IRegisterMap : IRegisterRead, IRegisterWrite
    {
    }


    public interface IRegisterRead<TContext>
    {
        void Read(TContext ctx, UInt32 offset, Span<byte> output, IORequestFlags flags);
    }

    public interface IRegisterWrite<TContext>
    {
        void Write(TContext ctx, UInt32 offset, ReadOnlySpan<byte> input, IORequestFlags flags);
    }

    public interface IRegisterMap<TContext> : IRegisterRead<TContext>, IRegisterWrite<TContext>
    {
    }


    public static class IRegisterMapExtensions
    {
        public static void Write<T>(this IRegisterWrite map, UInt32 address, in T value) where T : unmanaged
        {
            map.Write(address, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(value), 1)), IORequestFlags.None);
        }
        public static T Read<T>(this IRegisterRead map, UInt32 address) where T : unmanaged
        {
            T result = default;
            var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(result), 1));
            map.Read(address, span, IORequestFlags.None);
            return result;
        }

        public static void Read<T>(this IRegisterRead map, UInt32 address, ref T result) where T : unmanaged
        {
            var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));
            map.Read(address, span, IORequestFlags.None);
        }

        public static void Write<TContext, T>(this IRegisterWrite<TContext> map, TContext context, UInt32 address, in T value) where T : unmanaged
        {
            map.Write(context, address, MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(value), 1)), IORequestFlags.None);
        }

        public static T Read<TContext, T>(this IRegisterRead<TContext> map, TContext context, UInt32 address) where T : unmanaged
        {
            T result = default;
            var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(result), 1));
            map.Read(context, address, span, IORequestFlags.None);
            return result;
        }

        public static void Read<TContext, T>(this IRegisterRead<TContext> map, TContext context, UInt32 address, ref T result) where T : unmanaged
        {
            var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));
            map.Read(context, address, span, IORequestFlags.None);
        }


        /// <summary>
        /// Helper class.
        /// This class exposes a normal IRegisterMap interface by capturing the context.
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        private class CapturedContextRegisterMap<TContext> : IRegisterMap
        {
            TContext context { get; }
            IRegisterMap<TContext> map { get; }
            public CapturedContextRegisterMap(IRegisterMap<TContext> map, TContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }
                this.map = map;
                this.context = context;
                
            }

            public void Read(uint offset, Span<byte> output, IORequestFlags flags) => map.Read(context, offset, output, flags);

            public void Write(uint offset, ReadOnlySpan<byte> input, IORequestFlags flags) => map.Write(context, offset, input, flags);
        }

        

        private class CapturedContextRead<TContext> : IRegisterRead
        {
            TContext context { get; }
            IRegisterRead<TContext> map { get; }
            public CapturedContextRead(IRegisterRead<TContext> map, TContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }
                this.map = map;
                this.context = context;
            }
            public void Read(uint offset, Span<byte> output, IORequestFlags flags) => map.Read(context, offset, output, flags);
        }
        private class CapturedContextWrite<TContext> : IRegisterWrite
        {
            TContext context { get; }
            IRegisterWrite<TContext> map { get; }
            public CapturedContextWrite(IRegisterWrite<TContext> map, TContext context)
            {
                if (context == null)
                {
                    throw new ArgumentNullException(nameof(context));
                }
                this.map = map;
                this.context = context;
            }
            public void Write(uint offset, ReadOnlySpan<byte> input, IORequestFlags flags) => map.Write(context, offset, input, flags);
        }

        /// <summary>
        /// Convert a context based map into a normal <see cref="IRegisterMap"/> by capturing the context
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="map"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IRegisterMap Capture<TContext>(this IRegisterMap<TContext> map, TContext context) => new CapturedContextRegisterMap<TContext>(map, context);

        /// <summary>
        /// Convert a context based register read into a normal <see cref="IRegisterRead"/> by capturing the context
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="map"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IRegisterRead Capture<TContext>(this IRegisterRead<TContext> map, TContext context) => new CapturedContextRead<TContext>(map, context);

        /// <summary>
        /// Convert a context based register write into a normal <see cref="IRegisterWrite"/> by capturing the context
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="map"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IRegisterWrite Capture<TContext>(this IRegisterWrite<TContext> map, TContext context) => new CapturedContextWrite<TContext>(map, context);

        private class ReadonlyMap<TContext> : IRegisterMap<TContext>
        {
            readonly IRegisterRead<TContext> reader;
            public ReadonlyMap(IRegisterRead<TContext> reader) { this.reader = reader; }
            public void Read(TContext ctx, uint offset, Span<byte> output, IORequestFlags flags) => reader.Read(ctx, offset, output, flags);
            public void Write(TContext ctx, uint offset, ReadOnlySpan<byte> input, IORequestFlags flags) { }
        }
        private class ReadonlyMap : IRegisterMap
        {
            readonly IRegisterRead reader;
            public ReadonlyMap(IRegisterRead reader) { this.reader = reader; }
            public void Read(uint offset, Span<byte> output, IORequestFlags flags) => reader.Read(offset, output, flags);
            public void Write(uint offset, ReadOnlySpan<byte> input, IORequestFlags flags) { }
        }

        /// <summary>
        /// Convert a context aware reader to a readonly register map
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static IRegisterMap<TContext> ToReadOnlyMap<TContext>(this IRegisterRead<TContext> reader) => new ReadonlyMap<TContext>(reader);

        /// <summary>
        /// Convert a register reader to a readonly register map
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static IRegisterMap ToReadOnlyMap(this IRegisterRead reader) => new ReadonlyMap(reader);

        private class WriteonlyMap<TContext> : IRegisterMap<TContext>
        {
            readonly IRegisterWrite<TContext> writer;
            readonly byte defaultread;
            public WriteonlyMap(IRegisterWrite<TContext> writer, byte defaultread = 0x00) { this.writer = writer; this.defaultread = defaultread; }
            public void Write(TContext ctx, uint offset, ReadOnlySpan<byte> input, IORequestFlags flags) => writer.Write(ctx, offset, input, flags);
            public void Read(TContext ctx, uint offset, Span<byte> output, IORequestFlags flags)
            {
                output.Fill(defaultread);
            }
        }
        private class WriteonlyMap : IRegisterMap
        {
            readonly IRegisterWrite writer;
            readonly byte defaultread;
            public WriteonlyMap(IRegisterWrite writer, byte defaultread = 0x00) { this.writer = writer; this.defaultread = defaultread; }
            public void Write(uint offset, ReadOnlySpan<byte> input, IORequestFlags flags) => writer.Write(offset, input, flags);
            public void Read(uint offset, Span<byte> output, IORequestFlags flags)
            {
                output.Fill(defaultread);
            }
        }

        /// <summary>
        /// Convert a context aware register reader to a WriteOnly register map;
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="writer"></param>
        /// <returns></returns>
        public static IRegisterMap<TContext> ToWriteOnlyMap<TContext>(this IRegisterWrite<TContext> writer) => new WriteonlyMap<TContext>(writer);

        /// <summary>
        /// Convert a contextless register reader to a WriteOnly map
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="defaultreadvalue">Default return value if read from this map</param>
        /// <returns></returns>
        public static IRegisterMap ToWriteOnlyMap(this IRegisterWrite writer, byte defaultreadvalue = 0x00) => new WriteonlyMap(writer, defaultreadvalue);

    }
}