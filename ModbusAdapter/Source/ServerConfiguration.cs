namespace ModbusAdapter
{
    public class ServerConfiguration
    {
        public string Ip { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 502;
        public IList<byte>? Stations { get; set; }
        public IList<ModbusReadBuffConfiguration>? ReadBuffs { get; set; }
        public int ReadBuffsDelay { get; set; } = 1000;
    }
}
