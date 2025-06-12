using NModbus.IO;
using System.IO.Ports;

namespace ModbusRtuMasterExample
{
    public class SerialPortAdapter : IStreamResource, IDisposable
    {
        private readonly SerialPort _serialPort;

        public SerialPortAdapter(SerialPort serialPort)
        {
            _serialPort = serialPort;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _serialPort.Read(buffer, offset, count);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _serialPort.Write(buffer, offset, count);
        }

        public void DiscardInBuffer()
        {
            _serialPort.DiscardInBuffer();
        }

        public void Dispose()
        {
            // Do not dispose the serial port here, as it's disposed elsewhere
        }

        public int InfiniteTimeout => SerialPort.InfiniteTimeout;

        public int ReadTimeout
        {
            get => _serialPort.ReadTimeout;
            set => _serialPort.ReadTimeout = value;
        }

        public int WriteTimeout
        {
            get => _serialPort.WriteTimeout;
            set => _serialPort.WriteTimeout = value;
        }
    }
}