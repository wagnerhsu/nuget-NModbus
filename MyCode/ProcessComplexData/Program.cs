using NModbus;
using System.Net.Sockets;

string ipAddress = "192.168.0.100";
int port = 502;
byte slaveId = 1;

try
{
    using var client = new TcpClient(ipAddress, port);
    var factory = new ModbusFactory();
    IModbusMaster master = factory.CreateMaster(client);

    // ===== 读取32位整数 =====
    // 读取两个连续的寄存器
    ushort[] int32Registers = master.ReadHoldingRegisters(slaveId, 100, 2);

    // 方式1: 高位在前，低位在后 (Big Endian)
    int int32ValueBE = (int32Registers[0] << 16) | int32Registers[1];
    Console.WriteLine($"32位整数 (大端): {int32ValueBE}");

    // 方式2: 低位在前，高位在后 (Little Endian)
    int int32ValueLE = (int32Registers[1] << 16) | int32Registers[0];
    Console.WriteLine($"32位整数 (小端): {int32ValueLE}");

    // ===== 读取浮点数 =====
    // 读取两个连续的寄存器
    ushort[] floatRegisters = master.ReadHoldingRegisters(slaveId, 102, 2);

    // 方式1: 使用BitConverter (需要考虑字节顺序)
    byte[] bytes = new byte[4];

    // 假设设备使用大端序(ABCD)
    // A:高字节高字 B:高字节低字 C:低字节高字 D:低字节低字
    bytes[0] = (byte)((floatRegisters[0] >> 8) & 0xFF); // A
    bytes[1] = (byte)(floatRegisters[0] & 0xFF);        // B
    bytes[2] = (byte)((floatRegisters[1] >> 8) & 0xFF); // C
    bytes[3] = (byte)(floatRegisters[1] & 0xFF);        // D

    float floatValue = BitConverter.ToSingle(bytes, 0);
    Console.WriteLine($"浮点数 (ABCD): {floatValue}");

    // 其他常见的字节顺序
    // CDAB
    bytes[0] = (byte)((floatRegisters[1] >> 8) & 0xFF); // C
    bytes[1] = (byte)(floatRegisters[1] & 0xFF);        // D
    bytes[2] = (byte)((floatRegisters[0] >> 8) & 0xFF); // A
    bytes[3] = (byte)(floatRegisters[0] & 0xFF);        // B

    float floatValueCDAB = BitConverter.ToSingle(bytes, 0);
    Console.WriteLine($"浮点数 (CDAB): {floatValueCDAB}");

    // 写入32位整数
    int valueToWrite = 123456789;
    ushort[] registersToWrite = new ushort[2];

    // 高字节在前寄存器(大端)
    registersToWrite[0] = (ushort)((valueToWrite >> 16) & 0xFFFF);
    registersToWrite[1] = (ushort)(valueToWrite & 0xFFFF);

    master.WriteMultipleRegisters(slaveId, 100, registersToWrite);
    Console.WriteLine($"写入32位整数: {valueToWrite}");

    // 写入浮点数
    float floatToWrite = 123.456f;
    byte[] floatBytes = BitConverter.GetBytes(floatToWrite);

    // 转换为ABCD格式(假设设备要求)
    ushort register1 = (ushort)((floatBytes[0] << 8) | floatBytes[1]);
    ushort register2 = (ushort)((floatBytes[2] << 8) | floatBytes[3]);

    ushort[] floatRegistersToWrite = new ushort[] { register1, register2 };
    master.WriteMultipleRegisters(slaveId, 102, floatRegistersToWrite);
}
catch (Exception ex)
{
    Console.WriteLine($"错误: {ex.Message}");
}