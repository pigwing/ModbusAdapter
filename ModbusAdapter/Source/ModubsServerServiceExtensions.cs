using System.Net;
using FluentModbus;
using Microsoft.Extensions.Hosting;

namespace ModbusAdapter
{
    public static class ModubsServerServiceExtensions
    {
        public static IServiceCollection AddModbusServer(this IServiceCollection services)
        {
            ConfigurationHelper serverConfigurationHelper = new ConfigurationHelper();
            ServerConfiguration? serverConfiguration = serverConfigurationHelper.Load<ServerConfiguration>("server");
            serverConfiguration ??= new ServerConfiguration();
            services.AddSingleton(serverConfiguration);

            /* create logger */
            var loggerFactory = LoggerFactory.Create(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Error);
                loggingBuilder.AddConsole();
            });

            var serverLogger = loggerFactory.CreateLogger("Server");
            services.AddKeyedSingleton("serverLogger", serverLogger);
            /* create Modbus TCP server */
            var server = new ModbusTcpServer(serverLogger)
            {
                // see 'RegistersChanged' event below
                EnableRaisingEvents = true,
                //AlwaysRaiseChangedEvent = true
            };

            if (serverConfiguration.Stations != null)
            {
                foreach (byte unit in serverConfiguration.Stations)
                {
                    server.AddUnit(unit);
                }
            }

            

            /* subscribe to the 'RegistersChanged' event (in case you need it) */
            server.RegistersChanged += async (sender, registerAddresses) =>
            {
                // the variable 'registerAddresses' contains a list of modified register addresses
                if (sender is ModbusTcpServer modbusTcpServer)
                {
                    ModbusRtuClient? modbusRtuClient =
                        ServiceProviderAccessor.ServiceProvider?.GetRequiredService<ModbusRtuClient>();
                    ModbusFeatures? modbusFeatures = ServiceProviderAccessor.ServiceProvider
                                                                            ?.GetRequiredService<ModbusFeatures>();

                    int startingAddress = registerAddresses.Registers[0];
                    int quantityOfRegisters = registerAddresses.Registers.Length;

                    if (modbusRtuClient != null && registerAddresses.Registers.Length > 0 && modbusFeatures != null)
                    {
                        var bytes = modbusTcpServer.GetHoldingRegisterBuffer(registerAddresses.UnitIdentifier)
                                                   .Slice(startingAddress * 2, quantityOfRegisters * 2).ToArray();


                        await modbusFeatures.Locker.AcquireAsync(CancellationToken.None);

                        try
                        {
                            if (bytes.Length > 2)
                            {
                                await modbusRtuClient.WriteMultipleRegistersAsync(
                                    registerAddresses.UnitIdentifier,
                                    (ushort)startingAddress,
                                    bytes);
                            }
                            else
                            {
                                await modbusRtuClient.WriteSingleRegisterAsync(
                                    registerAddresses.UnitIdentifier,
                                    (ushort)startingAddress,
                                    bytes);
                            }
                        }
                        catch (Exception ex)
                        {
                            ILogger? logger =
                                ServiceProviderAccessor.ServiceProvider?.GetRequiredKeyedService<ILogger>(
                                    "serverLogger");
                            logger?.LogError(
                                $"Station: {registerAddresses.UnitIdentifier}, StartingAddress: {startingAddress}, Bytes: {string.Join(',', bytes)}");
                            logger?.LogError(ex.Message);
                        }
                        finally
                        {
                            modbusFeatures.Locker.Release();
                        }

                    }
                }
            };

            server.CoilsChanged += async (sender, coilAddresses) =>
            {

                if (sender is ModbusTcpServer modbusTcpServer)
                {
                    ModbusRtuClient? modbusRtuClient =
                        ServiceProviderAccessor.ServiceProvider?.GetRequiredService<ModbusRtuClient>();
                    ModbusFeatures? modbusFeatures = ServiceProviderAccessor.ServiceProvider
                                                                            ?.GetRequiredService<ModbusFeatures>();

                    if (modbusRtuClient != null && coilAddresses.Coils.Length > 0 && modbusFeatures != null)
                    {
                        int startingAddress = coilAddresses.Coils[0];
                        int quantityOfCoils = coilAddresses.Coils.Last() - startingAddress + 1;
                        var byteCount = (byte)Math.Ceiling((double)quantityOfCoils / 8);

                        var targetBuffer = new byte[byteCount];

                        for (int i = 0; i < quantityOfCoils; i++)
                        {
                            var sourceByteIndex = (startingAddress + i) / 8;
                            var sourceBitIndex = (startingAddress + i) % 8;

                            var targetByteIndex = i / 8;
                            var targetBitIndex = i % 8;

                            var isSet = (modbusTcpServer.GetCoilBuffer(coilAddresses.UnitIdentifier)[sourceByteIndex] &
                                         (1 << sourceBitIndex)) >
                                        0;

                            if (isSet)
                                targetBuffer[targetByteIndex] |= (byte)(1 << targetBitIndex);
                        }

                        foreach (byte b in targetBuffer)
                        {
                            var coils = b.ToString("b8").Select(e => e != '0').ToArray();
                            Array.Reverse(coils);

                            await modbusFeatures.Locker.AcquireAsync(CancellationToken.None);

                            try
                            {
                                if (quantityOfCoils > 1)
                                {
                                    modbusRtuClient.WriteMultipleCoilsAsync(
                                        coilAddresses.UnitIdentifier,
                                        startingAddress,
                                        coils.AsSpan().Slice(0, quantityOfCoils).ToArray());
                                }
                                else
                                {
                                    await modbusRtuClient.WriteSingleCoilAsync(
                                        coilAddresses.UnitIdentifier,
                                        startingAddress,
                                        coils[0]);
                                }

                            }
                            catch (Exception ex)
                            {
                                ILogger? logger =
                                    ServiceProviderAccessor.ServiceProvider
                                                           ?.GetRequiredKeyedService<ILogger>("serverLogger");
                                logger?.LogError(
                                    $"Station: {coilAddresses.UnitIdentifier}, StartingAddress: {startingAddress}, Coils: {string.Join(',', coils)}");
                                logger?.LogError(ex.Message);
                            }
                            finally
                            {
                                modbusFeatures.Locker.Release();
                            }


                        }

                    }
                }

            };

            IPAddress.TryParse(serverConfiguration.Ip, out IPAddress? ipAddress);
            ipAddress ??= IPAddress.Parse("127.0.0.1");
            server.Start(new IPEndPoint(ipAddress, serverConfiguration.Port));
            serverLogger.LogInformation("Server started.");

            services.AddSingleton(
            sp =>
            {
                    IHostApplicationLifetime hostApplicationLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
                    hostApplicationLifetime.ApplicationStopping.Register(
                        () =>
                        {
                            server.Dispose();
                        });
                    return server;
                });
            
            ConfigurationHelper configurationHelper = new ConfigurationHelper();
            ModbusSerialPortConfiguration?  modbusSerialPortConfiguration = configurationHelper.Load<ModbusSerialPortConfiguration>("modbus-serialport");
            modbusSerialPortConfiguration ??= new ModbusSerialPortConfiguration();
            services.AddSingleton(modbusSerialPortConfiguration);

            ModbusRtuClient modbusRtuClient = new ModbusRtuClient()
            {
                BaudRate = modbusSerialPortConfiguration.BaudRate,
                Parity = modbusSerialPortConfiguration.Parity,
                StopBits = modbusSerialPortConfiguration.StopBits,
                ReadTimeout = modbusSerialPortConfiguration.ReadTimeout,
                WriteTimeout = modbusSerialPortConfiguration.WriteTimeout
            };
            if(!modbusRtuClient.IsConnected)
                modbusRtuClient.Connect(modbusSerialPortConfiguration.Port, modbusSerialPortConfiguration.ModbusEndianness);
            services.AddSingleton(modbusRtuClient);

            services.AddSingleton<ModbusFeatures>();

            return services;
        }
    }
}
