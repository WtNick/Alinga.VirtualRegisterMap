namespace Alinga.VirtualRegisterMap
{
    public class LambdaReader<TContext> : IRegisterRead<TContext>
    {
        public delegate void GetterFn(TContext ctx, uint offset, Span<byte> output, IORequestFlags flags);

        readonly GetterFn getter;

        public LambdaReader(GetterFn getter)
        {
            this.getter = getter;
        }

        public void Read(TContext ctx, uint offset, Span<byte> output, IORequestFlags flags)
        {
            getter(ctx, offset, output, flags);
        }
    }
}