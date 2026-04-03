using System;
using System.Collections.Generic;
using FluentAssertions;
using LogViewerPro.WPF.Services.LogService;
using Xunit;

namespace LogViewerPro.Tests.LogService
{
    /// <summary>
    /// 日志高级筛选功能测试
    /// </summary>
    public class LogFilterTests
    {
        private readonly LogFilter _filter;
        private readonly List<LogEntry> _testLogs;

        public LogFilterTests()
        {
            _filter = new LogFilter();
            _testLogs = CreateTestLogs();
        }

        #region 日志级别筛选测试

        [Fact]
        public void FilterLogs_BySingleLevel_ShouldReturnMatchingLogs()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Levels = new List<string> { "ERROR" }
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().HaveCount(3);
            result.Should().OnlyContain(l => l.Level == "ERROR");
        }

        [Fact]
        public void FilterLogs_ByMultipleLevels_ShouldReturnMatchingLogs()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Levels = new List<string> { "ERROR", "FATAL" }
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().HaveCount(5);
            result.Should().OnlyContain(l => l.Level == "ERROR" || l.Level == "FATAL");
        }

        [Fact]
        public void FilterLogs_ByNonExistentLevel_ShouldReturnEmpty()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Levels = new List<string> { "TRACE" }
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region 时间范围筛选测试

        [Fact]
        public void FilterLogs_ByStartTime_ShouldReturnLogsAfterTime()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                StartTime = new DateTime(2024, 1, 1, 12, 0, 0)
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l => l.Timestamp >= criteria.StartTime);
        }

        [Fact]
        public void FilterLogs_ByEndTime_ShouldReturnLogsBeforeTime()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                EndTime = new DateTime(2024, 1, 1, 12, 0, 0)
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l => l.Timestamp <= criteria.EndTime);
        }

        [Fact]
        public void FilterLogs_ByTimeRange_ShouldReturnLogsInRange()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                StartTime = new DateTime(2024, 1, 1, 10, 0, 0),
                EndTime = new DateTime(2024, 1, 1, 14, 0, 0)
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l =>
                l.Timestamp >= criteria.StartTime &&
                l.Timestamp <= criteria.EndTime);
        }

        #endregion

        #region 关键字搜索测试

        [Fact]
        public void FilterLogs_ByKeyword_ShouldReturnMatchingLogs()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Keyword = "timeout"
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l => l.Message!.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void FilterLogs_ByKeywordCaseInsensitive_ShouldMatchRegardless()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Keyword = "TIMEOUT",
                CaseSensitive = false
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().NotBeEmpty();
        }

        [Fact]
        public void FilterLogs_ByKeywordCaseSensitive_ShouldMatchExactCase()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Keyword = "Timeout",
                CaseSensitive = true
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l => l.Message!.Contains("Timeout"));
        }

        #endregion

        #region 正则表达式搜索测试

        [Fact]
        public void FilterLogs_ByRegex_ShouldMatchPattern()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Keyword = @"\b\d{3}-\d{3}-\d{4}\b", // 电话号码模式
                UseRegex = true
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l => System.Text.RegularExpressions.Regex.IsMatch(l.Message!, @"\b\d{3}-\d{3}-\d{4}\b"));
        }

        [Fact]
        public void FilterLogs_ByInvalidRegex_ShouldFallbackToNormalSearch()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Keyword = "[invalid(regex", // 无效的正则表达式
                UseRegex = true
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            // 应该降级为普通搜索,而不是抛出异常
            result.Should().NotBeNull();
        }

        #endregion

        #region 模糊匹配测试

        [Fact]
        public void FilterLogs_ByFuzzyMatch_ShouldMatchSimilar()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Keyword = "timout", // 拼写错误
                FuzzyMatch = true,
                FuzzyThreshold = 0.7
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            // 应该匹配到 "timeout"
            result.Should().NotBeEmpty();
        }

        #endregion

        #region 多条件组合测试

        [Fact]
        public void FilterLogs_ByMultipleConditions_ShouldMatchAll()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Levels = new List<string> { "ERROR", "FATAL" },
                StartTime = new DateTime(2024, 1, 1, 10, 0, 0),
                EndTime = new DateTime(2024, 1, 1, 14, 0, 0),
                Keyword = "timeout"
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l =>
                (l.Level == "ERROR" || l.Level == "FATAL") &&
                l.Timestamp >= criteria.StartTime &&
                l.Timestamp <= criteria.EndTime &&
                l.Message!.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region 行号范围筛选测试

        [Fact]
        public void FilterLogs_ByLineRange_ShouldReturnLogsInRange()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                StartLine = 10,
                EndLine = 20
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l => l.LineNumber >= 10 && l.LineNumber <= 20);
        }

        #endregion

        #region 排除模式测试

        [Fact]
        public void FilterLogs_WithExcludePatterns_ShouldExcludeMatches()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                Keyword = "error",
                ExcludePatterns = new List<string> { "timeout" }
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l =>
                l.Message!.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                !l.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region 自定义表达式测试

        [Fact]
        public void FilterLogs_ByCustomExpression_ShouldEvaluateCorrectly()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                CustomExpression = "Level = 'ERROR' AND Message CONTAINS 'timeout'"
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l =>
                l.Level == "ERROR" &&
                l.Message!.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void FilterLogs_ByComplexExpression_ShouldEvaluateCorrectly()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                CustomExpression = "(Level = 'ERROR' OR Level = 'FATAL') AND Source CONTAINS 'Service'"
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().OnlyContain(l =>
                (l.Level == "ERROR" || l.Level == "FATAL") &&
                l.Source!.Contains("Service", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region 排序测试

        [Fact]
        public void FilterLogs_SortByTimestampDescending_ShouldSortCorrectly()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                SortBy = "TIMESTAMP",
                SortDescending = true
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            result.Should().BeInDescendingOrder(l => l.Timestamp);
        }

        [Fact]
        public void FilterLogs_SortByLevel_ShouldSortByPriority()
        {
            // Arrange
            var criteria = new LogFilterCriteria
            {
                SortBy = "LEVEL",
                SortDescending = true
            };

            // Act
            var result = _filter.FilterLogs(_testLogs, criteria);

            // Assert
            // FATAL(4) > ERROR(3) > WARN(2) > INFO(1) > DEBUG(0)
            result.Should().BeInDescendingOrder(l => GetLevelPriority(l.Level));
        }

        #endregion

        #region 辅助方法

        private List<LogEntry> CreateTestLogs()
        {
            return new List<LogEntry>
            {
                new LogEntry
                {
                    LineNumber = 1,
                    Timestamp = new DateTime(2024, 1, 1, 8, 0, 0),
                    Level = "INFO",
                    Source = "ServiceA",
                    Message = "Service started"
                },
                new LogEntry
                {
                    LineNumber = 5,
                    Timestamp = new DateTime(2024, 1, 1, 9, 0, 0),
                    Level = "DEBUG",
                    Source = "ServiceA",
                    Message = "Debug message"
                },
                new LogEntry
                {
                    LineNumber = 10,
                    Timestamp = new DateTime(2024, 1, 1, 10, 0, 0),
                    Level = "ERROR",
                    Source = "ServiceB",
                    Message = "Connection timeout error"
                },
                new LogEntry
                {
                    LineNumber = 15,
                    Timestamp = new DateTime(2024, 1, 1, 11, 0, 0),
                    Level = "WARN",
                    Source = "ServiceC",
                    Message = "Warning message"
                },
                new LogEntry
                {
                    LineNumber = 20,
                    Timestamp = new DateTime(2024, 1, 1, 12, 0, 0),
                    Level = "ERROR",
                    Source = "ServiceB",
                    Message = "Database error"
                },
                new LogEntry
                {
                    LineNumber = 25,
                    Timestamp = new DateTime(2024, 1, 1, 13, 0, 0),
                    Level = "INFO",
                    Source = "ServiceA",
                    Message = "Processing request"
                },
                new LogEntry
                {
                    LineNumber = 30,
                    Timestamp = new DateTime(2024, 1, 1, 14, 0, 0),
                    Level = "FATAL",
                    Source = "ServiceD",
                    Message = "System fatal error"
                },
                new LogEntry
                {
                    LineNumber = 35,
                    Timestamp = new DateTime(2024, 1, 1, 15, 0, 0),
                    Level = "ERROR",
                    Source = "ServiceB",
                    Message = "Another timeout"
                },
                new LogEntry
                {
                    LineNumber = 40,
                    Timestamp = new DateTime(2024, 1, 1, 16, 0, 0),
                    Level = "FATAL",
                    Source = "ServiceD",
                    Message = "Out of memory fatal"
                },
                new LogEntry
                {
                    LineNumber = 45,
                    Timestamp = new DateTime(2024, 1, 1, 17, 0, 0),
                    Level = "DEBUG",
                    Source = "ServiceA",
                    Message = "Phone: 123-456-7890"
                }
            };
        }

        private int GetLevelPriority(string? level)
        {
            return level?.ToUpperInvariant() switch
            {
                "DEBUG" => 0,
                "INFO" => 1,
                "WARN" or "WARNING" => 2,
                "ERROR" => 3,
                "FATAL" => 4,
                _ => -1
            };
        }

        #endregion
    }
}
