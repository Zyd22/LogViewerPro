using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LogViewerPro.WPF.Services.AIService
{
    /// <summary>
    /// 离线规则引擎 - 当AI模型不可用时的降级方案
    /// </summary>
    public class OfflineRuleEngine
    {
        private readonly Dictionary<string, CommandRule> _commandRules;

        public OfflineRuleEngine()
        {
            _commandRules = InitializeRules();
        }

        /// <summary>
        /// 处理用户消息(基于规则)
        /// </summary>
        public string ProcessMessage(string userMessage)
        {
            // 1. 尝试匹配预定义命令
            foreach (var rule in _commandRules.Values)
            {
                if (rule.Pattern.IsMatch(userMessage))
                {
                    return rule.Execute(userMessage);
                }
            }

            // 2. 检查是否为常见问题
            var faqResponse = CheckFAQ(userMessage);
            if (!string.IsNullOrEmpty(faqResponse))
            {
                return faqResponse;
            }

            // 3. 默认回复
            return GetDefaultResponse();
        }

        /// <summary>
        /// 初始化命令规则
        /// </summary>
        private Dictionary<string, CommandRule> InitializeRules()
        {
            return new Dictionary<string, CommandRule>
            {
                // 日志分析命令
                ["analyze_log"] = new CommandRule
                {
                    Pattern = new Regex(@"分析.*日志|日志.*分析|统计.*日志", RegexOptions.IgnoreCase),
                    Category = "日志分析",
                    Execute = msg => "【离线模式】日志分析功能:\n" +
                                   "1. 上传日志文件\n" +
                                   "2. 使用高级筛选功能\n" +
                                   "3. 查看统计图表\n" +
                                   "提示: 启动Ollama服务可获得AI智能分析能力"
                },

                // 筛选日志命令
                ["filter_log"] = new CommandRule
                {
                    Pattern = new Regex(@"筛选|过滤|查找.*日志|搜索", RegexOptions.IgnoreCase),
                    Category = "日志筛选",
                    Execute = msg => "【离线模式】日志筛选支持:\n" +
                                   "• 多条件组合筛选\n" +
                                   "• 正则表达式搜索\n" +
                                   "• 模糊匹配\n" +
                                   "• 时间范围过滤\n" +
                                   "• 日志级别筛选"
                },

                // 数据转换命令
                ["transform"] = new CommandRule
                {
                    Pattern = new Regex(@"转换|格式.*转换|json.*xml|xml.*json", RegexOptions.IgnoreCase),
                    Category = "数据转换",
                    Execute = msg => "【离线模式】支持的数据格式转换:\n" +
                                   "• JSON ↔ XML\n" +
                                   "• JSON ↔ CSV\n" +
                                   "• JSON ↔ YAML\n" +
                                   "• XML ↔ CSV\n" +
                                   "请在'数据转换'模块操作"
                },

                // 项目分析命令
                ["analyze_project"] = new CommandRule
                {
                    Pattern = new Regex(@"分析.*项目|项目.*分析|学习.*项目", RegexOptions.IgnoreCase),
                    Category = "项目分析",
                    Execute = msg => "【离线模式】C#项目分析功能:\n" +
                                   "• 项目结构扫描\n" +
                                   "• 技术栈识别\n" +
                                   "• 架构模式检测\n" +
                                   "• API提取\n" +
                                   "• 文档生成\n" +
                                   "请使用'项目学习'功能"
                },

                // IoT检测命令
                ["iot"] = new CommandRule
                {
                    Pattern = new Regex(@"iot|物联网|工控|硬件|modbus|plc", RegexOptions.IgnoreCase),
                    Category = "IoT分析",
                    Execute = msg => "【离线模式】工控IoT检测功能:\n" +
                                   "• 硬件平台识别(Raspberry Pi, Arduino, ESP32)\n" +
                                   "• 工业协议检测(I2C, SPI, UART, Modbus)\n" +
                                   "• 驱动模式分析\n" +
                                   "• 示例项目提取"
                },

                // 帮助命令
                ["help"] = new CommandRule
                {
                    Pattern = new Regex(@"帮助|help|使用.*说明|功能.*介绍", RegexOptions.IgnoreCase),
                    Category = "帮助",
                    Execute = msg => GetHelpText()
                },

                // 系统信息命令
                ["system_info"] = new CommandRule
                {
                    Pattern = new Regex(@"系统.*信息|环境.*信息|版本", RegexOptions.IgnoreCase),
                    Category = "系统",
                    Execute = msg => $"【系统信息】\n" +
                                    $"应用版本: LogViewerPro 2.0\n" +
                                    $"运行平台: {Environment.OSVersion}\n" +
                                    $".NET版本: {Environment.Version}\n" +
                                    $"CPU核心数: {Environment.ProcessorCount}\n" +
                                    $"工作目录: {Environment.CurrentDirectory}"
                }
            };
        }

        /// <summary>
        /// 检查常见问题
        /// </summary>
        private string? CheckFAQ(string userMessage)
        {
            var lowerMsg = userMessage.ToLowerInvariant();

            if (lowerMsg.Contains("如何") && lowerMsg.Contains("上传"))
            {
                return "【上传文件】\n" +
                       "1. 点击左侧菜单'文件管理'\n" +
                       "2. 选择'上传文件'按钮\n" +
                       "3. 选择文件(支持.log, .txt, .json, .xml, .zip)\n" +
                       "4. 等待验证和上传完成\n" +
                       "注意: 单文件最大500MB, 压缩包最大2GB";
            }

            if (lowerMsg.Contains("为什么") && lowerMsg.Contains("离线"))
            {
                return "当前运行在离线模式,原因可能是:\n" +
                       "1. Ollama服务未启动\n" +
                       "2. 未安装AI模型\n" +
                       "3. 系统资源不足\n\n" +
                       "解决方法:\n" +
                       "• 安装Ollama: https://ollama.ai\n" +
                       "• 下载模型: ollama pull tinyllama\n" +
                       "• 启动服务: ollama serve";
            }

            if (lowerMsg.Contains("支持") && lowerMsg.Contains("格式"))
            {
                return "【支持的文件格式】\n" +
                       "日志文件: .log, .txt\n" +
                       "数据文件: .json, .xml, .csv, .yaml\n" +
                       "压缩文件: .zip, .rar, .7z, .tar, .gz\n" +
                       "项目文件: .csproj, .sln, .cs";
            }

            return null;
        }

        /// <summary>
        /// 获取默认回复
        /// </summary>
        private string GetDefaultResponse()
        {
            return "【离线模式】抱歉,我无法理解您的请求。\n\n" +
                   "您可以:\n" +
                   "• 输入 '帮助' 查看功能说明\n" +
                   "• 使用左侧菜单的各项功能\n" +
                   "• 启动Ollama服务获得AI智能助手\n\n" +
                   "常用命令:\n" +
                   "• 分析日志\n" +
                   "• 筛选日志\n" +
                   "• 转换格式\n" +
                   "• 分析项目";
        }

        /// <summary>
        /// 获取帮助文本
        /// </summary>
        private string GetHelpText()
        {
            return "【LogViewerPro 功能指南】\n\n" +
                   "📂 文件管理\n" +
                   "  • 上传文件(支持验证)\n" +
                   "  • 文件可视化\n" +
                   "  • 数据格式转换\n\n" +

                   "📊 日志管理\n" +
                   "  • 日志解析\n" +
                   "  • 高级筛选(支持正则)\n" +
                   "  • 统计图表\n" +
                   "  • 导出功能\n\n" +

                   "🤖 AI助手\n" +
                   "  • 智能对话\n" +
                   "  • 日志分析\n" +
                   "  • 离线规则引擎\n\n" +

                   "🔧 项目学习\n" +
                   "  • C#项目分析\n" +
                   "  • IoT设备检测\n" +
                   "  • 文档生成\n\n" +

                   "💡 提示:\n" +
                   "启动Ollama服务可解锁AI智能功能";
        }
    }

    /// <summary>
    /// 命令规则
    /// </summary>
    public class CommandRule
    {
        public Regex Pattern { get; set; } = new Regex("");
        public string Category { get; set; } = "";
        public Func<string, string> Execute { get; set; } = msg => "";
    }
}
