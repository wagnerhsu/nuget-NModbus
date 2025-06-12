using NModbus;
using NModbus.Data;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {        Console.WriteLine("Modbus TCP Slave示例开始...");

        try
        {
            // TCP监听
            var listener = new TcpListener(IPAddress.Any, 502);
            listener.Start();
            Console.WriteLine("Modbus TCP服务器启动，监听端口502...");

            // 创建数据存储 - 使用正确的NModbus 3.x API
            var dataStore = new SlaveDataStore();            // 注册事件以监听数据变化
            dataStore.CoilDiscretes.BeforeWrite += (sender, e) =>
            {
                Console.WriteLine($"线圈数据将要更改 - 起始地址: {e.StartAddress}, 数量: {e.NumberOfPoints}");
            };
            
            dataStore.CoilDiscretes.AfterWrite += (sender, e) =>
            {
                Console.WriteLine($"线圈数据已更改 - 起始地址: {e.StartAddress}, 数量: {e.NumberOfPoints}");
            };
            
            dataStore.HoldingRegisters.BeforeWrite += (sender, e) =>
            {
                Console.WriteLine($"保持寄存器数据将要更改 - 起始地址: {e.StartAddress}, 数量: {e.NumberOfPoints}");
            };
            
            dataStore.HoldingRegisters.AfterWrite += (sender, e) =>
            {
                Console.WriteLine($"保持寄存器数据已更改 - 起始地址: {e.StartAddress}, 数量: {e.NumberOfPoints}");
            };

            // 写入初始数据到数据存储
            for (ushort i = 0; i < 100; i++)
            {
                dataStore.CoilDiscretes.WritePoints(i, new bool[] { i % 2 == 0 });
                dataStore.CoilInputs.WritePoints(i, new bool[] { i % 3 == 0 });
                dataStore.HoldingRegisters.WritePoints(i, new ushort[] { (ushort)(i + 1000) });
                dataStore.InputRegisters.WritePoints(i, new ushort[] { (ushort)(i + 2000) });
            }

            // 创建Modbus从站
            var factory = new ModbusFactory();
            var slaveNetwork = factory.CreateSlaveNetwork(listener);

            // 添加从站，ID为1
            var slave = factory.CreateSlave(1, dataStore);
            slaveNetwork.AddSlave(slave);

            // 启动监听
            Console.WriteLine("从站已启动，等待连接...");
            await slaveNetwork.ListenAsync();

            Console.WriteLine("按任意键停止服务...");
            Console.ReadKey();

            // 停止服务
            slaveNetwork.Dispose();
            listener.Stop();
            Console.WriteLine("服务已停止");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"发生错误: {ex.Message}");
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
    }
}