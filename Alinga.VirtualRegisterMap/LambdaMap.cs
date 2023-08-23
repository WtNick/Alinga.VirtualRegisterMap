using System.Runtime.InteropServices;

namespace Alinga.VirtualRegisterMap
{
    /// <summary>
    /// Virtual register map that allows read/write access through lambda getters/setters
    /// </summary>
    /// <typeparam name="T">Unmanaged type through which the map is written</typeparam>
    public class LambdaMap<T> : IRegisterMap where T : unmanaged
    {
        readonly Func<UInt32, T> getter;
        readonly Action<UInt32, T> setter;
        public LambdaMap(Func<UInt32,T> getter, Action<uint, T> setter)
        {
            this.getter = getter;
            this.setter = setter;
        }
        public LambdaMap(Func<UInt32, T> getter)
        {
            this.getter = getter;
            this.setter = dummywriter;
        }
        static void dummywriter(UInt32 addr, T value) { }

        public void Read(uint address, Span<byte> output, IORequestFlags flags)
        {
            var buf = output.Cast<T>();

            for(int i = 0;i<buf.Length;i++)
            {
                buf[i] = getter(address);
                
                if ((flags & IORequestFlags.NoAddressIncrement) == IORequestFlags.None)
                {
                    address += (UInt32)Marshal.SizeOf<T>();
                }
            }
        }

        public void Write(uint address, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            var buf = input.Cast<T>();

            for (int i = 0; i < buf.Length; i++)
            {
                setter(address, buf[i]);

                if ((flags & IORequestFlags.NoAddressIncrement) == IORequestFlags.None)
                {
                    address += (UInt32)Marshal.SizeOf<T>();
                }
            }
        }
    }

    public class LambdaMap<TContext, T> : IRegisterMap<TContext> where T : unmanaged
    {
        public delegate void SetterFn(TContext obj, uint address, T value);
        public delegate T GetterFn(TContext obj, uint address);

        readonly GetterFn getter;
        readonly SetterFn setter;
        public LambdaMap(GetterFn getter, SetterFn setter)
        {
            this.getter = getter;
            this.setter = setter;
        }
        public LambdaMap(GetterFn getter)
        {
            this.getter = getter;
            this.setter = dummywriter;
        }
        static void dummywriter(TContext context, UInt32 addr, T value) { }

        public void Read(TContext context, uint address, Span<byte> output, IORequestFlags flags)
        {
            var buf = output.Cast<T>();

            for (int i = 0; i < buf.Length; i++)
            {
                buf[i] = getter(context, address);

                if ((flags & IORequestFlags.NoAddressIncrement) == IORequestFlags.None)
                {
                    address += (UInt32)Marshal.SizeOf<T>();
                }
            }
        }

        public void Write(TContext context, uint address, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            var buf = input.Cast<T>();

            for (int i = 0; i < buf.Length; i++)
            {
                setter(context, address, buf[i]);

                if ((flags & IORequestFlags.NoAddressIncrement) == IORequestFlags.None)
                {
                    address += (UInt32)Marshal.SizeOf<T>();
                }
            }
        }
    }
}