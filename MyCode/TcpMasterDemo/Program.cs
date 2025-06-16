using Microsoft.Extensions.Configuration;
using NModbus;
using System.Net.Sockets;

Console.WriteLine("NModbus TCP Master示例开始...");

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

string ipAddress = config["Modbus:IpAddress"];
int port = int.Parse(config["Modbus:Port"]);
byte slaveId = 1;

try
{
    // 创建TCP连接
    using var client = new TcpClient(ipAddress, port);
    // 设置连接超时
    client.ReceiveTimeout = 1000;
    client.SendTimeout = 1000;

    // 创建Modbus工厂和Master
    var factory = new ModbusFactory();
    IModbusMaster master = factory.CreateMaster(client);

    // 1. 读取线圈状态 (功能码: 01)
    Console.WriteLine("\n读取线圈:");
    bool[] coils = await master.ReadCoilsAsync(slaveId, startAddress: 0, numberOfPoints: 10);
    for (int i = 0; i < coils.Length; i++)
    {
        Console.WriteLine($"线圈 {i}: {coils[i]}");
    }

    // 2. 读取保持寄存器 (功能码: 03)
    Console.WriteLine("\n读取保持寄存器:");
    ushort[] holdingRegisters = await master.ReadHoldingRegistersAsync(slaveId, startAddress: 0, numberOfPoints: 10);
    for (int i = 0; i < holdingRegisters.Length; i++)
    {
        Console.WriteLine($"保持寄存器 {i}: {holdingRegisters[i]} (十六进制: 0x{holdingRegisters[i]:X4})");
    }

    // 3. 写单个线圈 (功能码: 05)
    Console.WriteLine("\n写单个线圈:");
    await master.WriteSingleCoilAsync(slaveId, coilAddress: 0, value: true);
    Console.WriteLine("已写入线圈 0: true");

    // 4. 写单个寄存器 (功能码: 06)
    Console.WriteLine("\n写单个寄存器:");
    await master.WriteSingleRegisterAsync(slaveId, registerAddress: 0, value: 12345);
    Console.WriteLine("已写入寄存器 0: 12345");

    // 5. 写多个寄存器 (功能码: 16)
    Console.WriteLine("\n写多个寄存器:");
    await master.WriteMultipleRegistersAsync(slaveId, startAddress: 0,
        data: new ushort[] { 1000, 2000, 3000, 4000, 5000 });
    Console.WriteLine("已写入5个寄存器值");
}
catch (Exception ex)
{
    Console.WriteLine($"发生错误: {ex.Message}");
    Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
}

Console.WriteLine("按任意键退出...");
Console.ReadKey();