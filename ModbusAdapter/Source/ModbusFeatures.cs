using DotNext.Threading;

namespace ModbusAdapter
{
    public class ModbusFeatures
    {
        readonly AsyncExclusiveLock _locker = new AsyncExclusiveLock();
        public AsyncExclusiveLock Locker => _locker;
    }
}
