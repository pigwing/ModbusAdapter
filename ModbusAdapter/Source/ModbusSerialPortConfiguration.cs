using System.IO.Ports;
using FluentModbus;

namespace ModbusAdapter
{
    public class ModbusSerialPortConfiguration
    {
        public string Port { get; set; } = "";
        public int BaudRate { get; set; } = 9600;
        public Parity Parity { get; set; } = Parity.Odd;
        public StopBits StopBits { get; set; } = StopBits.One;
        public ModbusEndianness ModbusEndianness { get; set; } = ModbusEndianness.BigEndian;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
    }
}
