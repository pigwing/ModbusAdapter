using System.Linq;
using System.Runtime.InteropServices;
using FluentModbus;

namespace ModbusAdapter
{
    public class ModbusScanHostedService(ServerConfiguration serverConfiguration, ModbusTcpServer modbusTcpServer, ModbusRtuClient modbusRtuClient, ModbusFeatures modbusFeatures, IServiceProvider serviceProvider) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                //using var scope = serviceScopeFactory.CreateScope();
                //ServerConfiguration serverConfiguration =
                //    scope.ServiceProvider.GetRequiredService<ServerConfiguration>();
                //ModbusRtuClient modbusRtuClient = scope.ServiceProvider.GetRequiredService<ModbusRtuClient>();
                //ModbusTcpServer modbusTcpServer = scope.ServiceProvider.GetRequiredService<ModbusTcpServer>();

                if (serverConfiguration.ReadBuffs != null)
                {
                    foreach (ModbusReadBuffConfiguration modbusReadBuffConfiguration in serverConfiguration.ReadBuffs)
                    {
                        switch (modbusReadBuffConfiguration.Function)
                        {
                            case Function.Coil:
                            {

                                try
                                {
                                    Memory<byte> coils;
                                    await modbusFeatures.Locker.AcquireAsync(CancellationToken.None);
                                    try
                                    {
                                        coils = await modbusRtuClient.ReadCoilsAsync(
                                                        modbusReadBuffConfiguration.Station,
                                                        modbusReadBuffConfiguration.StartAddress,
                                                        modbusReadBuffConfiguration.Length,
                                                        default);
                                    }
                                    finally
                                    {
                                        modbusFeatures.Locker.Release();
                                    }


                                    for (var i = 0; i < modbusReadBuffConfiguration.Length; i++)
                                    {
                                        byte b = coils.Span[i / 8];
                                        int bit = i % 8;
                                        bool value = (b & (1 << bit)) != 0;

                                        WriteCoil(
                                            modbusTcpServer.GetCoils(modbusReadBuffConfiguration.Station),
                                            value,
                                            (ushort)(modbusReadBuffConfiguration.StartAddress + i));

                                    }
                                }
                                catch (Exception ex)
                                {
                                    ILogger logger =
                                        serviceProvider.GetRequiredKeyedService<ILogger>("serverLogger");
                                    logger.LogError(
                                        $"Station: {modbusReadBuffConfiguration.Station}, StartingAddress: {modbusReadBuffConfiguration.StartAddress}, Length: {modbusReadBuffConfiguration.Length}");
                                    logger.LogError(ex.Message);
                                }


                            }

                                break;
                            case Function.HoldingRegister:
                            {
                                

                                try
                                {
                                    await modbusFeatures.Locker.AcquireAsync(CancellationToken.None);

                                    Memory<byte> registers;
                                    try
                                    {
                                        registers = await modbusRtuClient.ReadHoldingRegistersAsync(
                                                            modbusReadBuffConfiguration.Station,
                                                            modbusReadBuffConfiguration.StartAddress,
                                                            modbusReadBuffConfiguration.Length,
                                                            default);
                                    }
                                    finally
                                    {
                                        modbusFeatures.Locker.Release();
                                    }


                                    WriteRegister(
                                        modbusTcpServer.GetHoldingRegisters(modbusReadBuffConfiguration.Station),
                                        MemoryMarshal.Cast<byte, short>(registers.Span),
                                        modbusReadBuffConfiguration.StartAddress);
                                }
                                catch (Exception ex)
                                {
                                    ILogger logger =
                                        serviceProvider.GetRequiredKeyedService<ILogger>("serverLogger");
                                    logger.LogError(
                                        $"Station: {modbusReadBuffConfiguration.Station}, StartingAddress: {modbusReadBuffConfiguration.StartAddress}, Length: {modbusReadBuffConfiguration.Length}");
                                    logger.LogError(ex.Message);
                                    logger.LogError(ex.StackTrace);
                                }
                              


                            }

                                break;
                            default:
                            {
                                ILogger logger =
                                    serviceProvider.GetRequiredKeyedService<ILogger>("serverLogger");
                                logger.LogError($"Function not found: {modbusReadBuffConfiguration.Function.ToString()}");
                            }

                                break;
                        }
                    }
                }


                await Task.Delay(serverConfiguration.ReadBuffsDelay, stoppingToken);
            }
        }

        void WriteRegister(Span<short> registers, Span<short> writeData, ushort startAddress)
        {
            writeData.CopyTo(registers[startAddress..]);
        }

        void WriteCoil(Span<byte> coils, bool value, ushort outputAddress)
        {
            var bufferByteIndex = outputAddress / 8;
            var bufferBitIndex = outputAddress % 8;

            var oldValue = coils[bufferByteIndex];
            var newValue = oldValue;

            if (value)
                newValue |= (byte)(1 << bufferBitIndex);

            else
                newValue &= (byte)~(1 << bufferBitIndex);

            coils[bufferByteIndex] = newValue;

        }

    }
}
