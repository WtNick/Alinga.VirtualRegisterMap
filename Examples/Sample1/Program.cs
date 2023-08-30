using Alinga.VirtualRegisterMap;

namespace Examples;

public class MyComponent
{
    [Register(0x00)] public int A { get; set; }
    [Register(0x08)] public void Test(UInt32 x) => Console.Write(x);
    [Register(0x10, 16)] public string Name { get; }

    public MyComponent(string name)
    {
        Name = name;
    }
}


public class MySystem
{

    [Register(0x2000, length: 1)] public byte A { get; set; }
    [Register(0x2001, length: 1)] public byte A2 { get; set; }
    //
    [Register(0x2002, length: 1)] public byte A3 { get; set; }
    [Register(0x2003, length: 1)] public byte A4 { get; set; }
    [Register(0x2010)] public Int16 B { get; set; }
    [Register(0x2020)] public Int32 C { get; set; }
    [Register(0x2030)] public Int64 D { get; set; }

    [Register(0, length: 1)]
    public void Doit(byte A)
    {

    }
    [Register(1, length: 1)]
    public void Doit2(byte A)
    {

    }
}


/// <summary>
/// A reusable sub module
/// </summary>
public class MyModule
{
    // property mapped to address 0x00. Default register length = 4 bytes
    [Register(0x00)] public UInt32 AProperty { get; set; }

    // registers can have any length. This is a 1 byte register
    [Register(0x04, length:1)] public byte SingleByte { get; set; }

}

/// <summary>
/// A emulated device that has its own registers, and composed of two modules
/// </summary>
public class MyDevice
{
    [Register(0x00)] public UInt32 Reg0 { get; set; }
    [Register(0x04)] public UInt32 Reg1 { get; set; }
    [Register(0x08)] public UInt32 Reg2 { get; set; }
    
    // map moduleA to offset 0x100
    [Register(0x100)] public MyModule ModuleA { get; } = new MyModule();

    // map moduleB to offset 0x200
    [Register(0x200)] public MyModule ModuleB { get; } = new MyModule();
}


internal class Program
{
    static void Main(string[] args)
    {
        // create an instance of my device
        var system = new MyDevice();

        // create a register map interface to this device
        var map = RegisterMapBuilder.CreateFromObject(typeof(MyDevice), system);

        var map2 = RegisterMapBuilder.CreateFromObject(typeof(MyDevice), system);

        // write to moduleA
        map.Write<UInt32>(0x100, 0x12345678);

        // write to moduleB
        map.Write<UInt32>(0x200, 0xAABBCCDD);

        // create 12 byte buffer with values 0..11
        var buffer = Enumerable.Range(0, 4 * 3).Select(i => (byte)i).ToArray();

        // bulk write Reg0..2
        map.Write(0x00, buffer, IORequestFlags.None);
    }
}