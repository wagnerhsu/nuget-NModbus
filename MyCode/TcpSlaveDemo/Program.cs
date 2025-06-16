using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Data;
using Serilog;
using System.Net;
using System.Net.Sockets;
using TcpSlaveDemo;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Build Serilog configuration
        var serilogConfig = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("serilog.json", optional: false, reloadOnChange: true)
            .Build();

        // Set up Serilog as the static logger
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(serilogConfig)
            .CreateLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile("serilog.json", optional: true, reloadOnChange: true);
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                    logging.AddSerilog();
                })
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var config = host.Services.GetRequiredService<IConfiguration>();

            logger.LogInformation("Modbus TCP Slave示例开始...");

            try
            {
                var ipString = config["TcpListener:Ip"] ?? "0.0.0.0";
                var port = int.TryParse(config["TcpListener:Port"], out var p) ? p : 502;
                var ip = IPAddress.Parse(ipString);

                var listener = new TcpListener(ip, port);
                listener.Start();
                logger.LogInformation("Modbus TCP服务器启动，监听 {Ip}:{Port} ...", ip, port);

                var dataStore = new SlaveDataStore();
                dataStore.CoilDiscretes.BeforeWrite += (sender, e) =>
                {
                    logger.LogInformation("线圈数据将要更改 - 起始地址: {StartAddress}, 数量: {NumberOfPoints}", e.StartAddress, e.NumberOfPoints);
                };
                dataStore.CoilDiscretes.AfterWrite += (sender, e) =>
                {
                    logger.LogInformation("线圈数据已更改 - 起始地址: {StartAddress}, 数量: {NumberOfPoints}", e.StartAddress, e.NumberOfPoints);
                };
                dataStore.HoldingRegisters.BeforeWrite += (sender, e) =>
                {
                    logger.LogInformation("保持寄存器数据将要更改 - 起始地址: {StartAddress}, 数量: {NumberOfPoints}", e.StartAddress, e.NumberOfPoints);
                };
                dataStore.HoldingRegisters.AfterWrite += (sender, e) =>
                {
                    logger.LogInformation("保持寄存器数据已更改 - 起始地址: {StartAddress}, 数量: {NumberOfPoints}", e.StartAddress, e.NumberOfPoints);
                };

                for (ushort i = 0; i < 100; i++)
                {
                    dataStore.CoilDiscretes.WritePoints(i, new bool[] { i % 2 == 0 });
                    dataStore.CoilInputs.WritePoints(i, new bool[] { i % 3 == 0 });
                    dataStore.HoldingRegisters.WritePoints(i, new ushort[] { (ushort)(i + 1000) });
                    dataStore.InputRegisters.WritePoints(i, new ushort[] { (ushort)(i + 2000) });
                }

                var modbusLogger = new ModbusLoggerAdapter(logger);
                var factory = new ModbusFactory(logger: modbusLogger);
                var slaveNetwork = factory.CreateSlaveNetwork(listener);

                var slave = factory.CreateSlave(1, dataStore);
                slaveNetwork.AddSlave(slave);

                logger.LogInformation("从站已启动，等待连接...");
                await slaveNetwork.ListenAsync();

                logger.LogInformation("按任意键停止服务...");
                Console.ReadKey();

                slaveNetwork.Dispose();
                listener.Stop();
                logger.LogInformation("服务已停止");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "发生错误: {Message}", ex.Message);
            }
            finally
            {
                if (host is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else
                    host.Dispose();
            }
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}