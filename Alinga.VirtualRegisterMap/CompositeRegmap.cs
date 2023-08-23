
namespace Alinga.VirtualRegisterMap
{

    public static class RegIODiagnostics
    {
        public static int UnmappedAccessWriteCounter = 0;
        public static int UnmappedAccessReadCounter = 0;
    }

    /// <summary>
    /// Base class for both <see cref="CompositeRegisterMap{TBlockContent, TContext}"/> and <see cref="CompositeRegisterMap"/>
    /// </summary>
    /// <typeparam name="TBlockContent"></typeparam>
    /// <typeparam name="TContext"></typeparam>
    internal class RegisterMapCompositor<TBlockContent, TContext> where TBlockContent : class
    {
        protected class Block
        {
            public UInt32 Address { get; }
            public int Size { get; }

            /// <summary>
            /// The mapped memory on this address bllock
            /// </summary>
            public TBlockContent Content { get; }

            /// <summary>
            /// Exclusive end address
            /// </summary>
            public UInt32 Top => (UInt32)(Address + Size);

            /// <summary>
            /// Check if an address is contained in this block
            /// </summary>
            /// <param name="addres"></param>
            /// <returns></returns>
            public bool Contains(UInt32 addres) => (addres >= Address) && (addres < Top);
            public bool OverLapsWith(Block other)
            {
                if (other.Address >= Top) return false; // other lies after this block
                if (other.Top <= Address) return false; // other lies before this block
                return true;
            }

            public Block(UInt32 address, int size, TBlockContent content)
            {
                this.Address = address;
                this.Size = size;
                this.Content = content;
            }
        }
        protected List<Block> blocks = new();

        /// <summary>
        /// Find the block index of the block which address is less or equal to the specified address.
        /// Returns false if no such block exists
        /// </summary>
        /// <param name="address"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        protected bool TryFindBlockIndex(UInt32 address, out int index)
        {
            // binary search for the block index that is less or equal to the address
            int L = 0;
            int R = blocks.Count;
            while (L < R)
            {
                var m = (L + R) / 2;
                if (blocks[m].Address > address)
                {
                    R = m;
                }
                else
                {
                    L = m + 1;
                }
            }
            index = L - 1;
            return index >= 0;
        }

        /// <summary>
        /// Insert a new register map at the specified address range
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="getter"></param>
        public void Insert(UInt32 address, int length, TBlockContent content)
        {
            if (length <= 0) throw new ArgumentException("Block length should be >0");

            var block = new Block(address, length, content);

            if (TryFindBlockIndex(address, out var index))
            {
                var b = blocks[index];
                if (b.OverLapsWith(block))
                {
                    throw new InvalidOperationException($"New address block collides with existing range [{b.Address:X8}:{(UInt32)(b.Address + b.Size):X8}]");
                }
            }

            blocks.Add(block);
            // sort in place. Dont mind this is done every time a block is added. Adding blocks is not a common operation
            blocks.Sort((m1, m2) => Comparer<UInt32>.Default.Compare(m1.Address, m2.Address));
        }

        int GetUnmappedProcessLength(int blockindex, UInt32 address, int length)
        {
            if (blockindex >= blocks.Count)
            {
                // end of blocks
                return length;
            }
            return (int)Math.Min(blocks[blockindex].Address - address, length);
        }

        public IEnumerable<(TBlockContent? content, UInt32 sectionoffset, int length)> GetBlocks(UInt32 address, int length)
        {
            if (!TryFindBlockIndex(address, out int index))
            {
                // starting read in unmapped region
                // calculate the amount of unmapped region until the first mapped block
                var processsize = GetUnmappedProcessLength(0, address, length);
                yield return ((null, 0, (int)processsize));
                address = (UInt32)(address + processsize);
                length -= (int)processsize;

                // continue with first block
                index = 0;
            }

            while (length > 0)
            {
                var b = blocks[index];
                if (address < b.Top)
                {
                    // we still have some data to process in this block
                    var lengthtoendofsection = b.Top - address;
                    var processlength = Math.Min(lengthtoendofsection, length);
                    
                    yield return (b.Content, address - b.Address, (int)processlength);

                    length-= (int)processlength;
                    address = (UInt32)(address + processlength);
                }
                index++;
                {
                    var processlength = GetUnmappedProcessLength(index, address, length);
                    if (processlength > 0)
                    {
                        yield return (null, 0, (int)processlength);

                        length -= (int)processlength;
                        address = (UInt32)(address + processlength);
                    }
                }
            }
        }
    }

    internal abstract class CompositeRegisterMapReaderBase<TBlockContent,TContext> : RegisterMapCompositor<TBlockContent, TContext>, IRegisterRead<TContext> where TBlockContent : class
    {
        protected abstract void Contentread(TContext context, TBlockContent content, UInt32 offset, Span<byte> output, IORequestFlags flags);

        public const byte ErrorReadValue = 0xEE;
        static void UnmappedRead(Span<byte> output)
        {
            // fill with error value
            output.Fill(ErrorReadValue);
            Interlocked.Increment(ref RegIODiagnostics.UnmappedAccessReadCounter);

        }

        public void Read(TContext context, uint address, Span<byte> output, IORequestFlags flags)
        {
            foreach(var (block, offset, length) in GetBlocks(address,output.Length))
            {
                if (block == null)
                {
                    // unmapped read
                    UnmappedRead(output[..length]);
                }
                else
                {
                    // mapped read
                    Contentread(context, block, offset, output[..length], flags);
                }
                output = output[length..];
            }
        }
    }

    internal abstract class CompositeRegisterMapWriterBase<TBlockContent, TContext> : RegisterMapCompositor<TBlockContent, TContext>, IRegisterWrite<TContext> where TBlockContent : class
    {
        protected abstract void Contentwrite(TContext context, TBlockContent content, UInt32 offset, ReadOnlySpan<byte> input, IORequestFlags flags);

        static void UnmappedWrite(uint address, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            Interlocked.Increment(ref RegIODiagnostics.UnmappedAccessWriteCounter);
        }
       
        public void Write(TContext context, uint address, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            foreach (var (block, offset, length) in GetBlocks(address, input.Length))
            {
                if (block == null)
                {
                    // unmapped write
                    UnmappedWrite(offset, input[..length], flags);
                }
                else
                {
                    // mapped write
                    Contentwrite(context, block, offset, input[..length], flags);
                }
                input = input[length..];
            }
        }
    }


    internal class DummyContext { }

    
    internal class CompositeRegisterMapReader : CompositeRegisterMapReaderBase<IRegisterRead, DummyContext?>
    {
        protected override void Contentread(DummyContext? _, IRegisterRead content, UInt32 offset, Span<byte> output, IORequestFlags flags)
        {
            content.Read(offset, output, flags);
        }
    }

    internal class CompositeRegisterMapReader<TContext> : CompositeRegisterMapReaderBase<IRegisterRead<TContext>, TContext>
    {
        protected override void Contentread(TContext context, IRegisterRead<TContext> content, UInt32 offset, Span<byte> output, IORequestFlags flags)
        {
            content.Read(context, offset, output, flags);
        }
    }

    internal class CompositeRegisterMapWriter : CompositeRegisterMapWriterBase<IRegisterWrite, DummyContext?>
    {
        protected override void Contentwrite(DummyContext? _, IRegisterWrite content, UInt32 offset, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            content.Write(offset, input, flags);
        }
    }

    internal class CompositeRegisterMapWriter<TContext> : CompositeRegisterMapWriterBase<IRegisterWrite<TContext>, TContext>
    {
        protected override void Contentwrite(TContext context, IRegisterWrite<TContext> content, UInt32 offset, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            content.Write(context, offset, input, flags);
        }
    }

    /// <summary>
    /// A Register map that is a composition of multiple other register maps, each in their own address range
    /// </summary>
    public class CompositeRegisterMap : IRegisterMap
    {
        readonly CompositeRegisterMapReader readmap = new ();
        readonly CompositeRegisterMapWriter writemap = new ();

        /// <summary>
        /// Insert a register map at the specified address range
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="map"></param>
        public void Insert(UInt32 address, int length, IRegisterMap map)
        {
            readmap.Insert(address, length, map);
            writemap.Insert(address, length, map);
        }

        public void Insert(UInt32 address, int length, IRegisterRead map)
        {
            readmap.Insert(address, length, map);
        }
        public void Insert(UInt32 address, int length, IRegisterWrite map)
        {
            writemap.Insert(address, length, map);
        }


        /// <summary>
        /// Insert a new register map at the specified address range
        /// The mapping is derived from the context using properties and command that have the <see cref="RegisterAttribute"/> attribte
        /// </summary>
        /// <typeparam name="TContext"></typeparam>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="context"></param>
        public void InsertContext<TContext>(UInt32 address, int length, TContext context)
        {
            Insert(address, length, ContextMapLoader<TContext>.Map.Capture(context));
        }

        /// <summary>
        /// Insert a new register map at the specified address range.
        /// The register map handler is a lambda function that returns the value to be read
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="getter"></param>
        public void Insert<T>(UInt32 address, int length, Func<UInt32, T> getter) where T : unmanaged
        {
            Insert(address, length, new LambdaMap<T>(getter));
        }

        /// <summary>
        /// Insert a new register map at the specified address range.
        /// The map is created from a getter and setter lambda functions
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        public void Insert<T>(UInt32 address, int length, Func<UInt32, T> getter, Action<UInt32, T> setter) where T : unmanaged
        {
            Insert(address, length, new LambdaMap<T>(getter, setter));
        }

        public void Read(uint offset, Span<byte> output, IORequestFlags flags)
        {
            readmap.Read(null, offset, output, flags);
        }

        public void Write(uint offset, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            writemap.Write(null, offset, input, flags);
        }
    }

    /// <summary>
    /// Composite register map that has seperate read and write maps
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public class CompositeRegisterMap<TContext> : IRegisterMap<TContext>
    {
        readonly CompositeRegisterMapReader<TContext> readmap = new();
        readonly CompositeRegisterMapWriter<TContext> writemap = new();
        
        public void Read(TContext ctx, uint offset, Span<byte> output, IORequestFlags flags)
        {
            readmap.Read(ctx, offset, output, flags);
        }

        public void Write(TContext ctx, uint offset, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            writemap.Write(ctx, offset, input, flags);
        }

        /// <summary>
        /// Insert a register map at the specified address range
        /// </summary>
        /// <param name="address"></param>
        /// <param name="length"></param>
        /// <param name="map"></param>
        public void Insert(UInt32 address, int length, IRegisterMap<TContext> map)
        {
            readmap.Insert(address, length, map);
            writemap.Insert(address, length, map);
        }

        public void Insert(UInt32 address, int length, IRegisterRead<TContext> map)
        {
            readmap.Insert(address, length, map);
        }
        public void Insert(UInt32 address, int length, IRegisterWrite<TContext> map)
        {
            writemap.Insert(address, length, map);
        }
    }
}