
namespace Alinga.VirtualRegisterMap;

/// <summary>
/// String text register map reader
/// This uses the native c# utf16 string encoding, so returns (byte)char values.
/// </summary>
/// <typeparam name="TContext"></typeparam>
internal class StringReader<TContext> : IRegisterRead<TContext>
{
    readonly Func<TContext, string> getstring;
    public StringReader(Func<TContext, string> getstring)
    {
        this.getstring = getstring;
    }
    
    public void Read(TContext ctx, uint offset, Span<byte> output, IORequestFlags flags)
    {
        var text = getstring(ctx);
        int k = 0;

        int readlen = Math.Min(output.Length, (int)(text.Length - offset));
        for(int i = 0;i<readlen;i++)
        {
            output[k++] = (byte)text[(int)offset++];
        }
        while(k<output.Length)
        {
            output[k++] = 0; // null terminate
        }
    }
}