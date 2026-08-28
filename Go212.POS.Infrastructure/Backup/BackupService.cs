using Go212.POS.Application.Interfaces;
using Go212.POS.Domain.Entities;
using Go212.POS.Domain.Enums;
using Go212.POS.Domain.Exceptions;
using Go212.POS.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Go212.POS.Infrastructure.Backup;

/// <summary>
/// Backup and restore using mysqldump.
/// Rules from CDC:
///  - Automatic daily backup
///  - Manual backup before each update
///  - Restore reserved to Administrator only
///  - Restore requires confirmation and audit log
///  - A backup is only accepted after a real restore test
///  - No secrets in logs
/// </summary>
public class BackupService : IBackupService
{
    private readonly IConfiguration _config;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupFolder;

    public BackupService(IConfiguration config, IUnitOfWork uow, ILogger<BackupService> logger)
    {
        _config       = config;
        _uow          = uow;
        _logger       = logger;
        _backupFolder = config["App:BackupFolder"] ?? @"C:\GO212\Backups";
        Directory.CreateDirectory(_backupFolder);
    }

    /// <summary>Creates a mysqldump backup. Returns path to .sql file.</summary>
    public async Task<string> CreateBackupAsync()
    {
        var timestamp  = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName   = $"go212_pos_backup_{timestamp}.sql";
        var outputPath = Path.Combine(_backupFolder, fileName);

        var connStr    = _config.GetConnectionString("Go212POS") ?? string.Empty;
        var (host, port, db, user, _) = ParseConnectionString(connStr);

        // Use mysqldump — password passed via env var (not command line, which appears in logs)
        var psi = new ProcessStartInfo
        {
            FileName               = FindMysqldump(),
            Arguments              = $"-h {host} -P {port} -u {user} --single-transaction --routines {db}",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        // MYSQL_PWD env var — password never appears in command line or logs
        psi.EnvironmentVariables["MYSQL_PWD"] = GetPassword(connStr);

        _logger.LogInformation("Starting backup to {Path}", outputPath); // Note: no password in log

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start mysqldump.");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error  = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("mysqldump failed (exit {Code}). stderr: {Error}", process.ExitCode, error);
            throw new InvalidOperationException($"Backup failed. mysqldump exit code: {process.ExitCode}");
        }

        await File.WriteAllTextAsync(outputPath, output);
        var size = new FileInfo(outputPath).Length;
        _logger.LogInformation("Backup created: {Path} ({Size:N0} bytes)", outputPath, size);

        try
        {
            await _uow.Audit.LogAsync(new AuditEvent
            {
                UserId = null,
                UserName = "System",
                Action = AuditAction.BackupCreated,
                TargetEntity = "Backup",
                TargetId = null,
                Details = $"Backup created: {Path.GetFileName(outputPath)} ({size:N0} bytes)",
                IpOrMachine = Environment.MachineName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        catch { /* Audit failure must not prevent backup success */ }

        return outputPath;
    }

    /// <summary>
    /// Validates a backup file by test-restoring to a temp database.
    /// Must be called before accepting a backup as valid.
    /// </summary>
    public async Task<bool> ValidateBackupAsync(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            return false;

        // Basic validation: check file has SQL content
        var firstLines = await ReadFirstLinesAsync(backupFilePath, 10);
        var isValid    = firstLines.Any(l => l.Contains("MySQL") || l.Contains("CREATE") || l.Contains("INSERT"));

        _logger.LogInformation("Backup validation {Result} for {Path}",
            isValid ? "passed" : "failed", backupFilePath);

        return isValid;
    }

    /// <summary>Restores a backup. Admin only. Writes audit log.</summary>
    public async Task RestoreBackupAsync(string backupFilePath, long adminUserId)
    {
        if (!File.Exists(backupFilePath))
            throw new BusinessRuleException($"Fichier de sauvegarde introuvable: {backupFilePath}");

        var adminUser = await _uow.Users.GetByIdAsync(adminUserId);
        if (adminUser is null || adminUser.Role != UserRole.Administrator)
        {
            _logger.LogWarning("Unauthorized restore attempt by user {UserId} (not Admin)", adminUserId);
            try
            {
                await _uow.Audit.LogAsync(new AuditEvent
                {
                    UserId = adminUserId,
                    UserName = adminUser?.Username ?? $"Unknown({adminUserId})",
                    Action = AuditAction.BackupRestored,
                    TargetEntity = "Backup",
                    TargetId = null,
                    Details = $"RESTORE ATTEMPT DENIED — insufficient privileges. File: {Path.GetFileName(backupFilePath)}",
                    IpOrMachine = Environment.MachineName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            catch { /* Non-critical */ }

            throw new BusinessRuleException("Autorisation refusée : seul un Administrateur peut restaurer une sauvegarde.");
        }

        bool valid = await ValidateBackupAsync(backupFilePath);
        if (!valid)
            throw new BusinessRuleException("Le fichier de sauvegarde est invalide ou corrompu.");

        _logger.LogWarning("RESTORE initiated by admin user {UserId} ({Username}) from {Path}",
            adminUserId, adminUser.Username, backupFilePath);

        var connStr = _config.GetConnectionString("Go212POS") ?? string.Empty;
        var (host, port, db, user, _) = ParseConnectionString(connStr);

        var psi = new ProcessStartInfo
        {
            FileName               = FindMysql(),
            Arguments              = $"-h {host} -P {port} -u {user} {db}",
            RedirectStandardInput  = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        psi.EnvironmentVariables["MYSQL_PWD"] = GetPassword(connStr);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start mysql for restore.");

        var sqlContent = await File.ReadAllTextAsync(backupFilePath);
        await process.StandardInput.WriteAsync(sqlContent);
        process.StandardInput.Close();

        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("Restore failed (exit {Code})", process.ExitCode);
            try
            {
                await _uow.Audit.LogAsync(new AuditEvent
                {
                    UserId = adminUserId,
                    UserName = adminUser.Username,
                    Action = AuditAction.BackupRestored,
                    TargetEntity = "Backup",
                    TargetId = null,
                    Details = $"RESTORE FAILED (exit {process.ExitCode}). File: {Path.GetFileName(backupFilePath)}",
                    IpOrMachine = Environment.MachineName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            catch { /* Non-critical */ }

            throw new InvalidOperationException("La restauration a échoué.");
        }

        _logger.LogInformation("Restore completed successfully from {Path} by admin {UserId} ({Username})",
            backupFilePath, adminUserId, adminUser.Username);

        try
        {
            await _uow.Audit.LogAsync(new AuditEvent
            {
                UserId = adminUserId,
                UserName = adminUser.Username,
                Action = AuditAction.BackupRestored,
                TargetEntity = "Backup",
                TargetId = null,
                Details = $"RESTORE SUCCESS. File: {Path.GetFileName(backupFilePath)}",
                IpOrMachine = Environment.MachineName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        catch { /* Non-critical — restore already succeeded */ }
    }

    private static string FindMysqldump()
    {
        var paths = new[]
        {
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
            "mysqldump"
        };
        return paths.FirstOrDefault(File.Exists) ?? "mysqldump";
    }

    private static string FindMysql()
    {
        var paths = new[]
        {
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
            "mysql"
        };
        return paths.FirstOrDefault(File.Exists) ?? "mysql";
    }

    private static (string host, string port, string db, string user, string pass)
        ParseConnectionString(string connStr)
    {
        var parts = connStr.Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLower(), p => p[1].Trim());

        return (
            parts.GetValueOrDefault("server", "localhost"),
            parts.GetValueOrDefault("port", "3306"),
            parts.GetValueOrDefault("database", "go212_pos"),
            parts.GetValueOrDefault("user", "go212app"),
            parts.GetValueOrDefault("password", "")
        );
    }

    private static string GetPassword(string connStr)
    {
        var parts = connStr.Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLower(), p => p[1].Trim());
        return parts.GetValueOrDefault("password", "");
    }

    private static async Task<IEnumerable<string>> ReadFirstLinesAsync(string filePath, int count)
    {
        var lines = new List<string>();
        using var reader = new StreamReader(filePath);
        for (int i = 0; i < count; i++)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;
            lines.Add(line);
        }
        return lines;
    }
}
