using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;

namespace LogViewerPro.WPF.Services.FileService
{
    /// <summary>
    /// 流式压缩包解压器 - 支持大文件、进度报告和取消操作
    /// </summary>
    public class StreamZipExtractor
    {
        /// <summary>
        /// 流式解压压缩包
        /// </summary>
        public async Task<ExtractionResult> ExtractAsync(
            string zipPath,
            string destinationPath,
            IProgress<ExtractionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ExtractionResult();
            var startTime = DateTime.Now;

            try
            {
                // 创建目标目录
                Directory.CreateDirectory(destinationPath);

                // 使用SharpZipLib进行流式解压
                using var fileStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
                using var zipFile = new ZipFile(fileStream);

                var totalEntries = zipFile.Count;
                var processedEntries = 0;
                var totalBytes = fileStream.Length;
                var processedBytes = 0L;

                result.TotalFiles = totalEntries;

                foreach (ZipEntry entry in zipFile)
                {
                    // 检查取消请求
                    if (cancellationToken.IsCancellationRequested)
                    {
                        result.Cancelled = true;
                        result.ErrorMessage = "解压操作已取消";
                        break;
                    }

                    // 跳过目录
                    if (entry.IsDirectory)
                    {
                        continue;
                    }

                    // 安全检查: 防止路径遍历攻击
                    var entryName = GetSafeEntryName(entry.Name);
                    var destinationFilePath = Path.Combine(destinationPath, entryName);

                    // 确保目标目录存在
                    var destinationDir = Path.GetDirectoryName(destinationFilePath);
                    if (!string.IsNullOrEmpty(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    // 流式解压单个文件
                    await ExtractEntryAsync(zipFile, entry, destinationFilePath, cancellationToken);

                    // 更新进度
                    processedEntries++;
                    processedBytes += entry.Size;

                    result.ExtractedFiles++;

                    var percentage = (int)((processedEntries / (double)totalEntries) * 100);
                    var elapsed = DateTime.Now - startTime;
                    var speed = processedBytes / (1024.0 * 1024.0) / elapsed.TotalSeconds; // MB/s
                    var remainingBytes = totalBytes - processedBytes;
                    var estimatedTimeRemaining = TimeSpan.FromSeconds(remainingBytes / (1024.0 * 1024.0) / speed);

                    progress?.Report(new ExtractionProgress
                    {
                        Phase = "正在解压",
                        Percentage = percentage,
                        CurrentFile = entryName,
                        ProcessedFiles = processedEntries,
                        TotalFiles = totalEntries,
                        ProcessedBytes = processedBytes,
                        TotalBytes = totalBytes,
                        SpeedMBPerSecond = speed,
                        EstimatedTimeRemaining = estimatedTimeRemaining
                    });
                }

                result.Success = !result.Cancelled;
                result.ElapsedTime = DateTime.Now - startTime;
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
                result.ErrorMessage = "解压操作已取消";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"解压失败: {ex.Message}";
            }
            finally
            {
                // 如果失败或取消,清理部分解压的文件
                if (!result.Success && Directory.Exists(destinationPath))
                {
                    try
                    {
                        Directory.Delete(destinationPath, true);
                    }
                    catch
                    {
                        // 忽略清理错误
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 流式解压单个文件条目
        /// </summary>
        private async Task ExtractEntryAsync(
            ZipFile zipFile,
            ZipEntry entry,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            using var entryStream = zipFile.GetInputStream(entry);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);

            var buffer = new byte[8192]; // 8KB缓冲区
            int bytesRead;

            while ((bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            }

            // 恢复文件时间戳
            File.SetLastWriteTime(destinationPath, entry.DateTime);
        }

        /// <summary>
        /// 获取安全的条目名称(防止路径遍历攻击)
        /// </summary>
        private string GetSafeEntryName(string entryName)
        {
            // 移除路径遍历字符
            var safeName = entryName.Replace("..", "");

            // 规范化路径分隔符
            safeName = safeName.Replace('/', Path.DirectorySeparatorChar);
            safeName = safeName.Replace('\\', Path.DirectorySeparatorChar);

            // 移除开头的路径分隔符
            safeName = safeName.TrimStart(Path.DirectorySeparatorChar);

            return safeName;
        }

        /// <summary>
        /// 获取压缩包信息(不解压)
        /// </summary>
        public async Task<ArchiveInfo> GetArchiveInfoAsync(string zipPath)
        {
            var info = new ArchiveInfo();

            try
            {
                using var fileStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read);
                using var zipFile = new ZipFile(fileStream);

                info.TotalFiles = zipFile.Count;
                info.TotalSize = fileStream.Length;

                long uncompressedSize = 0;
                var fileTypes = new HashSet<string>();

                foreach (ZipEntry entry in zipFile)
                {
                    if (!entry.IsDirectory)
                    {
                        uncompressedSize += entry.Size;

                        var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
                        if (!string.IsNullOrEmpty(extension))
                        {
                            fileTypes.Add(extension);
                        }
                    }
                }

                info.UncompressedSize = uncompressedSize;
                info.FileTypes = fileTypes;
                info.CompressionRatio = (double)info.TotalSize / uncompressedSize;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
            }

            return info;
        }
    }

    #region 辅助类型

    public class ExtractionResult
    {
        public bool Success { get; set; }
        public bool Cancelled { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalFiles { get; set; }
        public int ExtractedFiles { get; set; }
        public TimeSpan ElapsedTime { get; set; }
    }

    public class ExtractionProgress
    {
        public string Phase { get; set; } = "";
        public int Percentage { get; set; }
        public string? CurrentFile { get; set; }
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public long ProcessedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedMBPerSecond { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    public class ArchiveInfo
    {
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public long UncompressedSize { get; set; }
        public double CompressionRatio { get; set; }
        public HashSet<string> FileTypes { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    #endregion
}
