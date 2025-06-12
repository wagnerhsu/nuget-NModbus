using NModbus;
using System.Net.Sockets;

/// <summary>
/// 通用Modbus设备类，提供了常用的Modbus操作，并处理连接、重试等问题
/// </summary>
public class ModbusDevice : IDisposable
{
    private readonly string _ipAddress;
    private readonly int _port;
    private readonly byte _slaveId;
    private readonly int _timeout;
    private readonly int _maxRetries;

    private TcpClient _client;
    private IModbusMaster _master;
    private readonly object _lock = new object();
    private bool _isDisposed;

    public ModbusDevice(string ipAddress, int port = 502, byte slaveId = 1, int timeout = 3000, int maxRetries = 3)
    {
        _ipAddress = ipAddress;
        _port = port;
        _slaveId = slaveId;
        _timeout = timeout;
        _maxRetries = maxRetries;
    }

    public async Task ConnectAsync()
    {
        if (_client != null && _client.Connected)
            return;

        lock (_lock)
        {
            if (_client != null && _client.Connected)
                return;

            _client?.Dispose();
            _client = new TcpClient();
            _client.ReceiveTimeout = _timeout;
            _client.SendTimeout = _timeout;
        }

        await _client.ConnectAsync(_ipAddress, _port);

        var factory = new ModbusFactory();
        _master = factory.CreateMaster(_client);
    }

    public async Task<bool> IsConnectedAsync()
    {
        if (_client == null || !_client.Connected)
            return false;

        try
        {
            // 尝试读取一个寄存器，验证连接是否有效
            await _master.ReadHoldingRegistersAsync(_slaveId, 0, 1);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #region 基本读写方法

    public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort numberOfPoints)
    {
        await EnsureConnectedAsync();
        return await ExecuteWithRetryAsync(async () =>
            await _master.ReadHoldingRegistersAsync(_slaveId, startAddress, numberOfPoints));
    }

    public async Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort numberOfPoints)
    {
        await EnsureConnectedAsync();
        return await ExecuteWithRetryAsync(async () =>
            await _master.ReadInputRegistersAsync(_slaveId, startAddress, numberOfPoints));
    }

    public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort numberOfPoints)
    {
        await EnsureConnectedAsync();
        return await ExecuteWithRetryAsync(async () =>
            await _master.ReadCoilsAsync(_slaveId, startAddress, numberOfPoints));
    }

    public async Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort numberOfPoints)
    {
        await EnsureConnectedAsync();
        return await ExecuteWithRetryAsync(async () =>
            await Task.Run(() => _master.ReadInputs(_slaveId, startAddress, numberOfPoints)));
    }

    public async Task WriteSingleCoilAsync(ushort coilAddress, bool value)
    {
        await EnsureConnectedAsync();
        await ExecuteWithRetryAsync(async () =>
            await _master.WriteSingleCoilAsync(_slaveId, coilAddress, value));
    }

    public async Task WriteSingleRegisterAsync(ushort registerAddress, ushort value)
    {
        await EnsureConnectedAsync();
        await ExecuteWithRetryAsync(async () =>
            await _master.WriteSingleRegisterAsync(_slaveId, registerAddress, value));
    }

    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values)
    {
        await EnsureConnectedAsync();
        await ExecuteWithRetryAsync(async () =>
            await _master.WriteMultipleRegistersAsync(_slaveId, startAddress, values));
    }

    public async Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values)
    {
        await EnsureConnectedAsync();
        await ExecuteWithRetryAsync(async () =>
            await _master.WriteMultipleCoilsAsync(_slaveId, startAddress, values));
    }

    #endregion 基本读写方法

    #region 复杂数据类型读写

    // 读取32位整数 (大端序)
    public async Task<int> ReadInt32Async(ushort startAddress)
    {
        ushort[] registers = await ReadHoldingRegistersAsync(startAddress, 2);
        return (registers[0] << 16) | registers[1];
    }

    // 读取32位整数 (小端序)
    public async Task<int> ReadInt32LittleEndianAsync(ushort startAddress)
    {
        ushort[] registers = await ReadHoldingRegistersAsync(startAddress, 2);
        return (registers[1] << 16) | registers[0];
    }

    // 读取浮点数 (ABCD顺序)
    public async Task<float> ReadFloat32Async(ushort startAddress)
    {
        ushort[] registers = await ReadHoldingRegistersAsync(startAddress, 2);

        byte[] bytes = new byte[4];
        bytes[0] = (byte)((registers[0] >> 8) & 0xFF); // A
        bytes[1] = (byte)(registers[0] & 0xFF);        // B
        bytes[2] = (byte)((registers[1] >> 8) & 0xFF); // C
        bytes[3] = (byte)(registers[1] & 0xFF);        // D

        return BitConverter.ToSingle(bytes, 0);
    }

    // 写入32位整数 (大端序)
    public async Task WriteInt32Async(ushort startAddress, int value)
    {
        ushort[] registers = new ushort[2];
        registers[0] = (ushort)((value >> 16) & 0xFFFF);
        registers[1] = (ushort)(value & 0xFFFF);

        await WriteMultipleRegistersAsync(startAddress, registers);
    }

    // 写入浮点数 (ABCD顺序)
    public async Task WriteFloat32Async(ushort startAddress, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);

        ushort[] registers = new ushort[2];
        registers[0] = (ushort)((bytes[0] << 8) | bytes[1]);
        registers[1] = (ushort)((bytes[2] << 8) | bytes[3]);

        await WriteMultipleRegistersAsync(startAddress, registers);
    }

    #endregion 复杂数据类型读写

    #region 内部辅助方法

    private async Task EnsureConnectedAsync()
    {
        if (_client == null || !_client.Connected)
        {
            await ConnectAsync();
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
    {
        int attempts = 0;
        Exception lastException = null;

        while (attempts < _maxRetries)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempts++;

                if (attempts < _maxRetries)
                {
                    // 延迟一段时间再重试
                    await Task.Delay(100 * attempts);

                    // 如果是连接问题，尝试重新连接
                    try
                    {
                        await ConnectAsync();
                    }
                    catch
                    {
                        // 忽略重连异常，下一次循环会再次尝试
                    }
                }
            }
        }

        throw new Exception($"操作失败，已重试 {_maxRetries} 次", lastException);
    }

    private async Task ExecuteWithRetryAsync(Func<Task> action)
    {
        int attempts = 0;
        Exception lastException = null;

        while (attempts < _maxRetries)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempts++;

                if (attempts < _maxRetries)
                {
                    await Task.Delay(100 * attempts);

                    try
                    {
                        await ConnectAsync();
                    }
                    catch
                    {
                        // 忽略重连异常
                    }
                }
            }
        }

        throw new Exception($"操作失败，已重试 {_maxRetries} 次", lastException);
    }

    #endregion 内部辅助方法

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _master = null;
        _client?.Dispose();
        _client = null;

        _isDisposed = true;
    }
}

// 示例使用
internal class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            using var device = new ModbusDevice(
                ipAddress: "192.168.0.100",
                port: 502,
                slaveId: 1);

            await device.ConnectAsync();

            // 读取保持寄存器
            ushort[] registers = await device.ReadHoldingRegistersAsync(0, 10);
            Console.WriteLine("保持寄存器值:");
            for (int i = 0; i < registers.Length; i++)
            {
                Console.WriteLine($"寄存器 {i}: {registers[i]}");
            }

            // 读取32位整数
            int int32Value = await device.ReadInt32Async(100);
            Console.WriteLine($"32位整数值: {int32Value}");

            // 读取浮点数
            float floatValue = await device.ReadFloat32Async(102);
            Console.WriteLine($"浮点数值: {floatValue}");

            // 写入值
            await device.WriteSingleRegisterAsync(0, 1234);
            await device.WriteInt32Async(100, 87654321);
            await device.WriteFloat32Async(102, 987.654f);

            Console.WriteLine("写入完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
    }
}