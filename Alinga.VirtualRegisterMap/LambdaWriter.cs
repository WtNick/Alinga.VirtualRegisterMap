namespace Alinga.VirtualRegisterMap
{
    public class LambdaWriter<TContext> : IRegisterWrite<TContext>
    {
        public delegate void SetterFn(TContext ctx, uint offset, ReadOnlySpan<byte> input, IORequestFlags flags);
        readonly SetterFn setter;

        public LambdaWriter(SetterFn setter)
        {
            this.setter = setter;
        }
        public void Write(TContext ctx, uint offset, ReadOnlySpan<byte> input, IORequestFlags flags)
        {
            setter(ctx, offset, input, flags);
        }
    }
}