using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LogViewerPro.WPF.Services.LogService
{
    /// <summary>
    /// 日志高级筛选服务 - 支持多条件组合、正则表达式、模糊匹配
    /// </summary>
    public class LogFilter
    {
        /// <summary>
        /// 高级筛选日志
        /// </summary>
        public List<LogEntry> FilterLogs(List<LogEntry> logs, LogFilterCriteria criteria)
        {
            var query = logs.AsEnumerable();

            // 1. 日志级别筛选(多选)
            if (criteria.Levels != null && criteria.Levels.Any())
            {
                var levels = criteria.Levels.Select(l => l.ToUpperInvariant()).ToHashSet();
                query = query.Where(l => levels.Contains(l.Level?.ToUpperInvariant() ?? ""));
            }

            // 2. 时间范围筛选
            if (criteria.StartTime.HasValue)
            {
                query = query.Where(l => l.Timestamp >= criteria.StartTime.Value);
            }

            if (criteria.EndTime.HasValue)
            {
                query = query.Where(l => l.Timestamp <= criteria.EndTime.Value);
            }

            // 3. 关键字搜索
            if (!string.IsNullOrWhiteSpace(criteria.Keyword))
            {
                query = ApplyKeywordFilter(query, criteria);
            }

            // 4. 来源筛选(多选)
            if (criteria.Sources != null && criteria.Sources.Any())
            {
                query = query.Where(l => criteria.Sources.Contains(l.Source ?? ""));
            }

            // 5. 行号范围筛选
            if (criteria.StartLine.HasValue)
            {
                query = query.Where(l => l.LineNumber >= criteria.StartLine.Value);
            }

            if (criteria.EndLine.HasValue)
            {
                query = query.Where(l => l.LineNumber <= criteria.EndLine.Value);
            }

            // 6. 自定义表达式筛选
            if (!string.IsNullOrWhiteSpace(criteria.CustomExpression))
            {
                query = ApplyCustomExpression(query, criteria.CustomExpression);
            }

            // 7. 排除模式
            if (criteria.ExcludePatterns != null && criteria.ExcludePatterns.Any())
            {
                foreach (var pattern in criteria.ExcludePatterns)
                {
                    if (criteria.UseRegex)
                    {
                        try
                        {
                            var regex = new Regex(pattern, criteria.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                            query = query.Where(l => !regex.IsMatch(l.Message ?? ""));
                        }
                        catch
                        {
                            // 忽略无效的正则表达式
                        }
                    }
                    else
                    {
                        var comparison = criteria.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        query = query.Where(l => !l.Message?.Contains(pattern, comparison) ?? false);
                    }
                }
            }

            // 8. 结果排序
            query = ApplySorting(query, criteria);

            return query.ToList();
        }

        /// <summary>
        /// 应用关键字筛选
        /// </summary>
        private IEnumerable<LogEntry> ApplyKeywordFilter(IEnumerable<LogEntry> query, LogFilterCriteria criteria)
        {
            var keyword = criteria.Keyword!;

            if (criteria.UseRegex)
            {
                // 正则表达式搜索
                try
                {
                    var regex = new Regex(keyword, criteria.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                    return query.Where(l =>
                        regex.IsMatch(l.Message ?? "") ||
                        regex.IsMatch(l.Source ?? ""));
                }
                catch (ArgumentException ex)
                {
                    // 无效的正则表达式,降级为普通搜索
                    return ApplyNormalSearch(query, keyword, criteria.CaseSensitive);
                }
            }
            else if (criteria.FuzzyMatch)
            {
                // 模糊匹配(编辑距离)
                return query.Where(l =>
                    IsFuzzyMatch(l.Message ?? "", keyword, criteria.FuzzyThreshold) ||
                    IsFuzzyMatch(l.Source ?? "", keyword, criteria.FuzzyThreshold));
            }
            else
            {
                // 普通搜索
                return ApplyNormalSearch(query, keyword, criteria.CaseSensitive);
            }
        }

        /// <summary>
        /// 普通关键字搜索
        /// </summary>
        private IEnumerable<LogEntry> ApplyNormalSearch(IEnumerable<LogEntry> query, string keyword, bool caseSensitive)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return query.Where(l =>
                (l.Message?.Contains(keyword, comparison) ?? false) ||
                (l.Source?.Contains(keyword, comparison) ?? false));
        }

        /// <summary>
        /// 模糊匹配(Levenshtein距离)
        /// </summary>
        private bool IsFuzzyMatch(string text, string pattern, double threshold)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return false;

            var distance = LevenshteinDistance(text, pattern);
            var maxLength = Math.Max(text.Length, pattern.Length);
            var similarity = 1.0 - (double)distance / maxLength;

            return similarity >= threshold;
        }

        /// <summary>
        /// 计算Levenshtein距离
        /// </summary>
        private int LevenshteinDistance(string s1, string s2)
        {
            var m = s1.Length;
            var n = s2.Length;
            var dp = new int[m + 1, n + 1];

            for (int i = 0; i <= m; i++)
                dp[i, 0] = i;

            for (int j = 0; j <= n; j++)
                dp[0, j] = j;

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);
                }
            }

            return dp[m, n];
        }

        /// <summary>
        /// 应用自定义表达式筛选
        /// 表达式示例: "(Level = 'ERROR' OR Level = 'FATAL') AND Message CONTAINS 'timeout'"
        /// </summary>
        private IEnumerable<LogEntry> ApplyCustomExpression(IEnumerable<LogEntry> query, string expression)
        {
            try
            {
                // 解析表达式并生成谓词
                var predicate = ParseCustomExpression(expression);
                return query.Where(predicate);
            }
            catch
            {
                // 无效的表达式,返回原查询
                return query;
            }
        }

        /// <summary>
        /// 解析自定义表达式
        /// </summary>
        private Func<LogEntry, bool> ParseCustomExpression(string expression)
        {
            // 简化的表达式解析器
            // 支持的操作符: =, !=, CONTAINS, AND, OR, NOT, (, )

            expression = expression.ToUpperInvariant();

            // 处理OR操作
            var orParts = SplitByOperator(expression, " OR ");
            if (orParts.Count > 1)
            {
                return l => orParts.Any(part => ParseCustomExpression(part)(l));
            }

            // 处理AND操作
            var andParts = SplitByOperator(expression, " AND ");
            if (andParts.Count > 1)
            {
                return l => andParts.All(part => ParseCustomExpression(part)(l));
            }

            // 处理NOT操作
            if (expression.StartsWith("NOT "))
            {
                var innerExpression = expression.Substring(4);
                var innerPredicate = ParseCustomExpression(innerExpression);
                return l => !innerPredicate(l);
            }

            // 处理括号
            if (expression.StartsWith("(") && expression.EndsWith(")"))
            {
                return ParseCustomExpression(expression.Substring(1, expression.Length - 2));
            }

            // 处理基本条件
            return ParseBasicCondition(expression);
        }

        /// <summary>
        /// 解析基本条件
        /// </summary>
        private Func<LogEntry, bool> ParseBasicCondition(string condition)
        {
            // 格式: Field Operator Value
            // 示例: Level = 'ERROR'

            if (condition.Contains("="))
            {
                var parts = condition.Split('=');
                if (parts.Length == 2)
                {
                    var field = parts[0].Trim();
                    var value = parts[1].Trim().Trim('\'', '"');
                    return l => GetFieldValue(l, field)?.Equals(value, StringComparison.OrdinalIgnoreCase) ?? false;
                }
            }

            if (condition.Contains("CONTAINS"))
            {
                var parts = condition.Split(new[] { "CONTAINS" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var field = parts[0].Trim();
                    var value = parts[1].Trim().Trim('\'', '"');
                    return l => GetFieldValue(l, field)?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false;
                }
            }

            // 默认返回true
            return l => true;
        }

        /// <summary>
        /// 获取字段值
        /// </summary>
        private string? GetFieldValue(LogEntry entry, string fieldName)
        {
            return fieldName.ToUpperInvariant() switch
            {
                "LEVEL" => entry.Level,
                "MESSAGE" => entry.Message,
                "SOURCE" => entry.Source,
                "TIMESTAMP" => entry.Timestamp?.ToString(),
                _ => null
            };
        }

        /// <summary>
        /// 按操作符分割表达式
        /// </summary>
        private List<string> SplitByOperator(string expression, string op)
        {
            var result = new List<string>();
            var depth = 0;
            var start = 0;

            for (int i = 0; i < expression.Length - op.Length + 1; i++)
            {
                if (expression[i] == '(') depth++;
                if (expression[i] == ')') depth--;

                if (depth == 0 && expression.Substring(i, op.Length) == op)
                {
                    result.Add(expression.Substring(start, i - start).Trim());
                    start = i + op.Length;
                    i += op.Length - 1;
                }
            }

            if (start < expression.Length)
            {
                result.Add(expression.Substring(start).Trim());
            }

            return result;
        }

        /// <summary>
        /// 应用排序
        /// </summary>
        private IEnumerable<LogEntry> ApplySorting(IEnumerable<LogEntry> query, LogFilterCriteria criteria)
        {
            return criteria.SortBy?.ToUpperInvariant() switch
            {
                "TIMESTAMP" => criteria.SortDescending
                    ? query.OrderByDescending(l => l.Timestamp)
                    : query.OrderBy(l => l.Timestamp),
                "LEVEL" => criteria.SortDescending
                    ? query.OrderByDescending(l => GetLevelPriority(l.Level))
                    : query.OrderBy(l => GetLevelPriority(l.Level)),
                "LINE" => criteria.SortDescending
                    ? query.OrderByDescending(l => l.LineNumber)
                    : query.OrderBy(l => l.LineNumber),
                "SOURCE" => criteria.SortDescending
                    ? query.OrderByDescending(l => l.Source)
                    : query.OrderBy(l => l.Source),
                _ => query
            };
        }

        /// <summary>
        /// 获取日志级别优先级
        /// </summary>
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

        /// <summary>
        /// 保存筛选条件
        /// </summary>
        public void SaveFilterCriteria(string name, LogFilterCriteria criteria)
        {
            // TODO: 保存到数据库或配置文件
            var criteriaJson = Newtonsoft.Json.JsonConvert.SerializeObject(criteria);
            // 保存逻辑...
        }

        /// <summary>
        /// 加载筛选条件
        /// </summary>
        public LogFilterCriteria? LoadFilterCriteria(string name)
        {
            // TODO: 从数据库或配置文件加载
            return null;
        }

        /// <summary>
        /// 获取所有保存的筛选条件
        /// </summary>
        public List<SavedFilter> GetSavedFilters()
        {
            // TODO: 从数据库或配置文件加载
            return new List<SavedFilter>();
        }
    }

    #region 辅助类型

    public class LogFilterCriteria
    {
        // 基本筛选条件
        public List<string>? Levels { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Keyword { get; set; }
        public List<string>? Sources { get; set; }
        public int? StartLine { get; set; }
        public int? EndLine { get; set; }

        // 高级筛选选项
        public bool UseRegex { get; set; }
        public bool CaseSensitive { get; set; }
        public bool FuzzyMatch { get; set; }
        public double FuzzyThreshold { get; set; } = 0.7; // 模糊匹配阈值(0-1)

        // 排除模式
        public List<string>? ExcludePatterns { get; set; }

        // 自定义表达式
        public string? CustomExpression { get; set; }

        // 排序选项
        public string? SortBy { get; set; } // TIMESTAMP, LEVEL, LINE, SOURCE
        public bool SortDescending { get; set; } = true;
    }

    public class LogEntry
    {
        public int LineNumber { get; set; }
        public DateTime? Timestamp { get; set; }
        public string? Level { get; set; }
        public string? Source { get; set; }
        public string? Message { get; set; }
        public bool HasError { get; set; }
    }

    public class SavedFilter
    {
        public string Name { get; set; } = "";
        public LogFilterCriteria Criteria { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
        public int UseCount { get; set; }
    }

    #endregion
}
