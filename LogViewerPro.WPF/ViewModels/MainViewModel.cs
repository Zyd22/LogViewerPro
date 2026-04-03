using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Mvvm;
using Prism.Commands;
using LogViewerPro.WPF.Services.AIService;
using System.Windows.Threading;

namespace LogViewerPro.WPF.ViewModels
{
    /// <summary>
    /// 主窗口ViewModel
    /// </summary>
    public class MainViewModel : BindableBase
    {
        private readonly OllamaModelDetector _modelDetector;
        private readonly OfflineRuleEngine _offlineEngine;
        private readonly DispatcherTimer _timer;

        private string _title = "LogViewer Pro - 工控上位机分析工具";
        private bool _isBusy;
        private string _statusMessage = "就绪";
        private AIModel? _currentModel;
        private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
        private int _selectedMenuIndex;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public AIModel? CurrentModel
        {
            get => _currentModel;
            set => SetProperty(ref _currentModel, value);
        }

        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        public int SelectedMenuIndex
        {
            get => _selectedMenuIndex;
            set => SetProperty(ref _selectedMenuIndex, value);
        }

        public ObservableCollection<MenuItem> MenuItems { get; }
        public ObservableCollection<AIModel> AvailableModels { get; }

        public ICommand LoadedCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand AboutCommand { get; }
        public ICommand SwitchModelCommand { get; }

        public MainViewModel(
            OllamaModelDetector modelDetector,
            OfflineRuleEngine offlineEngine)
        {
            _modelDetector = modelDetector;
            _offlineEngine = offlineEngine;

            // 初始化定时器
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            _timer.Start();

            MenuItems = new ObservableCollection<MenuItem>
            {
                new MenuItem { Icon = "📁", Title = "文件管理", Description = "上传、可视化、转换文件" },
                new MenuItem { Icon = "📊", Title = "日志管理", Description = "解析、筛选、统计日志" },
                new MenuItem { Icon = "🤖", Title = "AI助手", Description = "智能对话、日志分析" },
                new MenuItem { Icon = "🔧", Title = "项目学习", Description = "C#项目分析、IoT检测" },
                new MenuItem { Icon = "⚙️", Title = "系统设置", Description = "配置、备份、日志" }
            };

            AvailableModels = new ObservableCollection<AIModel>();

            LoadedCommand = new DelegateCommand(async () => await OnLoadedAsync());
            ExitCommand = new DelegateCommand(() => Application.Current.Shutdown());
            AboutCommand = new DelegateCommand(ShowAbout);
            SwitchModelCommand = new DelegateCommand<AIModel>(SwitchModel);
        }

        private async Task OnLoadedAsync()
        {
            StatusMessage = "正在检测AI模型...";
            IsBusy = true;

            try
            {
                var result = await _modelDetector.DetectAvailableModelsAsync();

                AvailableModels.Clear();
                foreach (var model in result.Models)
                {
                    AvailableModels.Add(model);
                }

                CurrentModel = result.RecommendedModel;

                if (result.OfflineMode)
                {
                    StatusMessage = "离线模式 - 规则引擎已启用";
                }
                else if (result.Success)
                {
                    StatusMessage = $"已连接AI模型: {CurrentModel?.Name}";
                }
                else
                {
                    StatusMessage = $"AI服务不可用: {result.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"模型检测失败: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SwitchModel(AIModel? model)
        {
            if (model == null) return;

            CurrentModel = model;
            StatusMessage = $"已切换到模型: {model.Name}";
        }

        private void ShowAbout()
        {
            var message = $"LogViewer Pro v2.0\n\n" +
                         "C#工控上位机项目分析工具\n\n" +
                         "功能特性:\n" +
                         "• 文件管理 - 安全上传、流式解压\n" +
                         "• 日志管理 - 高级筛选、智能分析\n" +
                         "• AI助手 - 智能对话、自动检测模型\n" +
                         "• 项目学习 - IoT检测、文档生成\n\n" +
                         "技术支持: ai-support@logviewerpro.com";

            MessageBox.Show(message, "关于 LogViewer Pro", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class MenuItem
    {
        public string Icon { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
