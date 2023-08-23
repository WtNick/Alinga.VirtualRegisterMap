using NUnit.Framework;
using Alinga.VirtualRegisterMap;

namespace Tests;


public class LambdaRegMapTests
{
    [Test]
    public void ByteAddressing_Constant()
    { 
        var map = new LambdaMap<byte>(addr => 1);
        
        Assert.That(map.Read<byte>(0), Is.EqualTo(1));
        Assert.That(map.Read<UInt16>(0), Is.EqualTo(0x0101));

        Assert.That(map.Read<byte>(8), Is.EqualTo(1));
        Assert.That(map.Read<UInt16>(8), Is.EqualTo(0x0101));

    }

    [Test]
    public void ByteAddressing_LinearRamp()
    {
        var map = new LambdaMap<byte>(addr => (byte)addr);
        Assert.That(map.Read<byte>(123), Is.EqualTo(123));
        Assert.That(map.Read<byte>(456), Is.EqualTo((byte)(456 % 0x100)));
    }

    [Test]
    public void LambdaMapWrite()
    {
        var table = new Dictionary<uint, byte>();

        // Create a lamba map that writes to the table
        var map = new LambdaMap<byte>(a => 1, (a, v) => table[a] = v);

        map.Write<byte>(0, 123);
        Assert.That(table[0], Is.EqualTo(123));

        map.Write<byte>(1, 200);
        Assert.That(table[1], Is.EqualTo(200));

        map.Write<UInt32>(4, 0x12345678);
        Assert.That(table[7], Is.EqualTo(0x12));
        Assert.That(table[6], Is.EqualTo(0x34));
        Assert.That(table[5], Is.EqualTo(0x56));
        Assert.That(table[4], Is.EqualTo(0x78));

        // only 6 writes should have been made
        Assert.That(table.Count, Is.EqualTo(6));
    }
}