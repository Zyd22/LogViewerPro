using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.DryIoc;
using Prism.Ioc;
using LogViewerPro.WPF.Services.FileService;
using LogViewerPro.WPF.Services.LogService;
using LogViewerPro.WPF.Services.AIService;
using LogViewerPro.WPF.ViewModels;
using LogViewerPro.WPF.Views;

namespace LogViewerPro.WPF
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册日志服务
            var serviceProvider = ConfigureServices();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            // 注册文件服务
            containerRegistry.RegisterSingleton<FileUploader>();
            containerRegistry.RegisterSingleton<StreamZipExtractor>();

            // 注册日志服务
            containerRegistry.RegisterSingleton<LogFilter>();

            // 注册AI服务
            containerRegistry.RegisterSingleton<OllamaModelDetector>();
            containerRegistry.RegisterSingleton<OfflineRuleEngine>();

            // 注册ViewModels
            containerRegistry.RegisterSingleton<MainViewModel>();
            containerRegistry.RegisterSingleton<LogManagerViewModel>();
            containerRegistry.RegisterSingleton<AIAssistantViewModel>();
            containerRegistry.RegisterSingleton<ProjectLearnerViewModel>();

            // 注册Views
            containerRegistry.RegisterForNavigation<Views.LogManagerView>();
            containerRegistry.RegisterForNavigation<Views.AIAssistantView>();
            containerRegistry.RegisterForNavigation<Views.ProjectLearnerView>();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // 添加日志
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 设置全局异常处理
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            base.OnStartup(e);
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            LogUnhandledException(exception, "AppDomain.UnhandledException");
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogUnhandledException(e.Exception, "Dispatcher.UnhandledException");
            e.Handled = true; // 防止应用崩溃
        }

        private void LogUnhandledException(Exception? exception, string source)
        {
            if (exception == null) return;

            // 记录日志
            var logger = Container?.Resolve<ILogger<App>>();
            logger?.LogCritical(exception, "未处理的异常 - {Source}", source);

            // 显示友好错误消息
            MessageBox.Show(
                $"应用程序遇到意外错误:\n\n{exception.Message}\n\n请联系技术支持。",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // 保存崩溃日志
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LogViewerPro",
                    "crash.log");

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {exception}\n\n");
            }
            catch
            {
                // 忽略日志写入错误
            }
        }
    }
}
