using System.Runtime.InteropServices;

namespace Alinga.VirtualRegisterMap;

internal static class SpanExt
{
    public static Span<T> Slice<T>(this T[] arr, int start, int length)
    {
        return new Span<T>(arr, start, length);
    }

    public static Span<TTo> Cast<TTo>(this Span<byte> input) where TTo : unmanaged
    {
        return MemoryMarshal.Cast<byte, TTo>(input);
    }
    public static ReadOnlySpan<TTo> Cast<TTo>(this ReadOnlySpan<byte> input) where TTo : unmanaged
    {
        return MemoryMarshal.Cast<byte, TTo>(input);
    }
}