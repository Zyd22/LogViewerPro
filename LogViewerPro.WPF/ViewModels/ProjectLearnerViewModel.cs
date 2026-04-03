using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Mvvm;
using Prism.Commands;
using Microsoft.Win32;
using System.IO;
using LogViewerPro.WPF.Services.FileService;
using System.Threading.Tasks;

namespace LogViewerPro.WPF.ViewModels
{
    public class ProjectLearnerViewModel : BindableBase
    {
        private readonly FileUploader _fileUploader;
        private readonly StreamZipExtractor _zipExtractor;

        private string _projectName = "";
        private int _progress;
        private string _progressText = "";
        private bool _isAnalyzing;

        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            set => SetProperty(ref _isAnalyzing, value);
        }

        public ProjectAnalysisResult AnalysisResult { get; set; }
        public ObservableCollection<string> Technologies { get; }
        public ObservableCollection<IoTDevice> IoTDevices { get; }
        public ObservableCollection<APIInfo> APIs { get; }

        public ICommand UploadProjectCommand { get; }
        public ICommand AnalyzeCommand { get; }
        public ICommand ExportDocumentCommand { get; }

        public ProjectLearnerViewModel(FileUploader fileUploader, StreamZipExtractor zipExtractor)
        {
            _fileUploader = fileUploader;
            _zipExtractor = zipExtractor;

            AnalysisResult = new ProjectAnalysisResult();
            Technologies = new ObservableCollection<string>();
            IoTDevices = new ObservableCollection<IoTDevice>();
            APIs = new ObservableCollection<APIInfo>();

            UploadProjectCommand = new DelegateCommand(async () => await UploadProjectAsync());
            AnalyzeCommand = new DelegateCommand(async () => await AnalyzeProjectAsync(), () => !IsAnalyzing);
            ExportDocumentCommand = new DelegateCommand(ExportDocument);
        }

        private async Task UploadProjectAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "项目文件|*.zip;*.rar;*.7z",
                Title = "选择项目压缩包"
            };

            if (dialog.ShowDialog() == true)
            {
                var filePath = dialog.FileName;
                ProjectName = Path.GetFileNameWithoutExtension(filePath);

                // 验证文件
                var result = await _fileUploader.UploadFileAsync(filePath, FileType.Archive);
                
                if (result.Success)
                {
                    // 开始解压和分析
                    await AnalyzeProjectAsync(filePath);
                }
                else
                {
                    System.Windows.MessageBox.Show(result.ErrorMessage, "上传失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task AnalyzeProjectAsync(string? filePath = null)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            IsAnalyzing = true;
            Progress = 0;
            ProgressText = "正在解压项目...";

            try
            {
                // 解压文件
                var extractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                var progress = new Progress<ExtractionProgress>(p =>
                {
                    Progress = p.Percentage;
                    ProgressText = $"解压中: {p.CurrentFile}";
                });

                var extractResult = await _zipExtractor.ExtractAsync(filePath, extractPath, progress);

                if (extractResult.Success)
                {
                    ProgressText = "正在分析项目...";
                    Progress = 50;

                    // 分析项目
                    await Task.Run(() => AnalyzeProject(extractPath));

                    Progress = 100;
                    ProgressText = "分析完成";

                    // 清理临时文件
                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show(extractResult.ErrorMessage, "解压失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"分析失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsAnalyzing = false;
                (AnalyzeCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void AnalyzeProject(string projectPath)
        {
            // 查找所有.cs文件
            var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories);

            AnalysisResult.TotalFiles = csFiles.Length;
            AnalysisResult.ProjectName = ProjectName;

            // 简化的分析逻辑
            foreach (var file in csFiles.Take(50))
            {
                var content = File.ReadAllText(file);

                // 检测技术栈
                DetectTechnologies(content);

                // 检测IoT设备
                DetectIoTDevices(content);

                // 提取API
                ExtractAPIs(file, content);
            }

            // 更新UI
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Technologies.Clear();
                foreach (var tech in AnalysisResult.Technologies)
                {
                    Technologies.Add(tech);
                }

                IoTDevices.Clear();
                foreach (var device in AnalysisResult.IoTDevices)
                {
                    IoTDevices.Add(device);
                }

                APIs.Clear();
                foreach (var api in AnalysisResult.APIs.Take(50))
                {
                    APIs.Add(api);
                }
            });
        }

        private void DetectTechnologies(string content)
        {
            if (content.Contains("ASP.NET") && !AnalysisResult.Technologies.Contains("ASP.NET Core"))
                AnalysisResult.Technologies.Add("ASP.NET Core");

            if (content.Contains("Entity Framework") && !AnalysisResult.Technologies.Contains("Entity Framework Core"))
                AnalysisResult.Technologies.Add("Entity Framework Core");

            if (content.Contains("WPF") && !AnalysisResult.Technologies.Contains("WPF"))
                AnalysisResult.Technologies.Add("WPF");

            if (content.Contains("MVVM") && !AnalysisResult.Technologies.Contains("MVVM"))
                AnalysisResult.Technologies.Add("MVVM");
        }

        private void DetectIoTDevices(string content)
        {
            if (content.Contains("RaspberryPi") || content.Contains("Raspberry Pi"))
            {
                if (!AnalysisResult.IoTDevices.Any(d => d.Name == "Raspberry Pi"))
                {
                    AnalysisResult.IoTDevices.Add(new IoTDevice
                    {
                        Name = "Raspberry Pi",
                        Type = "Single Board Computer",
                        Detected = true
                    });
                }
            }

            if (content.Contains("Arduino"))
            {
                if (!AnalysisResult.IoTDevices.Any(d => d.Name == "Arduino"))
                {
                    AnalysisResult.IoTDevices.Add(new IoTDevice
                    {
                        Name = "Arduino",
                        Type = "Microcontroller",
                        Detected = true
                    });
                }
            }

            if (content.Contains("ESP32"))
            {
                if (!AnalysisResult.IoTDevices.Any(d => d.Name == "ESP32"))
                {
                    AnalysisResult.IoTDevices.Add(new IoTDevice
                    {
                        Name = "ESP32",
                        Type = "WiFi Module",
                        Detected = true
                    });
                }
            }
        }

        private void ExtractAPIs(string filePath, string content)
        {
            // 简化的API提取
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("public ") && (line.Contains("(") && line.Contains(")")))
                {
                    AnalysisResult.APIs.Add(new APIInfo
                    {
                        ClassName = Path.GetFileNameWithoutExtension(filePath),
                        MemberName = line.Split('(')[0].Split(' ').Last(),
                        MemberType = line.Contains("class ") ? "Class" : "Method",
                        Signature = line
                    });
                }
            }
        }

        private void ExportDocument()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Markdown文件|*.md",
                Title = "导出学习文档",
                FileName = $"{ProjectName}_学习文档.md"
            };

            if (dialog.ShowDialog() == true)
            {
                var document = GenerateDocument();
                File.WriteAllText(dialog.FileName, document);
                System.Windows.MessageBox.Show("文档导出成功!", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private string GenerateDocument()
        {
            return $@"# {ProjectName} 项目学习文档

## 项目概述
- 项目名称: {ProjectName}
- 文件数量: {AnalysisResult.TotalFiles}

## 技术栈
{string.Join("\n", AnalysisResult.Technologies.Select(t => $"- {t}"))}

## IoT设备
{string.Join("\n", AnalysisResult.IoTDevices.Select(d => $"- {d.Name} ({d.Type})"))}

## API接口 (前50个)
{string.Join("\n", AnalysisResult.APIs.Take(50).Select(a => $"- {a.ClassName}.{a.MemberName}"))}

---
生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
";
        }
    }

    public class ProjectAnalysisResult
    {
        public string ProjectName { get; set; } = "";
        public int TotalFiles { get; set; }
        public List<string> Technologies { get; set; } = new List<string>();
        public List<IoTDevice> IoTDevices { get; set; } = new List<IoTDevice>();
        public List<APIInfo> APIs { get; set; } = new List<APIInfo>();
    }

    public class IoTDevice
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool Detected { get; set; }
    }

    public class APIInfo
    {
        public string ClassName { get; set; } = "";
        public string MemberName { get; set; } = "";
        public string MemberType { get; set; } = "";
        public string Signature { get; set; } = "";
    }
}
