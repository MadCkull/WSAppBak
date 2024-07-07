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
using System.Text.RegularExpressions;


namespace WSAppBak
{
    public sealed partial class MainWindow : Window
    {
        private const string AppName = "Windows Store App Backup";
        private const string AppCreator = "MadCkull";
        private const string WSAppXmlFile = "AppxManifest.xml";

        private readonly string _appCurrentDir = AppContext.BaseDirectory;

        private InstalledApp _selectedApp;
        private string SelectedAppPath;
        private List<InstalledApp> AllInstalledApps;


        private WSAppInfo _wsAppInfo;

        public MainWindow()
        {
            InitializeComponent();
            InfoText.Text = "Please Wait while we load Apps...";
            ControlsStatus(false);
            InitializeWindowAsync();
            LogCurrentDirectory();
        }

        private async void InitializeWindowAsync()
        {
            SetWindowSize(1800, 950, false);
            ExtendsContentIntoTitleBar = true;
            //SetTitleBar(AppTitleBar);
            await LoadInstalledAppsAsync();
            InfoText.Text = String.Empty;

            ControlsStatus(true);
            StartButton.IsEnabled = false;

            OutputPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "WSAppBak");
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
                //overlappedPresenter.IsAlwaysOnTop = !resizable;
            }
        }


        private void ControlsStatus(bool Status)
        {
            AppSearchBox.IsEnabled = Status;
            OutputPath.IsEnabled = Status;
            StartButton.IsEnabled = Status;
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

        private static readonly HashSet<string> SystemAppPrefixes = new HashSet<string>
        {
            "1527c705-839a-4832-9118-54d4Bd6a0c89",
            "c5e2524a-ea46-4f67-841f-6a9465d9d515",
            "EightSecure.Firewall",
            "E2A4F912-2574-4A75-9BB0-0D023378592B",
            "F46D4000-FD22-4DB4-AC8E-4E1DDDE828FE",
            "Microsoft.AAD.BrokerPlugin",
            "Microsoft.AccountsControl",
            "Microsoft.AsyncTextService",
            "Microsoft.BioEnrollment",
            "Microsoft.CredDialogHost",
            "Microsoft.ECApp",
            "Microsoft.LockApp",
            "Microsoft.MicrosoftEdge",
            "Microsoft.MicrosoftEdgeDevToolsClient",
            "Microsoft.Win32WebViewHost",
            "Microsoft.Windows.",
            "windows.immersivecontrolpanel",
            "Windows.ContactSupport",
            "Windows.PrintDialog",
            "Microsoft.Net",
            "Microsoft.VCLibs",
            "Microsoft.UI.Xaml",
            "Microsoft.DirectX",
            "Microsoft.WebpImageExtension",
            "Microsoft.VP9VideoExtensions",
            "Microsoft.ScreenSketch",
            "Microsoft.Services.Store.Engagement",
            "Microsoft.XboxGameCallableUI",
            "Microsoft.XboxIdentityProvider",
            "Microsoft.YourPhone",
        };

        private async Task<List<InstalledApp>> GetInstalledAppsAsync()
        {
            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser(string.Empty);

            var installedApps = new List<InstalledApp>();

            foreach (var package in packages)
            {
                try
                {
                    if (IsSystemOrDependencyApp(package))
                    {
                        continue;
                    }

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

        private bool IsSystemOrDependencyApp(Package package)
        {
            // Filter out system apps
            if (package.SignatureKind == PackageSignatureKind.System)
            {
                return true;
            }

            // Filter out apps with empty or generic names
            if (string.IsNullOrWhiteSpace(package.DisplayName) || package.DisplayName == "App" || package.DisplayName == "Application")
            {
                return true;
            }

            // Filter out known system app prefixes
            foreach (var prefix in SystemAppPrefixes)
            {
                if (package.Id.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Filter out framework packages
            if (package.IsFramework)
            {
                return true;
            }

            // Filter out resource packages
            if (package.IsResourcePackage)
            {
                return true;
            }

            // Additional filters can be added here as needed

            return false;
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
            if (OutputPath.Text == string.Empty || AppSearchBox.Text == string.Empty)
            {
                StartButton.IsEnabled = false;
            }
            else
            {
                StartButton.IsEnabled = true;
            }
        }

        private void OutputPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (OutputPath.Text == string.Empty || AppSearchBox.Text == string.Empty)
            {
                StartButton.IsEnabled = false;
            }
            else
            {
                StartButton.IsEnabled = true;
            }
        }

        private void AppSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is InstalledApp selectedApp)
            {
                _selectedApp = selectedApp;
                SelectedAppPath = selectedApp.ManifestPath.Substring(0, selectedApp.ManifestPath.LastIndexOf('\\'));
                sender.Text = selectedApp.Name;
            }
            if (OutputPath.Text == string.Empty || AppSearchBox.Text == string.Empty)
            {
                StartButton.IsEnabled = false;
            }
            else
            {
                StartButton.IsEnabled = true;
            }
        }

        private void AppSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_selectedApp != null)
            {
                AppSearchBox.Text = _selectedApp.Name;
            }
            else
            {
                AppSearchBox.Text = string.Empty;
            }
        }




        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            InfoText.Text = string.Empty;
            ControlsStatus(false);
            try
            {
                await RunAsync();
                await RenameOutputFilesAsync();
            }
            catch (Exception ex)
            {
                InfoText.Text = $"An error occurred: {ex.Message}";
            }
            finally
            {
                ControlsStatus(true);
            }
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

            var appName = _selectedApp.Name;
            // Clean the app name
            appName = Regex.Replace(appName, @"[^\w\s]", " "); // Remove special characters
            appName = Regex.Replace(appName, @"\s+", " ").Trim(); // Remove extra spaces

            var wsAppOutputPath = $"{OutputPath.Text.Trim('"')}\\{appName}";
            InfoText.Text = wsAppOutputPath;


            if (!File.Exists(Path.Combine(wsAppPath, WSAppXmlFile)))
            {
                throw new FileNotFoundException($"Invalid App Path. {WSAppXmlFile} file not found!");
            }

            if (Directory.Exists(wsAppOutputPath))
            {
                Directory.Delete(wsAppOutputPath, true);
            }

            Directory.CreateDirectory(wsAppOutputPath);
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










        //Renaming Methods:


        private async Task RenameOutputFilesAsync()
        {
            try
            {
                var outputDir = new DirectoryInfo(_wsAppInfo.OutputPath);
                var files = outputDir.GetFiles();

                // Rename .appx file
                var appxFile = files.FirstOrDefault(f => f.Extension.Equals(".appx", StringComparison.OrdinalIgnoreCase));
                if (appxFile != null)
                {
                    string newAppxName = GetAppxFileName(_wsAppInfo.Name, _wsAppInfo.Version);
                    File.Move(appxFile.FullName, Path.Combine(outputDir.FullName, newAppxName));
                }

                // Rename certificate files
                RenameFile(files, ".pvk", "Sign Key");
                RenameFile(files, ".cer", "Certificate");
                RenameFile(files, ".pfx", "Certificate");
            }
            catch (Exception ex)
            {
                InfoText.Text = $"Error renaming files: {ex.Message}";
            }
        }

        private string GetAppxFileName(string appName, string version)
        {
            // Clean the app name
            appName = Regex.Replace(appName, @"[^\w\s]", " "); // Remove special characters
            appName = Regex.Replace(appName, @"\s+", " ").Trim(); // Remove extra spaces

            // Process the version
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