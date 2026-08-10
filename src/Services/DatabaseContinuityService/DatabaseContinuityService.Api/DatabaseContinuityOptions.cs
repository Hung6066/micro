namespace His.Hope.DatabaseContinuityService;

public sealed class DatabaseContinuityOptions
{
    public const string SectionName = "DatabaseContinuity";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "postgresql";
    public string StorageUri { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = "auto";
    public bool StorageFallbackEnabled { get; set; } = true;
    public string LocalStoragePath { get; set; } = "/var/lib/his-hope/backups";
    public string EncryptionProvider { get; set; } = "vault-kms";
    public int RetentionDays { get; set; } = 30;
    public int KeepLastBackupsPerDatabase { get; set; } = 1;
    public bool PitrEnabled { get; set; }
    public int TargetRpoMinutes { get; set; } = 5;
    public int TargetRtoMinutes { get; set; } = 30;
    public string ExecutorPath { get; set; } = string.Empty;
    public string ExecutorWorkingDirectory { get; set; } = string.Empty;
    public bool SchedulerEnabled { get; set; }
    public int BackupIntervalHours { get; set; } = 24;
    public int RestoreDrillIntervalHours { get; set; } = 168;
    public int MaxAttempts { get; set; } = 3;
    public string RestoreDrillTargetEnvironment { get; set; } = "isolated";
    public string VaultAddress { get; set; } = string.Empty;
    public string VaultToken { get; set; } = string.Empty;
    public string VaultTransitKeyName { get; set; } = "his-hope-backup-encryption";
    public string AuditConnectionString { get; set; } = "";
}
