using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LogViewerPro.WPF.Services.AIService
{
    /// <summary>
    /// Ollama本地AI模型检测器 - 自动检测已安装模型
    /// </summary>
    public class OllamaModelDetector
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaEndpoint;

        public OllamaModelDetector(string ollamaEndpoint = "http://localhost:11434")
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _ollamaEndpoint = ollamaEndpoint;
        }

        /// <summary>
        /// 检测可用的AI模型
        /// </summary>
        public async Task<ModelDetectionResult> DetectAvailableModelsAsync()
        {
            var result = new ModelDetectionResult();

            try
            {
                // 1. 检查Ollama服务是否运行
                var serviceRunning = await CheckOllamaServiceAsync();
                if (!serviceRunning)
                {
                    result.ErrorMessage = "Ollama服务未运行,请启动Ollama服务";
                    result.OfflineMode = true;
                    result.Models = GetOfflineModels();
                    return result;
                }

                // 2. 获取已安装的模型列表
                var models = await GetInstalledModelsAsync();
                if (models == null || !models.Any())
                {
                    result.ErrorMessage = "未检测到已安装的AI模型\n请使用 'ollama pull <模型名>' 安装模型";
                    result.OfflineMode = true;
                    result.Models = GetOfflineModels();
                    return result;
                }

                // 3. 测试每个模型的性能
                foreach (var model in models)
                {
                    await TestModelPerformanceAsync(model);
                }

                // 4. 根据硬件配置推荐最佳模型
                var recommended = RecommendBestModel(models);

                result.Success = true;
                result.Models = models;
                result.RecommendedModel = recommended;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"模型检测失败: {ex.Message}";
                result.OfflineMode = true;
                result.Models = GetOfflineModels();
            }

            return result;
        }

        /// <summary>
        /// 检查Ollama服务是否运行
        /// </summary>
        private async Task<bool> CheckOllamaServiceAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ollamaEndpoint}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取已安装的模型列表
        /// </summary>
        private async Task<List<AIModel>> GetInstalledModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ollamaEndpoint}/api/tags");
                var json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);

                var models = new List<AIModel>();

                foreach (var model in data["models"] ?? new JArray())
                {
                    models.Add(new AIModel
                    {
                        Name = model["name"]?.ToString() ?? "",
                        Size = ParseSize(model["size"]?.ToString() ?? "0"),
                        ModifiedAt = model["modified_at"]?.ToString() ?? "",
                        Digest = model["digest"]?.ToString() ?? "",
                        Available = true
                    });
                }

                return models;
            }
            catch
            {
                return new List<AIModel>();
            }
        }

        /// <summary>
        /// 测试模型性能
        /// </summary>
        private async Task TestModelPerformanceAsync(AIModel model)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                var request = new
                {
                    model = model.Name,
                    prompt = "test",
                    stream = false,
                    options = new
                    {
                        num_predict = 1
                    }
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_ollamaEndpoint}/api/generate", content);
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    model.ResponseTime = stopwatch.ElapsedMilliseconds;
                    model.Available = true;
                }
                else
                {
                    model.Available = false;
                    model.ErrorMessage = $"响应失败: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                model.Available = false;
                model.ErrorMessage = ex.Message;
            }
        }

        /// <summary>
        /// 根据硬件配置推荐最佳模型
        /// </summary>
        private AIModel? RecommendBestModel(List<AIModel> models)
        {
            // 获取系统信息
            var totalMemoryGB = GetTotalMemoryGB();
            var cpuCores = Environment.ProcessorCount;

            // 推荐策略:
            // 内存 < 8GB: 推荐小模型(tinyllama, phi)
            // 内存 8-16GB: 推荐中等模型(llama2, mistral)
            // 内存 > 16GB: 推荐大模型(llama3, codellama)

            var preferredModels = totalMemoryGB switch
            {
                < 8 => new[] { "tinyllama", "phi", "gemma:2b" },
                < 16 => new[] { "llama2", "mistral", "codellama" },
                _ => new[] { "llama3", "codellama", "mixtral" }
            };

            // 查找匹配的模型
            foreach (var preferred in preferredModels)
            {
                var match = models.FirstOrDefault(m =>
                    m.Name.StartsWith(preferred, StringComparison.OrdinalIgnoreCase) &&
                    m.Available);

                if (match != null)
                {
                    match.Recommended = true;
                    return match;
                }
            }

            // 如果没有匹配的,返回第一个可用模型
            var firstAvailable = models.FirstOrDefault(m => m.Available);
            if (firstAvailable != null)
            {
                firstAvailable.Recommended = true;
                return firstAvailable;
            }

            return null;
        }

        /// <summary>
        /// 获取系统总内存(GB)
        /// </summary>
        private long GetTotalMemoryGB()
        {
            try
            {
                using var proc = Process.GetCurrentProcess();
                // 简化实现,实际应使用PerformanceCounter或WMI
                return 8; // 默认返回8GB
            }
            catch
            {
                return 8;
            }
        }

        /// <summary>
        /// 获取离线模型列表
        /// </summary>
        private List<AIModel> GetOfflineModels()
        {
            return new List<AIModel>
            {
                new AIModel
                {
                    Name = "离线规则引擎",
                    Type = ModelType.Offline,
                    Available = true,
                    Description = "基于规则的离线助手,无需AI模型"
                }
            };
        }

        /// <summary>
        /// 解析大小字符串
        /// </summary>
        private long ParseSize(string sizeStr)
        {
            if (long.TryParse(sizeStr, out var size))
                return size;

            // 处理 "1.2 GB" 这样的格式
            var parts = sizeStr.Split(' ');
            if (parts.Length == 2 && double.TryParse(parts[0], out var value))
            {
                var unit = parts[1].ToUpperInvariant();
                return unit switch
                {
                    "B" => (long)value,
                    "KB" => (long)(value * 1024),
                    "MB" => (long)(value * 1024 * 1024),
                    "GB" => (long)(value * 1024 * 1024 * 1024),
                    _ => (long)value
                };
            }

            return 0;
        }
    }

    #region 辅助类型

    public class ModelDetectionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool OfflineMode { get; set; }
        public List<AIModel> Models { get; set; } = new();
        public AIModel? RecommendedModel { get; set; }
    }

    public class AIModel
    {
        public string Name { get; set; } = "";
        public ModelType Type { get; set; } = ModelType.Online;
        public long Size { get; set; }
        public string ModifiedAt { get; set; } = "";
        public string Digest { get; set; } = "";
        public bool Available { get; set; }
        public long ResponseTime { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Recommended { get; set; }
        public string? Description { get; set; }
    }

    public enum ModelType
    {
        Online,
        Offline
    }

    #endregion
}
