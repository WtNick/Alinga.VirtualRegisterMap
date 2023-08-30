using NUnit.Framework;
using Alinga.VirtualRegisterMap;

namespace Tests;

public class MapLoader
{
    class TestClass<TData> where TData : unmanaged
    {
        [Register(0x00, length: 8)] public TData Value { get; set; }
    }

    private void checkproperty<T>(T testvalue) where T : unmanaged
    {
        var tc = new TestClass<T>();
        var map = RegisterMapBuilder.CreateFromObject(tc);

        map.Write(0x00, testvalue);
        var mapreadback = map.Read<T>(0x00);

        Assert.That(tc.Value, Is.EqualTo(testvalue));
        Assert.That(mapreadback, Is.EqualTo(testvalue));
    }

    [Test] public void CheckProperty_UInt32() => checkproperty<UInt32>(0x12345678);

    [Test] public void CheckProperty_Int32() => checkproperty<int>(0x12345678);

    [Test] public void CheckProperty_Byte() => checkproperty<byte>(123);
    [Test] public void CheckProperty_SByte() => checkproperty<sbyte>(-123);

    [Test] public void CheckProperty_UInt64() => checkproperty<UInt64>(0x1234567890ABCDEF);
    [Test] public void CheckProperty_Int64() => checkproperty<Int64>(0x1234567890ABCDEF);


}

public class NonGenericMapLoader
{
    class TestClass
    {
        [Register(0x00, length: 1)] public byte Value { get; set; }
    }

    [Test] public void CheckProperty()
    {
        var tc = new TestClass();
        var map = RegisterMapBuilder.CreateFromObject(typeof(TestClass), tc);

        var testvalue = 0x01;

        map.Write(0x00, testvalue);
        var mapreadback = map.Read<byte>(0x00);

        Assert.That(tc.Value, Is.EqualTo(testvalue));
        Assert.That(mapreadback, Is.EqualTo(testvalue));
    }


}
