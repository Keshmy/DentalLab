using System.IO.Compression;
using DentalLab.Models;
using DentalLab.Models.Entities;
using DentalLab.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DentalLab.Services
{
    public class BackupService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<BackupService> _logger;

        public BackupService(
            AppDbContext context,
            IConfiguration configuration,
            IWebHostEnvironment env,
            ILogger<BackupService> logger)
        {
            _context = context;
            _configuration = configuration;
            _env = env;
            _logger = logger;
        }

        public async Task<BackupSettings> GetOrCreateSettingsAsync()
        {
            var settings = await _context.BackupSettings.AsTracking().FirstOrDefaultAsync();
            if (settings != null)
                return settings;

            settings = new BackupSettings
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000001"),
                FolderPath = @"C:\DentalLabBackups",
                AutoEnabled = true,
                AutoHour = 2,
                AutoMinute = 0,
                Created = DateTime.UtcNow
            };
            _context.BackupSettings.Add(settings);
            await _context.SaveChangesAsync();
            EnsureFolder(settings.FolderPath);
            return settings;
        }

        public List<string> GetSuggestedFolders()
        {
            var list = new List<string> { @"C:\DentalLabBackups" };
            try
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    var path = Path.Combine(drive.RootDirectory.FullName, "DentalLabBackups");
                    if (!list.Contains(path, StringComparer.OrdinalIgnoreCase))
                        list.Add(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not enumerate drives for backup suggestions");
            }
            return list;
        }

        public (bool Ok, string? Error) EnsureFolder(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                    return (false, "مسار المجلد فارغ.");

                Directory.CreateDirectory(folderPath);
                var probe = Path.Combine(folderPath, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create/access backup folder {Folder}", folderPath);
                return (false, ex.Message);
            }
        }

        public List<BackupFileInfoVM> ListBackups(string folderPath)
        {
            var result = new List<BackupFileInfoVM>();
            if (!Directory.Exists(folderPath))
                return result;

            foreach (var path in Directory.EnumerateDirectories(folderPath, "DentalLab_*")
                         .OrderByDescending(p => p))
            {
                try
                {
                    var dir = new DirectoryInfo(path);
                    long size = dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                    result.Add(new BackupFileInfoVM
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        Created = dir.CreationTime,
                        SizeBytes = size
                    });
                }
                catch
                {
                    // skip inaccessible entries
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a dated backup folder that never overwrites previous ones:
        /// DentalLab_yyyy-MM-dd_HHmmss\database.bak + uploads.zip
        /// </summary>
        public async Task<(bool Ok, string Message, string? Path)> CreateBackupAsync(bool isAuto = false)
        {
            var settings = await GetOrCreateSettingsAsync();
            var ensure = EnsureFolder(settings.FolderPath);
            if (!ensure.Ok)
                return (false, $"تعذر الوصول لمجلد النسخ: {ensure.Error}", null);

            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var backupDir = Path.Combine(settings.FolderPath, $"DentalLab_{stamp}");
            Directory.CreateDirectory(backupDir);

            try
            {
                var bakPath = Path.Combine(backupDir, "database.bak");
                await BackupDatabaseAsync(bakPath);

                var uploadsSource = Path.Combine(_env.WebRootPath, "upload");
                var uploadsZip = Path.Combine(backupDir, "uploads.zip");
                if (Directory.Exists(uploadsSource) && Directory.EnumerateFileSystemEntries(uploadsSource).Any())
                {
                    if (File.Exists(uploadsZip))
                        File.Delete(uploadsZip);
                    ZipFile.CreateFromDirectory(uploadsSource, uploadsZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                }

                var msg = isAuto
                    ? $"نسخ تلقائي: {backupDir}"
                    : $"تم إنشاء النسخة الاحتياطية: {backupDir}";

                settings.LastBackupAt = DateTime.Now;
                settings.LastBackupMessage = msg;
                if (isAuto)
                    settings.LastAutoBackupDate = DateTime.Now.ToString("yyyy-MM-dd");
                settings.Modified = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Backup created at {Path}", backupDir);
                return (true, msg, backupDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed");
                try
                {
                    settings.LastBackupMessage = $"فشل النسخ: {ex.Message}";
                    settings.Modified = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch { /* ignore */ }

                try
                {
                    if (Directory.Exists(backupDir))
                        Directory.Delete(backupDir, recursive: true);
                }
                catch { /* ignore cleanup */ }

                return (false, $"فشل النسخ الاحتياطي: {ex.Message}", null);
            }
        }

        public async Task SaveSettingsAsync(string folderPath, bool autoEnabled, int autoHour, int autoMinute)
        {
            var settings = await GetOrCreateSettingsAsync();
            settings.FolderPath = folderPath.Trim();
            settings.AutoEnabled = autoEnabled;
            settings.AutoHour = Math.Clamp(autoHour, 0, 23);
            settings.AutoMinute = Math.Clamp(autoMinute, 0, 59);
            settings.Modified = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            EnsureFolder(settings.FolderPath);
        }

        /// <summary>
        /// Restores DB (+ uploads if present) from a dated backup folder on the server.
        /// Works from any browser — the restore runs on the machine hosting SQL Server / the app.
        /// </summary>
        public async Task<(bool Ok, string Message)> RestoreAsync(string backupFolderPath)
        {
            var settings = await GetOrCreateSettingsAsync();
            if (!IsUnderBackupRoot(backupFolderPath, settings.FolderPath))
                return (false, "مسار النسخة غير مسموح.");

            var bakPath = Path.Combine(backupFolderPath, "database.bak");
            if (!File.Exists(bakPath))
                return (false, "ملف database.bak غير موجود داخل مجلد النسخة.");

            try
            {
                await RestoreDatabaseAsync(bakPath);

                var uploadsZip = Path.Combine(backupFolderPath, "uploads.zip");
                if (File.Exists(uploadsZip))
                    RestoreUploads(uploadsZip);

                var msg = $"تمت الاستعادة من: {Path.GetFileName(backupFolderPath)}";
                try
                {
                    // Context may be reset after RESTORE — best-effort status update.
                    _context.ChangeTracker.Clear();
                    var settingsAfter = await _context.BackupSettings.FirstOrDefaultAsync();
                    if (settingsAfter != null)
                    {
                        settingsAfter.LastBackupMessage = msg;
                        settingsAfter.Modified = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not update backup settings after restore");
                }

                _logger.LogInformation("Database restored from {Path}", backupFolderPath);
                return (true, msg + " — أعد تحميل الصفحة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Restore failed from {Path}", backupFolderPath);
                return (false, $"فشلت الاستعادة: {ex.Message}");
            }
        }

        /// <summary>
        /// Accepts a backup uploaded from the user's PC (.zip of DentalLab_* folder, or a raw .bak).
        /// Saves it under the server backup folder then restores.
        /// </summary>
        public async Task<(bool Ok, string Message)> RestoreFromUploadAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return (false, "اختر ملف نسخة احتياطية.");

            var settings = await GetOrCreateSettingsAsync();
            var ensure = EnsureFolder(settings.FolderPath);
            if (!ensure.Ok)
                return (false, $"تعذر الوصول لمجلد النسخ: {ensure.Error}");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
            var targetDir = Path.Combine(settings.FolderPath, $"DentalLab_Upload_{stamp}");
            Directory.CreateDirectory(targetDir);

            try
            {
                if (ext == ".bak")
                {
                    var bakPath = Path.Combine(targetDir, "database.bak");
                    await using (var fs = File.Create(bakPath))
                        await file.CopyToAsync(fs);
                }
                else if (ext == ".zip")
                {
                    var zipPath = Path.Combine(targetDir, "upload.zip");
                    await using (var fs = File.Create(zipPath))
                        await file.CopyToAsync(fs);

                    var extractDir = Path.Combine(targetDir, "_extract");
                    ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

                    // Zip may contain database.bak at root, or a single DentalLab_* folder
                    var bak = Directory.EnumerateFiles(extractDir, "database.bak", SearchOption.AllDirectories).FirstOrDefault();
                    if (bak == null)
                        return (false, "الملف المضغوط لا يحتوي على database.bak");

                    var bakParent = Path.GetDirectoryName(bak)!;
                    File.Copy(bak, Path.Combine(targetDir, "database.bak"), overwrite: true);
                    var uploads = Path.Combine(bakParent, "uploads.zip");
                    if (File.Exists(uploads))
                        File.Copy(uploads, Path.Combine(targetDir, "uploads.zip"), overwrite: true);

                    try { Directory.Delete(extractDir, true); } catch { /* ignore */ }
                    try { File.Delete(zipPath); } catch { /* ignore */ }
                }
                else
                {
                    return (false, "الصيغة المدعومة: .bak أو .zip فقط.");
                }

                return await RestoreAsync(targetDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Restore from upload failed");
                return (false, $"فشلت الاستعادة من الملف المرفوع: {ex.Message}");
            }
        }

        private static bool IsUnderBackupRoot(string path, string root)
        {
            try
            {
                var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;
                var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                               + Path.DirectorySeparatorChar;
                return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void RestoreUploads(string uploadsZipPath)
        {
            var uploadDir = Path.Combine(_env.WebRootPath, "upload");
            Directory.CreateDirectory(uploadDir);

            var temp = Path.Combine(Path.GetTempPath(), "DentalLab_uploads_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try
            {
                ZipFile.ExtractToDirectory(uploadsZipPath, temp, overwriteFiles: true);
                foreach (var file in Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(temp, file);
                    var dest = Path.Combine(uploadDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, overwrite: true);
                }
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { /* ignore */ }
            }
        }

        private async Task RestoreDatabaseAsync(string bakPath)
        {
            var cs = _configuration.GetConnectionString("DbCon")
                     ?? throw new InvalidOperationException("Connection string DbCon is missing.");

            var appCs = new SqlConnectionStringBuilder(cs);
            var dbName = appCs.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName))
                throw new InvalidOperationException("Database name is missing from the connection string.");

            var masterCs = new SqlConnectionStringBuilder(cs)
            {
                InitialCatalog = "master"
            };

            var sqlPath = Path.GetFullPath(bakPath).Replace("'", "''");
            var sqlDbBracket = dbName.Replace("]", "]]");
            var sqlDbLiteral = dbName.Replace("'", "''");

            await using var conn = new SqlConnection(masterCs.ConnectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 600;
            cmd.CommandText = $@"
IF DB_ID(N'{sqlDbLiteral}') IS NOT NULL
BEGIN
    ALTER DATABASE [{sqlDbBracket}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END
RESTORE DATABASE [{sqlDbBracket}]
FROM DISK = N'{sqlPath}'
WITH REPLACE, RECOVERY;
ALTER DATABASE [{sqlDbBracket}] SET MULTI_USER;
";

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task BackupDatabaseAsync(string bakPath)
        {
            var cs = _configuration.GetConnectionString("DbCon")
                     ?? throw new InvalidOperationException("Connection string DbCon is missing.");

            var builder = new SqlConnectionStringBuilder(cs);
            var dbName = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName))
                throw new InvalidOperationException("Database name is missing from the connection string.");

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();

            var sqlPath = bakPath.Replace("'", "''");
            var sqlDb = dbName.Replace("]", "]]");

            await using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 600;
            cmd.CommandText = $@"
BACKUP DATABASE [{sqlDb}]
TO DISK = N'{sqlPath}'
WITH COPY_ONLY, INIT, NAME = N'DentalLab dated backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10;";

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
