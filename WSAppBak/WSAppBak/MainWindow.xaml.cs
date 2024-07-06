using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using System;
using System.Xml;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT;
using Microsoft.UI;
using WinRT.Interop;
using System.Collections.Generic;
using Windows.Management.Deployment;

using System.Linq;
using Windows.ApplicationModel;

using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;


namespace WSAppBak
{
    public sealed partial class MainWindow : Window
    {
        private const string AppName = "Windows Store App Backup";
        private const string AppCreator = "MadCkull";
        private const string WSAppXmlFile = "AppxManifest.xml";

        private readonly string _appCurrentDir = AppContext.BaseDirectory;

        private string SelectedAppPath;
        private List<InstalledApp> AllInstalledApps;


        private WSAppInfo _wsAppInfo;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWindow();
            LogCurrentDirectory();
        }

        private void InitializeWindow()
        {
            SetWindowSize(1800, 950, false);
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            LoadInstalledAppsAsync();

            OutputPath.Text = "D:\\Other\\WSAPPOutput";
        }

        private void SetWindowSize(int width, int height, bool resizable)
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });

            if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
            {
                overlappedPresenter.IsResizable = resizable;
                overlappedPresenter.IsMaximizable = resizable;
                overlappedPresenter.IsAlwaysOnTop = !resizable;
            }
        }

        private void LogCurrentDirectory()
        {
            //[Write Log Code Here]

            //File.AppendAllText(_logFilePath, $"DirectoryPath: {currentDir}\nFull Name: {rootPath}\nParent: {parentPath}\n");
        }


        //Fetching App Installed List


        private async Task LoadInstalledAppsAsync()
        {
            AllInstalledApps = await GetInstalledAppsAsync();
            AppSearchBox.ItemsSource = AllInstalledApps;
        }

        private async Task<List<InstalledApp>> GetInstalledAppsAsync()
        {
            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser(string.Empty);

            var installedApps = new List<InstalledApp>();

            foreach (var package in packages)
            {
                try
                {
                    var manifestPath = Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
                    if (File.Exists(manifestPath))
                    {
                        var logoPath = await GetAppLogoPathAsync(package);
                        installedApps.Add(new InstalledApp
                        {
                            Name = package.DisplayName,
                            ManifestPath = manifestPath,
                            IconSource = await LoadAppIconAsync(logoPath)
                        });
                    }
                }
                catch (Exception)
                {
                    // Skip packages that we can't access
                }
            }

            return installedApps.OrderBy(app => app.Name).ToList();
        }

        private async Task<string> GetAppLogoPathAsync(Package package)
        {
            try
            {
                var installedLocation = package.InstalledLocation;
                var manifestFile = await installedLocation.GetFileAsync("AppxManifest.xml");
                var manifestContent = await FileIO.ReadTextAsync(manifestFile);

                XmlDocument manifestXml = new XmlDocument();
                manifestXml.LoadXml(manifestContent);

                XmlNamespaceManager nsmgr = new XmlNamespaceManager(new NameTable());
                nsmgr.AddNamespace("x", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

                XmlNode logoNode = manifestXml.SelectSingleNode("//x:Properties/x:Logo", nsmgr);
                if (logoNode != null)
                {
                    var logoPath = logoNode.InnerText;
                    var logoUri = new Uri(installedLocation.Path + "\\" + logoPath);
                    return logoUri.AbsoluteUri;
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions as needed
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            return null;
        }

        private async Task<BitmapImage> LoadAppIconAsync(string logoPath)
        {
            if (string.IsNullOrEmpty(logoPath)) return null;

            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(logoPath);
                using (IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(stream);
                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }

        private void AppSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var suggestions = AllInstalledApps
                    .Where(app => app.Name.Contains(sender.Text, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                sender.ItemsSource = suggestions;
            }
        }

        private void AppSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is InstalledApp selectedApp)
            {
                SelectedAppPath = selectedApp.ManifestPath.Substring(0, selectedApp.ManifestPath.LastIndexOf('\\'));
            }
        }







        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            InfoText.Text = string.Empty;
            await RunAsync();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new HelpWindow();
            window.Activate();
        }

        private async Task RunAsync()
        {
            try
            {
                InfoText.Text = "Please Wait...";
                await ReadArgAsync();
                await CreateAppxPackageAsync();
                await CreateCertificatesAsync();
                await SignAppAsync();
                InfoText.Text = "Package Signing succeeded. Please install the '.cer' files before installing Application";
            }
            catch (Exception ex)
            {
                InfoText.Text = $"An error occurred: {ex.Message}";
            }
        }

        private async Task ReadArgAsync()
        {
            InfoText.Text = "Reading Arguments.";

            var wsAppPath = SelectedAppPath;
            var wsAppOutputPath = OutputPath.Text.Trim('"');

            if (!File.Exists(Path.Combine(wsAppPath, WSAppXmlFile)))
            {
                throw new FileNotFoundException($"Invalid App Path. {WSAppXmlFile} file not found!");
            }

            if (!Directory.Exists(wsAppOutputPath))
            {
                throw new DirectoryNotFoundException($"Invalid Output Path. {wsAppOutputPath} directory not found!");
            }

            _wsAppInfo = new WSAppInfo
            {
                Path = wsAppPath,
                OutputPath = wsAppOutputPath,
                FileName = Path.GetFileName(wsAppPath)
            };

            await ReadAppManifestAsync();
        }

        private async Task ReadAppManifestAsync()
        {
            var xmlPath = Path.Combine(_wsAppInfo.Path, WSAppXmlFile);
            var settings = new XmlReaderSettings { Async = true };
            using var xmlReader = XmlReader.Create(xmlPath, settings);
            while (await xmlReader.ReadAsync())
            {
                if (xmlReader.IsStartElement() && xmlReader.Name == "Identity")
                {
                    _wsAppInfo.Name = xmlReader["Name"];
                    _wsAppInfo.Publisher = xmlReader["Publisher"];
                    _wsAppInfo.Version = xmlReader["Version"];
                    _wsAppInfo.ProcessorArchitecture = xmlReader["ProcessorArchitecture"];
                    break;
                }
            }
        }

        private async Task CreateAppxPackageAsync()
        {
            InfoText.Text = "Creating Appx File.";
            var makeAppxPath = Path.Combine(_appCurrentDir, "WSTools", "MakeAppx.exe");
            var outputAppxPath = Path.Combine(_wsAppInfo.OutputPath, $"{_wsAppInfo.FileName}.appx");

            if (!File.Exists(makeAppxPath))
            {
                throw new FileNotFoundException("Can't create '.appx' file. 'MakeAppx.exe' file not found!");
            }

            File.Delete(outputAppxPath);

            var args = $"pack -d \"{_wsAppInfo.Path}\" -p \"{outputAppxPath}\" -l";
            var result = await RunProcessAsync(makeAppxPath, args);

            if (!result.ToLower().Contains("succeeded"))
            {
                throw new Exception($"Failed to create '{_wsAppInfo.FileName}.appx'.");
            }

            InfoText.Text = $"'{_wsAppInfo.FileName}.appx' Created.";
        }

        private async Task CreateCertificatesAsync()
        {
            InfoText.Text = "Creating Certificates...";
            await CreateCertificateAsync();
            await ConvertCertificateAsync();
        }

        private async Task CreateCertificateAsync()
        {
            var makeCertPath = Path.Combine(_appCurrentDir, "WSTools", "MakeCert.exe");
            if (!File.Exists(makeCertPath))
            {
                throw new FileNotFoundException("Can't create Certificates. 'MakeCert.exe' file not found!");
            }

            var pvkPath = Path.Combine(_wsAppInfo.OutputPath, $"{_wsAppInfo.FileName}.pvk");
            var cerPath = Path.Combine(_wsAppInfo.OutputPath, $"{_wsAppInfo.FileName}.cer");

            File.Delete(pvkPath);
            File.Delete(cerPath);

            var args = $"-n \"{_wsAppInfo.Publisher}\" -r -a sha256 -len 2048 -cy end -h 0 -eku 1.3.6.1.5.5.7.3.3 -b 01/01/2000 -sv \"{pvkPath}\" \"{cerPath}\"";
            var result = await RunProcessAsync(makeCertPath, args);

            if (!result.ToLower().Contains("succeeded"))
            {
                throw new Exception("Failed to create Certificates!");
            }
        }

        private async Task ConvertCertificateAsync()
        {
            var pvk2PfxPath = Path.Combine(_appCurrentDir, "WSTools", "Pvk2Pfx.exe");
            if (!File.Exists(pvk2PfxPath))
            {
                throw new FileNotFoundException("Can't convert Certificates. 'Pvk2Pfx.exe' file not found!");
            }

            var pvkPath = Path.Combine(_wsAppInfo.OutputPath, $"{_wsAppInfo.FileName}.pvk");
            var cerPath = Path.Combine(_wsAppInfo.OutputPath, $"{_wsAppInfo.FileName}.cer");
            var pfxPath = Path.Combine(_wsAppInfo.OutputPath, $"{_wsAppInfo.FileName}.pfx");

            File.Delete(pfxPath);

            var args = $"-pvk \"{pvkPath}\" -spc \"{cerPath}\" -pfx \"{pfxPath}\"";
            var result = await RunProcessAsync(pvk2PfxPath, args);

            if (result.Length != 0)
            {
                throw new Exception("Failed to convert certificates!");
            }
        }

        private async Task SignAppAsync()
        {
            InfoText.Text = "Signing Application.";
            var signToolPath = Path.Combine(_appCurrentDir, "WSTools", "SignTool.exe");
            if (!File.Exists(signToolPath))
            {
                throw new FileNotFoundException("Can't Sign the Package. 'SignTool.exe' file not found!");
            }

            var pfxPath = Path.Combine(_wsAppInfo.OutputPath, $"{_wsAppInfo.FileName}.pfx");
            var appxPath = Path.Combine(_wsAppInfo.OutputPath, $"{_wsAppInfo.FileName}.appx");

            var args = $"sign -fd SHA256 -a -f \"{pfxPath}\" \"{appxPath}\"";
            var result = await RunProcessAsync(signToolPath, args);

            if (!result.ToLower().Contains("successfully signed"))
            {
                throw new Exception("Failed to sign the Package!");
            }
        }

        private async Task<string> RunProcessAsync(string fileName, string args)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            InfoText.Text = "Process Started.";

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output;
        }
    }


    public class InstalledApp
    {
        public string Name { get; set; }
        public string ManifestPath { get; set; }
        public BitmapImage IconSource { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class WSAppInfo
    {
        public string Path { get; set; }
        public string OutputPath { get; set; }
        public string FileName { get; set; }
        public string Name { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public string ProcessorArchitecture { get; set; }
    }
}