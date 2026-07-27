using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WSAppBak.Models;
using WSAppBak.Services;

namespace WSAppBak.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly AppRetrievalService _appRetrievalService;
        private readonly AppPackageService _appPackageService;

        private List<InstalledApp> _allInstalledApps;
        private InstalledApp _selectedApp;
        private string _outputPath;
        private string _infoText;
        private bool _isBusy;
        private bool _isControlsEnabled = true; // Default true
        private WSAppInfo _currentAppInfo;

        private bool _infoBarIsOpen;
        private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel()
        {
            _appRetrievalService = new AppRetrievalService();
            _appPackageService = new AppPackageService();

            StartCommand = new RelayCommand(async () => await StartProcessAsync(), CanStart);
            HelpCommand = new RelayCommand(OpenHelp);
            BrowseCommand = new RelayCommand(OpenFolderPicker);

            // Set default output path
            OutputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "WSAppBak");
            
            _ = LoadAppsAsync();
        }

        public RelayCommand StartCommand { get; }
        public RelayCommand HelpCommand { get; }
        public RelayCommand BrowseCommand { get; }

        private bool _isLoadingApps = true;

        public bool IsLoadingApps
        {
            get => _isLoadingApps;
            set { _isLoadingApps = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsContentVisible)); OnPropertyChanged(nameof(IsSplashVisible)); }
        }

        public bool IsContentVisible => !IsLoadingApps;
        public bool IsSplashVisible => IsLoadingApps;


        public List<InstalledApp> AllInstalledApps
        {
            get => _allInstalledApps;
            set { _allInstalledApps = value; OnPropertyChanged(); }
        }

        public InstalledApp SelectedApp
        {
            get => _selectedApp;
            set
            {
                _selectedApp = value;
                OnPropertyChanged();
                StartCommand.RaiseCanExecuteChanged();
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                _outputPath = value;
                OnPropertyChanged();
                StartCommand.RaiseCanExecuteChanged();
            }
        }

        public string InfoText
        {
            get => _infoText;
            set { _infoText = value; OnPropertyChanged(); }
        }

        public bool InfoBarIsOpen
        {
            get => _infoBarIsOpen;
            set { _infoBarIsOpen = value; OnPropertyChanged(); }
        }

        public InfoBarSeverity InfoBarSeverity
        {
            get => _infoBarSeverity;
            set { _infoBarSeverity = value; OnPropertyChanged(); }
        }

        public bool IsControlsEnabled
        {
            get => _isControlsEnabled;
            set { _isControlsEnabled = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }


        private async Task LoadAppsAsync()
        {
            IsLoadingApps = true;
            IsControlsEnabled = false;
            try
            {
                AllInstalledApps = await _appRetrievalService.GetInstalledAppsAsync();
            }
            finally
            {
                IsLoadingApps = false;
                IsControlsEnabled = true;
            }
        }

        private bool CanStart()
        {
            return SelectedApp != null && !string.IsNullOrWhiteSpace(OutputPath) && !IsBusy;
        }

        private async Task StartProcessAsync()
        {
            IsBusy = true;
            IsControlsEnabled = false;
            StartCommand.RaiseCanExecuteChanged();
            InfoText = string.Empty;
            InfoBarIsOpen = false;

            try
            {
                await RunBackupProcessAsync();
                await RenameOutputFilesAsync();
                InfoText = "Process Completed Successfully.";
                InfoBarSeverity = InfoBarSeverity.Success;
                InfoBarIsOpen = true;
            }
            catch (Exception ex)
            {
                InfoText = $"An error occurred: {ex.Message}";
                InfoBarSeverity = InfoBarSeverity.Error;
                InfoBarIsOpen = true;
            }
            finally
            {
                IsBusy = false;
                IsControlsEnabled = true;
                StartCommand.RaiseCanExecuteChanged();
            }
        }


        private async Task RunBackupProcessAsync()
        {
             InfoText = "Please Wait...";
             InfoBarSeverity = InfoBarSeverity.Informational;
             InfoBarIsOpen = true;
             
             // Prepare App Info
             var wsAppPath = SelectedApp.ManifestPath.Substring(0, SelectedApp.ManifestPath.LastIndexOf('\\'));
             var appName = CleanAppName(SelectedApp.Name);
             var wsAppOutputPath = Path.Combine(OutputPath.Trim('"'), appName);

             if (!File.Exists(SelectedApp.ManifestPath))
             {
                 throw new FileNotFoundException($"Invalid App Path. AppxManifest.xml not found!");
             }

             if (Directory.Exists(wsAppOutputPath))
             {
                 Directory.Delete(wsAppOutputPath, true);
             }
             Directory.CreateDirectory(wsAppOutputPath);

             _currentAppInfo = new WSAppInfo
             {
                 Path = wsAppPath,
                 OutputPath = wsAppOutputPath,
                 FileName = Path.GetFileName(wsAppPath)
             };

            await ReadAppManifestAsync(_currentAppInfo);

            await _appPackageService.CreateAppxPackageAsync(_currentAppInfo, UpdateStatus);
            await _appPackageService.CreateCertificatesAsync(_currentAppInfo, UpdateStatus);
            await _appPackageService.SignAppAsync(_currentAppInfo, UpdateStatus);
            
            UpdateStatus("Package Signing succeeded. Please install the '.cer' files before installing Application");
        }
        
        private void UpdateStatus(string message)
        {
            InfoText = message;
        }

        private async Task ReadAppManifestAsync(WSAppInfo wsAppInfo)
        {
            var xmlPath = Path.Combine(wsAppInfo.Path, "AppxManifest.xml");
            var settings = new XmlReaderSettings { Async = true };
            using var xmlReader = XmlReader.Create(xmlPath, settings);
            while (await xmlReader.ReadAsync())
            {
                if (xmlReader.IsStartElement() && xmlReader.Name == "Identity")
                {
                    wsAppInfo.Name = xmlReader["Name"];
                    wsAppInfo.Publisher = xmlReader["Publisher"];
                    wsAppInfo.Version = xmlReader["Version"];
                    wsAppInfo.ProcessorArchitecture = xmlReader["ProcessorArchitecture"];
                    break;
                }
            }
        }

        private async Task RenameOutputFilesAsync()
        {
             if (_currentAppInfo == null) return;

             try
             {
                // Offload to background thread if heavy I/O
                await Task.Run(() => 
                {
                    var outputDir = new DirectoryInfo(_currentAppInfo.OutputPath);
                    var files = outputDir.GetFiles();

                    // Rename .appx file
                    var appxFile = files.FirstOrDefault(f => f.Extension.Equals(".appx", StringComparison.OrdinalIgnoreCase));
                    if (appxFile != null)
                    {
                        string newAppxName = GetAppxFileName(_currentAppInfo.Name, _currentAppInfo.Version);
                        File.Move(appxFile.FullName, Path.Combine(outputDir.FullName, newAppxName));
                    }

                    // Rename certificate files
                    RenameFile(files, ".pvk", "Sign Key");
                    RenameFile(files, ".cer", "Certificate");
                    RenameFile(files, ".pfx", "Certificate");
                });
             }
             catch (Exception ex)
             {
                 InfoText = $"Error renaming files: {ex.Message}";
             }
        }

        private string GetAppxFileName(string appName, string version)
        {
            appName = CleanAppName(appName);
            var versionParts = version.Split('.');
            var majorVersion = versionParts[0];
            var minorVersion = versionParts.Length > 1 ? "." + versionParts[1] : "";

            return $"{appName} v{majorVersion}{minorVersion}.appx";
        }

        private void RenameFile(FileInfo[] files, string extension, string baseName)
        {
            var matchingFiles = files.Where(f => f.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase)).ToList();
            for (int i = 0; i < matchingFiles.Count; i++)
            {
                string newName = i == 0
                    ? $"{baseName}{extension}"
                    : $"{baseName} {i + 1}{extension}";
                File.Move(matchingFiles[i].FullName, Path.Combine(matchingFiles[i].DirectoryName, newName));
            }
        }


        private string CleanAppName(string appName)
        {
            appName = Regex.Replace(appName, @"[^\w\s]", " ");
            appName = Regex.Replace(appName, @"\s+", " ").Trim();
            return appName;
        }
        
        private void OpenHelp()
        {
            RequestOpenHelp?.Invoke(this, EventArgs.Empty);
        }

        private void OpenFolderPicker()
        {
            RequestFolderPicker?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler RequestOpenHelp;
        public event EventHandler RequestFolderPicker;
        
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
