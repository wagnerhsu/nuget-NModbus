using NModbus;
using NModbus.IO;
using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace ModbusRtuMasterExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("NModbus RTU Master示例开始...");
            
            string portName = "COM3";
            int baudRate = 9600;
            byte slaveId = 1;
            
            try
            {
                // 配置串口
                using var serialPort = new SerialPort(portName)
                {
                    BaudRate = baudRate,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                
                serialPort.Open();
                Console.WriteLine($"已连接到串口 {portName}");
                
                // 创建Modbus RTU主站
                var factory = new ModbusFactory();
                using var adapter = new SerialPortAdapter(serialPort);
                IModbusMaster master = factory.CreateRtuMaster(adapter);
                
                // 设置报文超时时间
                master.Transport.ReadTimeout = 1000;
                master.Transport.WriteTimeout = 1000;
                
                // 读取保持寄存器
                ushort startAddress = 0;
                ushort numberOfPoints = 10;
                
                Console.WriteLine($"从设备 {slaveId} 读取保持寄存器 {startAddress}-{startAddress + numberOfPoints - 1}");
                ushort[] registers = await master.ReadHoldingRegistersAsync(slaveId, startAddress, numberOfPoints);
                
                for (int i = 0; i < registers.Length; i++)
                {
                    Console.WriteLine($"寄存器 {startAddress + i}: {registers[i]} (十六进制: 0x{registers[i]:X4})");
                }
                
                // 写入单个寄存器
                ushort registerAddress = 0;
                ushort registerValue = 1234;
                
                Console.WriteLine($"向设备 {slaveId} 的寄存器 {registerAddress} 写入值 {registerValue}");
                await master.WriteSingleRegisterAsync(slaveId, registerAddress, registerValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }
    }
}