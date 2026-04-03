using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Mvvm;
using Prism.Commands;
using LogViewerPro.WPF.Services.AIService;
using System.Threading.Tasks;

namespace LogViewerPro.WPF.ViewModels
{
    public class AIAssistantViewModel : BindableBase
    {
        private readonly OllamaModelDetector _modelDetector;
        private readonly OfflineRuleEngine _offlineEngine;

        private string _userInput = "";
        private bool _isProcessing;
        private AIModel? _currentModel;

        public string UserInput
        {
            get => _userInput;
            set => SetProperty(ref _userInput, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public AIModel? CurrentModel
        {
            get => _currentModel;
            set => SetProperty(ref _currentModel, value);
        }

        public ObservableCollection<ChatMessage> Messages { get; }
        public ObservableCollection<AIModel> AvailableModels { get; }

        public ICommand SendMessageCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand UseToolCommand { get; }

        public AIAssistantViewModel(OllamaModelDetector modelDetector, OfflineRuleEngine offlineEngine)
        {
            _modelDetector = modelDetector;
            _offlineEngine = offlineEngine;

            Messages = new ObservableCollection<ChatMessage>();
            AvailableModels = new ObservableCollection<AIModel>();

            SendMessageCommand = new DelegateCommand(async () => await SendMessageAsync(), () => !IsProcessing && !string.IsNullOrWhiteSpace(UserInput));
            ClearHistoryCommand = new DelegateCommand(ClearHistory);
            UseToolCommand = new DelegateCommand<string>(UseTool);

            // 添加欢迎消息
            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = "你好!我是LogViewer Pro的智能助手。我可以帮你分析日志、转换数据、分析项目等。有什么可以帮你的吗?",
                Timestamp = DateTime.Now
            });

            InitializeModels();
        }

        private async void InitializeModels()
        {
            var result = await _modelDetector.DetectAvailableModelsAsync();
            
            AvailableModels.Clear();
            foreach (var model in result.Models)
            {
                AvailableModels.Add(model);
            }

            CurrentModel = result.RecommendedModel;
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserInput)) return;

            var userMessage = UserInput;
            UserInput = "";
            IsProcessing = true;

            // 添加用户消息
            Messages.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage,
                Timestamp = DateTime.Now
            });

            try
            {
                // 使用离线引擎处理
                var response = _offlineEngine.ProcessMessage(userMessage);

                await Task.Delay(500); // 模拟处理延迟

                // 添加AI回复
                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                Messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = $"抱歉,处理消息时出错: {ex.Message}",
                    Timestamp = DateTime.Now
                });
            }
            finally
            {
                IsProcessing = false;
                (SendMessageCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void ClearHistory()
        {
            Messages.Clear();
            Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = "会话已清空。有什么可以帮你的吗?",
                Timestamp = DateTime.Now
            });
        }

        private void UseTool(string toolName)
        {
            // 根据工具名称插入提示文本
            UserInput = toolName switch
            {
                "analyze_logs" => "请帮我分析日志文件",
                "filter_logs" => "请帮我筛选日志",
                "transform_data" => "请帮我转换数据格式",
                "validate_data" => "请帮我验证数据",
                "compare_data" => "请帮我对比数据",
                "analyze_project" => "请帮我分析项目",
                _ => toolName
            };
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = ""; // "user" 或 "assistant"
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
