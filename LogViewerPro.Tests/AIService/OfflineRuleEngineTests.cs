using FluentAssertions;
using LogViewerPro.WPF.Services.AIService;
using Xunit;

namespace LogViewerPro.Tests.AIService
{
    /// <summary>
    /// 离线规则引擎测试
    /// </summary>
    public class OfflineRuleEngineTests
    {
        private readonly OfflineRuleEngine _engine;

        public OfflineRuleEngineTests()
        {
            _engine = new OfflineRuleEngine();
        }

        #region 命令匹配测试

        [Fact]
        public void ProcessMessage_LogAnalysisCommand_ShouldReturnLogHelp()
        {
            // Arrange
            var message = "帮我分析日志";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("日志分析");
            response.Should().Contain("离线模式");
        }

        [Fact]
        public void ProcessMessage_FilterCommand_ShouldReturnFilterHelp()
        {
            // Arrange
            var message = "如何筛选日志?";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("筛选");
            response.Should().Contain("正则表达式");
        }

        [Fact]
        public void ProcessMessage_TransformCommand_ShouldReturnTransformHelp()
        {
            // Arrange
            var message = "json转xml";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("转换");
            response.Should().Contain("JSON");
            response.Should().Contain("XML");
        }

        [Fact]
        public void ProcessMessage_ProjectAnalysisCommand_ShouldReturnProjectHelp()
        {
            // Arrange
            var message = "分析C#项目";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("项目");
            response.Should().Contain("分析");
        }

        [Fact]
        public void ProcessMessage_IoTCommand_ShouldReturnIoTHelp()
        {
            // Arrange
            var message = "检测IoT设备";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("IoT");
            response.Should().Contain("工控");
        }

        [Fact]
        public void ProcessMessage_HelpCommand_ShouldReturnHelpText()
        {
            // Arrange
            var message = "帮助";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("功能指南");
            response.Should().Contain("文件管理");
            response.Should().Contain("日志管理");
        }

        #endregion

        #region FAQ测试

        [Fact]
        public void ProcessMessage_HowToUpload_ShouldReturnUploadInstructions()
        {
            // Arrange
            var message = "如何上传文件?";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("上传文件");
            response.Should().Contain("步骤");
        }

        [Fact]
        public void ProcessMessage_WhyOffline_ShouldExplainOfflineMode()
        {
            // Arrange
            var message = "为什么是离线模式?";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("离线模式");
            response.Should().Contain("Ollama");
        }

        [Fact]
        public void ProcessMessage_SupportedFormats_ShouldListFormats()
        {
            // Arrange
            var message = "支持哪些格式?";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("支持");
            response.Should().Contain(".log");
            response.Should().Contain(".json");
            response.Should().Contain(".xml");
        }

        #endregion

        #region 默认回复测试

        [Fact]
        public void ProcessMessage_UnknownMessage_ShouldReturnDefaultResponse()
        {
            // Arrange
            var message = "这是一条无法识别的消息";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("无法理解");
            response.Should().Contain("帮助");
        }

        #endregion

        #region 系统信息测试

        [Fact]
        public void ProcessMessage_SystemInfoCommand_ShouldReturnSystemInfo()
        {
            // Arrange
            var message = "系统信息";

            // Act
            var response = _engine.ProcessMessage(message);

            // Assert
            response.Should().Contain("系统信息");
            response.Should().Contain("版本");
            response.Should().Contain("CPU");
        }

        #endregion
    }
}
