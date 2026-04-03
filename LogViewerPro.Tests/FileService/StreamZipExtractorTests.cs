using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LogViewerPro.WPF.Services.FileService;
using Xunit;

namespace LogViewerPro.Tests.FileService
{
    /// <summary>
    /// 流式压缩包解压测试
    /// </summary>
    public class StreamZipExtractorTests : IDisposable
    {
        private readonly StreamZipExtractor _extractor;
        private readonly string _testDirectory;
        private readonly string _extractPath;

        public StreamZipExtractorTests()
        {
            _extractor = new StreamZipExtractor();
            _testDirectory = Path.Combine(Path.GetTempPath(), "LogViewerProZipTests", Guid.NewGuid().ToString());
            _extractPath = Path.Combine(_testDirectory, "extracted");
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        #region 基本解压测试

        [Fact]
        public async Task ExtractAsync_ValidZipFile_ShouldExtractAllFiles()
        {
            // Arrange
            var zipFile = CreateZipFile(3);

            // Act
            var result = await _extractor.ExtractAsync(zipFile, _extractPath);

            // Assert
            result.Success.Should().BeTrue();
            result.ExtractedFiles.Should().Be(3);
            Directory.GetFiles(_extractPath, "*", SearchOption.AllDirectories).Should().HaveCount(3);
        }

        [Fact]
        public async Task ExtractAsync_EmptyZip_ShouldSuccess()
        {
            // Arrange
            var zipFile = CreateEmptyZipFile();

            // Act
            var result = await _extractor.ExtractAsync(zipFile, _extractPath);

            // Assert
            result.Success.Should().BeTrue();
            result.ExtractedFiles.Should().Be(0);
        }

        [Fact]
        public async Task ExtractAsync_CorruptedZip_ShouldFail()
        {
            // Arrange
            var corruptZip = CreateCorruptedZipFile();

            // Act
            var result = await _extractor.ExtractAsync(corruptZip, _extractPath);

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region 大文件解压测试

        [Fact]
        public async Task ExtractAsync_LargeFiles_ShouldNotExceedMemoryLimit()
        {
            // Arrange
            var zipFile = CreateZipWithLargeFile(); // 100MB文件

            // Act
            var memoryBefore = GC.GetTotalMemory(false);
            var result = await _extractor.ExtractAsync(zipFile, _extractPath);
            var memoryAfter = GC.GetTotalMemory(false);

            // Assert
            result.Success.Should().BeTrue();
            // 内存增长不应超过50MB(证明是流式处理)
            (memoryAfter - memoryBefore).Should().BeLessThan(50 * 1024 * 1024);
        }

        #endregion

        #region 进度报告测试

        [Fact]
        public async Task ExtractAsync_WithProgressCallback_ShouldReportProgress()
        {
            // Arrange
            var zipFile = CreateZipFile(10);
            var progressReports = new List<ExtractionProgress>();
            var progress = new Progress<ExtractionProgress>(p => progressReports.Add(p));

            // Act
            var result = await _extractor.ExtractAsync(zipFile, _extractPath, progress);

            // Assert
            result.Success.Should().BeTrue();
            progressReports.Should().NotBeEmpty();
            progressReports[progressReports.Count - 1].Percentage.Should().Be(100);
        }

        [Fact]
        public async Task ExtractAsync_ProgressShouldIncludeSpeedEstimate()
        {
            // Arrange
            var zipFile = CreateZipFile(5);
            ExtractionProgress? lastProgress = null;
            var progress = new Progress<ExtractionProgress>(p => lastProgress = p);

            // Act
            var result = await _extractor.ExtractAsync(zipFile, _extractPath, progress);

            // Assert
            result.Success.Should().BeTrue();
            lastProgress.Should().NotBeNull();
            lastProgress!.SpeedMBPerSecond.Should().BeGreaterThan(0);
        }

        #endregion

        #region 取消操作测试

        [Fact]
        public async Task ExtractAsync_WithCancellation_ShouldCancelAndCleanup()
        {
            // Arrange
            var zipFile = CreateZipFile(100);
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(10));

            // Act
            var result = await _extractor.ExtractAsync(zipFile, _extractPath, null, cts.Token);

            // Assert
            result.Cancelled.Should().BeTrue();
            // 应该清理部分解压的文件
            Directory.Exists(_extractPath).Should().BeFalse();
        }

        #endregion

        #region 安全性测试

        [Fact]
        public async Task ExtractAsync_ZipWithPathTraversal_ShouldSanitize()
        {
            // Arrange
            var zipFile = CreateZipWithPathTraversal();

            // Act
            var result = await _extractor.ExtractAsync(zipFile, _extractPath);

            // Assert
            result.Success.Should().BeTrue();
            // 不应该在测试目录外创建文件
            File.Exists("/tmp/malicious.txt").Should().BeFalse();
        }

        [Fact]
        public async Task ExtractAsync_ZipWithAbsolutePaths_ShouldSanitize()
        {
            // Arrange
            var zipFile = CreateZipWithAbsolutePath();

            // Act
            var result = await _extractor.ExtractAsync(zipFile, _extractPath);

            // Assert
            result.Success.Should().BeTrue();
            // 不应该在绝对路径创建文件
            File.Exists("C:\\Windows\\System32\\malicious.txt").Should().BeFalse();
        }

        #endregion

        #region 获取压缩包信息测试

        [Fact]
        public async Task GetArchiveInfoAsync_ValidZip_ShouldReturnCorrectInfo()
        {
            // Arrange
            var zipFile = CreateZipFile(5);

            // Act
            var info = await _extractor.GetArchiveInfoAsync(zipFile);

            // Assert
            info.TotalFiles.Should().Be(5);
            info.FileTypes.Should().Contain(".txt");
            info.CompressionRatio.Should().BeGreaterThan(0);
        }

        #endregion

        #region 文件名编码测试

        [Fact]
        public async Task ExtractAsync_ZipWithChineseFilenames_ShouldExtractCorrectly()
        {
            // Arrange
            var zipFile = CreateZipWithChineseNames();

            // Act
            var result = await _extractor.ExtractAsync(zipFile, _extractPath);

            // Assert
            result.Success.Should().BeTrue();
            Directory.GetFiles(_extractPath, "*测试*").Should().NotBeEmpty();
        }

        #endregion

        #region 辅助方法

        private string CreateZipFile(int fileCount)
        {
            var zipPath = Path.Combine(_testDirectory, "test.zip");
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);

            for (int i = 0; i < fileCount; i++)
            {
                var entry = archive.CreateEntry($"file{i}.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write($"This is file {i} content");
            }

            return zipPath;
        }

        private string CreateEmptyZipFile()
        {
            var zipPath = Path.Combine(_testDirectory, "empty.zip");
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
            return zipPath;
        }

        private string CreateCorruptedZipFile()
        {
            var zipPath = Path.Combine(_testDirectory, "corrupt.zip");
            File.WriteAllText(zipPath, "This is not a valid zip file");
            return zipPath;
        }

        private string CreateZipWithLargeFile()
        {
            var zipPath = Path.Combine(_testDirectory, "large.zip");
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);

            var entry = archive.CreateEntry("largefile.txt");
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);

            // 写入100MB数据
            var line = new string('a', 1024); // 1KB
            for (int i = 0; i < 100 * 1024; i++)
            {
                writer.WriteLine(line);
            }

            return zipPath;
        }

        private string CreateZipWithPathTraversal()
        {
            var zipPath = Path.Combine(_testDirectory, "traversal.zip");
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);

            // 尝试路径遍历攻击
            var entry = archive.CreateEntry("../../../tmp/malicious.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("malicious content");

            return zipPath;
        }

        private string CreateZipWithAbsolutePath()
        {
            var zipPath = Path.Combine(_testDirectory, "absolute.zip");
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);

            // 尝试绝对路径
            var entry = archive.CreateEntry("/Windows/System32/malicious.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("malicious content");

            return zipPath;
        }

        private string CreateZipWithChineseNames()
        {
            var zipPath = Path.Combine(_testDirectory, "chinese.zip");
            using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);

            var entry = archive.CreateEntry("测试文件.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("中文内容");

            return zipPath;
        }

        #endregion
    }
}
