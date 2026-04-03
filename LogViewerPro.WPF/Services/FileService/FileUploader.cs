using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LogViewerPro.WPF.Services.FileService
{
    /// <summary>
    /// 文件上传服务 - 包含完整的安全验证和进度报告
    /// </summary>
    public class FileUploader
    {
        private readonly HashSet<string> _allowedLogExtensions = new()
        {
            ".log", ".txt", ".json", ".xml", ".csv", ".yaml", ".yml"
        };

        private readonly HashSet<string> _allowedArchiveExtensions = new()
        {
            ".zip", ".rar", ".7z", ".tar", ".gz"
        };

        private readonly HashSet<string> _dangerousExtensions = new()
        {
            ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jar", ".dll", ".scr", ".pif"
        };

        // 文件大小限制
        private const long MaxLogFileSize = 500 * 1024 * 1024; // 500MB
        private const long MaxArchiveSize = 2L * 1024 * 1024 * 1024; // 2GB

        /// <summary>
        /// 上传并验证文件
        /// </summary>
        public async Task<FileUploadResult> UploadFileAsync(
            string filePath,
            FileType expectedType,
            IProgress<FileUploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new FileUploadResult();

            try
            {
                // 1. 文件存在性检查
                if (!File.Exists(filePath))
                {
                    result.ErrorMessage = "文件不存在";
                    return result;
                }

                var fileInfo = new FileInfo(filePath);

                // 2. 文件大小检查
                var sizeValidation = ValidateFileSize(fileInfo, expectedType);
                if (!sizeValidation.IsValid)
                {
                    result.ErrorMessage = sizeValidation.ErrorMessage;
                    return result;
                }

                // 3. 文件类型验证
                var typeValidation = ValidateFileType(fileInfo, expectedType);
                if (!typeValidation.IsValid)
                {
                    result.ErrorMessage = typeValidation.ErrorMessage;
                    return result;
                }

                // 4. 安全检查
                var securityValidation = ValidateFileSecurity(fileInfo);
                if (!securityValidation.IsValid)
                {
                    result.ErrorMessage = securityValidation.ErrorMessage;
                    return result;
                }

                // 5. 文件内容验证
                var contentValidation = await ValidateFileContentAsync(fileInfo, expectedType, progress, cancellationToken);
                if (!contentValidation.IsValid)
                {
                    result.ErrorMessage = contentValidation.ErrorMessage;
                    return result;
                }

                // 6. 计算文件哈希
                var fileHash = await CalculateFileHashAsync(filePath, progress, cancellationToken);

                result.Success = true;
                result.FilePath = filePath;
                result.SafeFileName = GenerateSafeFileName(fileInfo.Name);
                result.FileSize = fileInfo.Length;
                result.FileHash = fileHash;
                result.FileType = expectedType;
                result.UploadTime = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "上传已取消";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"上传失败: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// 验证文件大小
        /// </summary>
        private ValidationResult ValidateFileSize(FileInfo fileInfo, FileType expectedType)
        {
            var maxSize = expectedType switch
            {
                FileType.Log => MaxLogFileSize,
                FileType.Archive => MaxArchiveSize,
                _ => MaxLogFileSize
            };

            if (fileInfo.Length > maxSize)
            {
                var maxMB = maxSize / (1024 * 1024);
                var actualMB = fileInfo.Length / (1024 * 1024);
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"文件大小超出限制。最大允许: {maxMB}MB, 实际: {actualMB}MB"
                };
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// 验证文件类型
        /// </summary>
        private ValidationResult ValidateFileType(FileInfo fileInfo, FileType expectedType)
        {
            var extension = fileInfo.Extension.ToLowerInvariant();

            // 检查危险文件类型
            if (_dangerousExtensions.Contains(extension))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"禁止上传可执行文件: {extension}"
                };
            }

            // 检查允许的文件类型
            var allowedExtensions = expectedType switch
            {
                FileType.Log => _allowedLogExtensions,
                FileType.Archive => _allowedArchiveExtensions,
                _ => _allowedLogExtensions
            };

            if (!allowedExtensions.Contains(extension))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"不支持的文件类型: {extension}。允许的类型: {string.Join(", ", allowedExtensions)}"
                };
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// 验证文件安全性
        /// </summary>
        private ValidationResult ValidateFileSecurity(FileInfo fileInfo)
        {
            // 1. 文件名安全检查
            var fileName = fileInfo.Name;

            // 检查路径遍历攻击
            if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "文件名包含非法字符"
                };
            }

            // 检查特殊字符
            var invalidChars = Path.GetInvalidFileNameChars();
            if (fileName.IndexOfAny(invalidChars) >= 0)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "文件名包含非法字符"
                };
            }

            // 2. 检查文件路径
            var fullPath = fileInfo.FullName;
            if (Path.IsPathRooted(fullPath) && fullPath.StartsWith("/etc/", StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "禁止访问系统目录"
                };
            }

            return new ValidationResult { IsValid = true };
        }

        /// <summary>
        /// 验证文件内容
        /// </summary>
        private async Task<ValidationResult> ValidateFileContentAsync(
            FileInfo fileInfo,
            FileType expectedType,
            IProgress<FileUploadProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                if (expectedType == FileType.Archive)
                {
                    // 验证压缩包完整性
                    return await ValidateArchiveAsync(fileInfo.FullName, progress, cancellationToken);
                }
                else if (expectedType == FileType.Log)
                {
                    // 验证日志文件编码
                    return await ValidateLogFileAsync(fileInfo.FullName, cancellationToken);
                }

                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"文件内容验证失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 验证压缩包完整性
        /// </summary>
        private async Task<ValidationResult> ValidateArchiveAsync(
            string archivePath,
            IProgress<FileUploadProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                using var fileStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
                using var archive = new System.IO.Compression.ZipArchive(fileStream, System.IO.Compression.ZipArchiveMode.Read);

                var totalEntries = archive.Entries.Count;
                var checkedEntries = 0;

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // 尝试读取每个条目以验证完整性
                    using var entryStream = entry.Open();
                    var buffer = new byte[1024];
                    await entryStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    checkedEntries++;
                    progress?.Report(new FileUploadProgress
                    {
                        Phase = "验证压缩包",
                        Percentage = (int)((checkedEntries / (double)totalEntries) * 30),
                        CurrentFile = entry.Name
                    });
                }

                return new ValidationResult { IsValid = true };
            }
            catch (InvalidDataException ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"压缩包已损坏: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 验证日志文件
        /// </summary>
        private async Task<ValidationResult> ValidateLogFileAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                // 读取前几行验证编码
                using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
                for (int i = 0; i < 10; i++)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    cancellationToken.ThrowIfCancellationRequested();

                    // 检查是否有不可读的字符
                    if (line.Any(c => char.IsControl(c) && c != '\t' && c != '\r' && c != '\n'))
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = "文件包含不可读的控制字符,可能不是有效的文本文件"
                        };
                    }
                }

                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"日志文件验证失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 计算文件哈希值
        /// </summary>
        private async Task<string> CalculateFileHashAsync(
            string filePath,
            IProgress<FileUploadProgress>? progress,
            CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            var totalBytes = fileStream.Length;
            var processedBytes = 0L;
            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                processedBytes += bytesRead;

                // 报告进度(30-100%)
                var percentage = 30 + (int)((processedBytes / (double)totalBytes) * 70);
                progress?.Report(new FileUploadProgress
                {
                    Phase = "计算文件哈希",
                    Percentage = percentage,
                    BytesProcessed = processedBytes,
                    TotalBytes = totalBytes
                });
            }

            sha256.TransformFinalBlock(buffer, 0, 0);
            var hash = sha256.Hash;

            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// 生成安全的文件名
        /// </summary>
        private string GenerateSafeFileName(string originalFileName)
        {
            // 移除路径信息
            var fileName = Path.GetFileName(originalFileName);

            // 替换非法字符
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            // 添加时间戳前缀避免冲突
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);

            return $"{timestamp}_{nameWithoutExt}{extension}";
        }
    }

    #region 辅助类型

    public enum FileType
    {
        Log,
        Archive,
        Other
    }

    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FilePath { get; set; }
        public string? SafeFileName { get; set; }
        public long FileSize { get; set; }
        public string? FileHash { get; set; }
        public FileType FileType { get; set; }
        public DateTime UploadTime { get; set; }
    }

    public class FileUploadProgress
    {
        public string Phase { get; set; } = "";
        public int Percentage { get; set; }
        public string? CurrentFile { get; set; }
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public double TransferSpeed { get; set; } // MB/s
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
