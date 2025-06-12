using NModbus;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusTcpSlaveExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Modbus TCP Slave示例开始...");
            
            // 数据存储
            var coils = new bool[100];
            var discreteInputs = new bool[100];
            var holdingRegisters = new ushort[100];
            var inputRegisters = new ushort[100];
            
            // 初始化一些数据以供测试
            for (int i = 0; i < holdingRegisters.Length; i++)
            {
                holdingRegisters[i] = (ushort)(i + 1000);
                inputRegisters[i] = (ushort)(i + 2000);
            }
            
            for (int i = 0; i < coils.Length; i++)
            {
                coils[i] = i % 2 == 0;
                discreteInputs[i] = i % 3 == 0;
            }
            
            try
            {
                // TCP监听
                var listener = new TcpListener(IPAddress.Any, 502);
                listener.Start();
                Console.WriteLine("Modbus TCP服务器启动，监听端口502...");
                
                // 创建数据存储
                var dataStore = new ModbusSlaveDataStore(
                    new ModbusDataCollection<bool>(coils),
                    new ModbusDataCollection<bool>(discreteInputs),
                    new ModbusDataCollection<ushort>(holdingRegisters),
                    new ModbusDataCollection<ushort>(inputRegisters)
                );
                
                // 创建Modbus从站
                var factory = new ModbusFactory();
                var slaveNetwork = factory.CreateSlaveNetwork(listener);
                
                // 添加从站，ID为1
                var slave = factory.CreateSlave(1);
                slave.DataStore = dataStore;
                slaveNetwork.AddSlave(slave);
                
                // 启动监听
                Console.WriteLine("从站已启动，等待连接...");
                await slaveNetwork.ListenAsync();
                
                // 注册事件以监听数据变化
                dataStore.HoldingRegisters.DataChanged += (sender, e) =>
                {
                    Console.WriteLine($"保持寄存器数据已更改 - 起始地址: {e.Index}, 数量: {e.Count}");
                    
                    var collection = (ModbusDataCollection<ushort>)sender;
                    for (int i = e.Index; i < e.Index + e.Count; i++)
                    {
                        if (i < collection.Count)
                        {
                            Console.WriteLine($"  地址 {i}: {collection[i]}");
                        }
                    }
                };
                
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
}