using System.Threading.Tasks;
using FluentAssertions;
using LogViewerPro.WPF.Services.AIService;
using Xunit;

namespace LogViewerPro.Tests.AIService
{
    /// <summary>
    /// AI模型检测测试
    /// </summary>
    public class OllamaModelDetectorTests
    {
        private readonly OllamaModelDetector _detector;

        public OllamaModelDetectorTests()
        {
            _detector = new OllamaModelDetector("http://localhost:11434");
        }

        #region 服务检测测试

        [Fact]
        public async Task DetectAvailableModels_WhenOllamaNotRunning_ShouldReturnOfflineMode()
        {
            // Act
            var result = await _detector.DetectAvailableModelsAsync();

            // Assert
            // 在测试环境中,Ollama服务通常未运行
            result.Should().NotBeNull();
            if (!result.Success)
            {
                result.OfflineMode.Should().BeTrue();
                result.Models.Should().Contain(m => m.Name == "离线规则引擎");
            }
        }

        #endregion

        #region 离线模式测试

        [Fact]
        public async Task DetectAvailableModels_OfflineMode_ShouldReturnOfflineModel()
        {
            // Act
            var result = await _detector.DetectAvailableModelsAsync();

            // Assert
            result.Models.Should().NotBeEmpty();
            result.Models.Should().Contain(m => m.Type == ModelType.Offline);
        }

        #endregion

        #region 模型推荐测试

        [Fact]
        public async Task DetectAvailableModels_WhenModelsAvailable_ShouldRecommendOne()
        {
            // Act
            var result = await _detector.DetectAvailableModelsAsync();

            // Assert
            // 如果有可用模型,应该推荐一个
            if (result.Success && result.Models.Count > 0)
            {
                result.RecommendedModel.Should().NotBeNull();
                result.RecommendedModel!.Recommended.Should().BeTrue();
            }
        }

        #endregion
    }
}
