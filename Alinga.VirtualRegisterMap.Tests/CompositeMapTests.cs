using NUnit.Framework;
using Alinga.VirtualRegisterMap;

namespace Tests;

public class CompositeMapInserts
{
    [Test]
    public void InsertOverlappingRegion()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var block = new CompositeRegisterMap();
            block.Insert(0x0000, 0x10, addr => (byte)1);
            block.Insert(0x0005, 0x10, addr => (byte)2);
        });
    }

    [Test]
    public void InsertStraddlingRegion()
    {
        Assert.DoesNotThrow(() =>
        {
            var block = new CompositeRegisterMap();
            block.Insert(0, 4, addr => 1);
            block.Insert(4, 4, addr => 2);
            block.Insert(8, 4, addr => 3);
        });
    }
}

public class CompositeMapRead
{
    [Test]
    public void ReadFromTinyRegion()
    {
        var block = new CompositeRegisterMap();
        block.Insert(100, 1, addr => (byte)1);
        block.Insert(101, 1, addr => (byte)2);
        // gap at 102
        block.Insert(103, 1, addr => (byte)3);

        var buffer = new byte[8];

        // start at offset -1
        block.Read(99, buffer, IORequestFlags.None);

        Assert.That(buffer[0], Is.EqualTo(0xEE));
        Assert.That(buffer[1], Is.EqualTo(1));
        Assert.That(buffer[2], Is.EqualTo(2));
        Assert.That(buffer[3], Is.EqualTo(0xEE));
        Assert.That(buffer[4], Is.EqualTo(3));
        Assert.That(buffer[5], Is.EqualTo(0xEE));
        Assert.That(buffer[6], Is.EqualTo(0xEE));
        Assert.That(buffer[7], Is.EqualTo(0xEE));
    }

    [Test]
    public void ReadFromEmptyMap()
    {
        var block = new CompositeRegisterMap();
        Assert.That(block.Read<byte>(0x0000) == 0xEE);
        Assert.That(block.Read<byte>(0x123456) == 0xEE);
        Assert.That(block.Read<byte>(0xFFFFFFFF) == 0xEE);
    }
}

public class CompositeMapWrite
{
    [Test]
    public void WriteToTinyRegion()
    {
        int region0=-1;
        int region1=-1;
        int region2=-1;

        var map = new CompositeRegisterMap();
        map.Insert<byte>(100, 1, addr => 0, (a, v) => region0= v);
        map.Insert<byte>(101, 1, addr => 0, (a, v) => region1= v);
        // gap at 102
        map.Insert<byte>(103, 1, addr => 0, (a, v) => region2 = v);


        // write to the composite map
        map.Write<byte>(100, 1);
        Assert.That(region0, Is.EqualTo(1));

        map.Write<byte>(101, 2);
        Assert.That(region1, Is.EqualTo(2));

        map.Write<byte>(103, 4);
        Assert.That(region2, Is.EqualTo(4));
    }

    [Test]
    public void BulkWrite()
    {
        int region0 = -1;
        int region1 = -1;
        int region2 = -1;

        var map = new CompositeRegisterMap();
        map.Insert<byte>(100, 1, addr => 0, (a, v) => region0 = v);
        map.Insert<byte>(101, 1, addr => 0, (a, v) => region1 = v);
        // gap at 102
        map.Insert<byte>(103, 1, addr => 0, (a, v) => region2 = v);

        // write a 32 bit value to the composite map
        map.Write<UInt32>(100, 0x12345678);

        // check the individual regions
        Assert.That(region0, Is.EqualTo(0x78));
        Assert.That(region1, Is.EqualTo(0x56));
        // gap at 102
        Assert.That(region2, Is.EqualTo(0x12));
    }
}