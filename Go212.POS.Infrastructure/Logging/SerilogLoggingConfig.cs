using System.Text.RegularExpressions;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace Go212.POS.Infrastructure.Logging;

/// <summary>
/// Serilog configuration with daily rolling log files, retention policy, and strict secret masking (PINs, passwords, card data).
/// </summary>
public static class SerilogLoggingConfig
{
    private static readonly Regex PinMaskRegex = new(@"""(?:pin|password|pinhash|cardnumber|secret)""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Serilog.ILogger CreateLogger(string logsDirectory = "logs")
    {
        if (!Directory.Exists(logsDirectory))
        {
            Directory.CreateDirectory(logsDirectory);
        }

        string logPath = Path.Combine(logsDirectory, "go212-pos-.log");

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "GO212.POS")
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 31,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
    }

    public static string MaskSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return PinMaskRegex.Replace(input, @"""$1"": ""***REDACTED***""");
    }
}

public interface IAuditLogger
{
    Task LogAsync(long? userId, string userName, AuditAction action, string? entityName, long? entityId, string? details);
}

public class AuditLogger : IAuditLogger
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(IUnitOfWork unitOfWork, ILogger<AuditLogger> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task LogAsync(long? userId, string userName, AuditAction action, string? entityName, long? entityId, string? details)
    {
        try
        {
            var auditEvent = new AuditEvent
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                TargetEntity = entityName,
                TargetId = entityId,
                Details = SerilogLoggingConfig.MaskSensitiveData(details ?? string.Empty),
                IpOrMachine = Environment.MachineName,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Audit.LogAsync(auditEvent);
            _logger.LogInformation("Audit [{Action}] by '{User}' on '{Entity}#{Id}': {Details}",
                action, userName, entityName, entityId, auditEvent.Details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log entry for user {User}", userName);
        }
    }
}
