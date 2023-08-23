
namespace Alinga.VirtualRegisterMap;


public class RegisterAttribute : Attribute
{
    public uint Address { get; }
    public int Length { get; set; } = 4;
    public uint DefaultValue { get; set; } = 0;
    public bool ReadOnly { get; set; }
    public bool WriteOnly { get; set; }

    public RegisterAttribute(uint address, int length = 4)
    {
        this.Address = address;
        this.Length = length;
    }
}