using Microsoft.Extensions.Logging;
using NModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpSlaveDemo;

public class ModbusLoggerAdapter : IModbusLogger
{
    private readonly ILogger _logger;

    public ModbusLoggerAdapter(ILogger logger)
    {
        _logger = logger;
    }

    public void Debug(string message) => _logger.LogDebug(message);
    public void Information(string message) => _logger.LogInformation(message);
    public void Warning(string message) => _logger.LogWarning(message);
    public void Error(string message) => _logger.LogError(message);
    public void Verbose(string message) => _logger.LogTrace(message);

    public void Log(LoggingLevel level, string message)
    {
        // Check if the logging level is enabled before logging the message
        if (ShouldLog(level))
        {
            // Log the message using the appropriate logging method
            switch (level)
            {
                case LoggingLevel.Trace:
                    _logger.LogTrace(message);
                    break;
                case LoggingLevel.Debug:
                    _logger.LogDebug(message);
                    break;
                case LoggingLevel.Information:
                    _logger.LogInformation(message);
                    break;
                case LoggingLevel.Warning:
                    _logger.LogWarning(message);
                    break;
                case LoggingLevel.Error:
                    _logger.LogError(message);
                    break;
                case LoggingLevel.Critical:
                    _logger.LogCritical(message);
                    break;
                default:
                    // Handle unknown logging levels if necessary
                    break;
            }
        }
    }

    public bool ShouldLog(LoggingLevel level)
    {
        // Check if the logger is enabled for the specified logging level
        return _logger.IsEnabled((LogLevel)level);
    }
}
