namespace ModbusAdapter
{
    public static class ServiceProviderAccessor
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public static void SetServiceProvider(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
    }
}
