using System.Text.Json.Serialization;

namespace ModbusAdapter
{
    [JsonConverter(typeof(JsonStringEnumConverter<Function>))]
    public enum Function
    {
        Coil,
        HoldingRegister
    }
    public class ModbusReadBuffConfiguration
    {
        public Function Function { get; set; }
        public byte Station { get; set; }
        public ushort StartAddress { get; set; }
        public ushort Length { get; set; }
    }
}
