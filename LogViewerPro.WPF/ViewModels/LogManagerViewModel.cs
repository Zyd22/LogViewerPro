using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Mvvm;
using Prism.Commands;
using System.IO;
using Microsoft.Win32;
using LogViewerPro.WPF.Services.FileService;
using LogViewerPro.WPF.Services.LogService;

namespace LogViewerPro.WPF.ViewModels
{
    public class LogManagerViewModel : BindableBase
    {
        private readonly FileUploader _fileUploader;
        private readonly LogFilter _logFilter;

        private string _searchText = "";
        private bool _useRegex;
        private bool _caseSensitive;
        private DateTime? _startTime;
        private DateTime? _endTime;

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool UseRegex
        {
            get => _useRegex;
            set => SetProperty(ref _useRegex, value);
        }

        public bool CaseSensitive
        {
            get => _caseSensitive;
            set => SetProperty(ref _caseSensitive, value);
        }

        public DateTime? StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        public ObservableCollection<LogEntry> Logs { get; set; }
        public ObservableCollection<LogEntry> FilteredLogs { get; set; }

        public ICommand OpenFileCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand ExportCommand { get; }

        public LogManagerViewModel(FileUploader fileUploader, LogFilter logFilter)
        {
            _fileUploader = fileUploader;
            _logFilter = logFilter;

            Logs = new ObservableCollection<LogEntry>();
            FilteredLogs = new ObservableCollection<LogEntry>();

            OpenFileCommand = new DelegateCommand(async () => await OpenFileAsync());
            ApplyFilterCommand = new DelegateCommand(ApplyFilter);
            ClearFilterCommand = new DelegateCommand(ClearFilter);
            ExportCommand = new DelegateCommand(ExportLogs);
        }

        private async Task OpenFileAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "日志文件|*.log;*.txt|所有文件|*.*",
                Title = "选择日志文件"
            };

            if (dialog.ShowDialog() == true)
            {
                // 实现文件打开和解析逻辑
                await Task.Run(() =>
                {
                    // 解析日志文件
                    var lines = File.ReadAllLines(dialog.FileName);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        // 简化的解析逻辑
                        var line = lines[i];
                        Logs.Add(new LogEntry
                        {
                            LineNumber = i + 1,
                            Message = line,
                            Level = DetectLevel(line),
                            Timestamp = DateTime.Now
                        });
                    }
                });
            }
        }

        private void ApplyFilter()
        {
            var criteria = new LogFilterCriteria
            {
                Keyword = SearchText,
                UseRegex = UseRegex,
                CaseSensitive = CaseSensitive,
                StartTime = StartTime,
                EndTime = EndTime
            };

            var filtered = _logFilter.FilterLogs(Logs.ToList(), criteria);
            FilteredLogs.Clear();
            foreach (var log in filtered)
            {
                FilteredLogs.Add(log);
            }
        }

        private void ClearFilter()
        {
            SearchText = "";
            UseRegex = false;
            CaseSensitive = false;
            StartTime = null;
            EndTime = null;

            FilteredLogs.Clear();
            foreach (var log in Logs)
            {
                FilteredLogs.Add(log);
            }
        }

        private void ExportLogs()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV文件|*.csv|Excel文件|*.xlsx",
                Title = "导出日志"
            };

            if (dialog.ShowDialog() == true)
            {
                // 实现导出逻辑
                var lines = FilteredLogs.Select(l => $"{l.LineNumber},{l.Timestamp},{l.Level},{l.Message}");
                File.WriteAllLines(dialog.FileName, lines);
            }
        }

        private string DetectLevel(string line)
        {
            var upper = line.ToUpper();
            if (upper.Contains("ERROR")) return "ERROR";
            if (upper.Contains("WARN")) return "WARN";
            if (upper.Contains("INFO")) return "INFO";
            if (upper.Contains("DEBUG")) return "DEBUG";
            if (upper.Contains("FATAL")) return "FATAL";
            return "INFO";
        }
    }
}
