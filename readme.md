# Virtual Register Map

Access objects through a register map interface.

This library provides a convenient way of modeling hardware devices in c#.

``` c#
using Alinga.VirtualRegisterMap;

public class MyClass
{
    // attach a register attribute to a property to make it accessible through a virtual register map interface
    // Memory map read/writes are automatically forwarded to the property getter/setter
    [Register(0x00)] public UInt32 AProperty { get; set; }

    // similarly we can create a 'command' register by attaching the RegisterAttribute to a method with a single argument
    [Register(0x10)] public void DoSomething(UInt32 value){
        Console.WriteLine($"something happended on register 0x10, value = {value:X8}");
    }
}

```


To get the register map interface, simply call `CreateFromObject` on the instance
``` c#
var myclass = new MyClass();
IRegisterMap map = RegisterMapBuilder.CreateFromObject(myclass);
```

Access the object through the map is easy:
``` c#
map.Write<UInt32>(0x00, 0x12345678);

var bytevalue = map.Read<byte>(0x00);

// read a 16 byte buffer from offset 0x00
Span<byte> buffer = new byte[16];
map.Read(0x00, buffer, IORequestFlags.None);
```


## Example:
Below is a slightly more elaborate example of a composed object.

``` c#
using Alinga.VirtualRegisterMap;

namespace Example;

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
        var map = RegisterMapBuilder.CreateFromObject(system);

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
```