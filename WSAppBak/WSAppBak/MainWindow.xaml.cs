using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using System.Diagnostics;
using System.Xml;

using System.Data;

using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

using System.Runtime.InteropServices;
using WinRT;
using Microsoft.UI.Windowing;


namespace WSAppBak
{

    public sealed partial class MainWindow : Window
    {
        private string AppName = "Windows Store App Backup";

        private string AppCreator = "MadCkull";

        //private string AppCurrentDirctory = Directory.GetCurrentDirectory();
        private string AppCurrentDirctory = "D:\\Files";

        private string WSAppXmlFile = "AppxManifest.xml";

        private bool Checking = true;

        private string WSAppName;

        private string WSAppPath;

        private string WSAppVersion;

        private string WSAppFileName;

        private string WSAppOutputPath;

        private string WSAppProcessorArchitecture;

        private string WSAppPublisher;

        public MainWindow()
        {
            this.InitializeComponent();

            WindowSize(1100, 600, false);

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
        }


        private void WindowSize(int Width, int Height, bool Resizeable)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = Width, Height = Height });

            OverlappedPresenter overlappedPresenter = appWindow.Presenter as OverlappedPresenter;
            overlappedPresenter.IsResizable = Resizeable;
            overlappedPresenter.IsMaximizable = Resizeable;
            overlappedPresenter.IsAlwaysOnTop = !Resizeable;
        }
        

        private async void InputPath_DragEnter(object sender, DragEventArgs args)
        {
            if (args.DataView.Contains(StandardDataFormats.StorageItems))
            {
                args.AcceptedOperation = DataPackageOperation.Copy;
            }
            else
            {
                args.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void InputPath_Drop(object sender, DragEventArgs args)
        {
            InfoText.Text = "";
            if (args.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await args.DataView.GetStorageItemsAsync();
                if (items.Count == 1 && items[0] is Windows.Storage.StorageFolder folder)
                {
                    InputPath.Text = folder.Path;
                }
            }
        }



        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            InfoText.Text = "";
            Run();
        }


        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new HelpWindow();
            window.Activate();
        }



        public void Run()
        {
            Reset();

            InfoText.Text = "Please Wait...";
            ReadArg();
        }



        private string RunProcess(string fileName, string args)
        {
            string result = "";
            Process process = new Process
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

            while (!process.StandardOutput.EndOfStream)
            {
                string text = process.StandardOutput.ReadLine();
                InfoText.Text = text;
                if (text.Length > 0)
                {
                    result = text;
                }
            }
            return result;
        }


        private void ReadArg()
        {
            InfoText.Text = "Reading Arguments.";

            WSAppPath = Convert.ToString(InputPath.Text);
            WSAppOutputPath = Convert.ToString(OutputPath.Text);

            if (WSAppPath.Contains("\""))
            {
                WSAppPath = WSAppPath.Replace("\"", "");
                WSAppPath = "\"" + WSAppPath + "\"";
            }
            else if (File.Exists(WSAppPath + "\\" + WSAppXmlFile))
            {
                if (WSAppOutputPath.Contains("\""))
                {
                    WSAppOutputPath = WSAppOutputPath.Replace("\"", "");
                    WSAppOutputPath = "\"" + WSAppOutputPath + "\"";
                }
                else if (Directory.Exists(WSAppOutputPath))
                {
                    WSAppFileName = Path.GetFileName(WSAppPath);
                    using (XmlReader xmlReader = XmlReader.Create(WSAppPath + "\\" + WSAppXmlFile))
                    {
                        while (xmlReader.Read())
                        {
                            if (xmlReader.IsStartElement() && xmlReader.Name == "Identity")
                            {
                                string text = xmlReader["Name"];
                                if (text != null)
                                {
                                    WSAppName = text;
                                }
                                string text2 = xmlReader["Publisher"];
                                if (text2 != null)
                                {
                                    WSAppPublisher = text2;
                                }
                                string text3 = xmlReader["Version"];
                                if (text3 != null)
                                {
                                    WSAppVersion = text3;
                                }
                                string text4 = xmlReader["ProcessorArchitecture"];
                                if (text4 != null)
                                {
                                    WSAppProcessorArchitecture = text4;
                                }
                            }
                        }
                    }

                    MakeAppx();
                }
                else
                {
                    InfoText.Text = "Invailed Output Path, " + WSAppOutputPath + " Directory not found!";
                }
            }
            else
            {
                InfoText.Text = "Invailed App Path, "+ WSAppXmlFile + " file not found!";
            }
        }

        private void MakeAppx()
        {
            InfoText.Text = "Creating Appx File.";

            string text = AppCurrentDirctory + "\\WSAppBak\\MakeAppx.exe";
            string args = "pack -d \"" + WSAppPath + "\" -p \"" + WSAppOutputPath + "\\" + WSAppFileName + ".appx\" -l";
            if (File.Exists(text))
            {
                if (File.Exists(WSAppOutputPath + "\\" + WSAppFileName + ".appx"))
                {
                    File.Delete(WSAppOutputPath + "\\" + WSAppFileName + ".appx");
                }
                InfoText.Text = "Creating Package File.";

                if (RunProcess(text, args).ToLower().Contains("succeeded"))
                {
                    InfoText.Text = "'" + WSAppFileName + ".appx' Created.";
                    while (Checking)
                    {
                        MakeCert();
                    }
                }
                else
                {
                    Checking = false;
                    InfoText.Text = "Failed to create '"+ WSAppFileName + ".appx'.";
                }
            }
            else
            {
                Checking = false;
                InfoText.Text = "Can't create '.appx' file, 'MakeAppx.exe' file not found!";
            }
        }

        private void MakeCert()
        {
            InfoText.Text = "Process Started.";

            string text = AppCurrentDirctory + "\\WSAppBak\\MakeCert.exe";
            string args = "-n \"" + WSAppPublisher + "\" -r -a sha256 -len 2048 -cy end -h 0 -eku 1.3.6.1.5.5.7.3.3 -b 01/01/2000 -sv \"" + WSAppOutputPath + "\\" + WSAppFileName + ".pvk\" \"" + WSAppOutputPath + "\\" + WSAppFileName + ".cer\"";
            if (File.Exists(text))
            {
                if (File.Exists(WSAppOutputPath + "\\" + WSAppFileName + ".pvk"))
                {
                    File.Delete(WSAppOutputPath + "\\" + WSAppFileName + ".pvk");
                }
                if (File.Exists(WSAppOutputPath + "\\" + WSAppFileName + ".cer"))
                {
                    File.Delete(WSAppOutputPath + "\\" + WSAppFileName + ".cer");
                }

                InfoText.Text = "Creating Certificates...";

                if (RunProcess(text, args).ToLower().Contains("succeeded"))
                {
                    while (Checking)
                    {
                        Pvk2Pfx();
                    }
                }
                else
                {
                    Checking = false;
                    InfoText.Text = "Failed to create Certificates!";
                }
            }
            else
            {
                Checking = false;
                InfoText.Text = "Can't create Certificates, 'MakeCert.exe' file not found!";
            }
        }

        private void Pvk2Pfx()
        {
            string text = AppCurrentDirctory + "\\WSAppBak\\Pvk2Pfx.exe";
            string args = "-pvk \"" + WSAppOutputPath + "\\" + WSAppFileName + ".pvk\" -spc \"" + WSAppOutputPath + "\\" + WSAppFileName + ".cer\" -pfx \"" + WSAppOutputPath + "\\" + WSAppFileName + ".pfx\"";
            if (File.Exists(text))
            {
                if (File.Exists(WSAppOutputPath + "\\" + WSAppFileName + ".pfx"))
                {
                    File.Delete(WSAppOutputPath + "\\" + WSAppFileName + ".pfx");
                }

                if (RunProcess(text, args).Length == 0)
                {
                    Console.Write("succeeded");
                    while (Checking)
                    {
                        SignApp();
                    }
                }
                else
                {
                    Checking = false;
                    InfoText.Text = "Failed to convert certificates!";
                }
            }
            else
            {
                Checking = false;
                InfoText.Text = "Can't convert Certificates, 'Pvk2Pfx.exe' file not found!";
            }
        }

        private void SignApp()
        {
            InfoText.Text = "Signing Application.";

            string text = AppCurrentDirctory + "\\WSAppBak\\SignTool.exe";
            string args = "sign -fd SHA256 -a -f \"" + WSAppOutputPath + "\\" + WSAppFileName + ".pfx\" \"" + WSAppOutputPath + "\\" + WSAppFileName + ".appx\"";
            if (File.Exists(text))
            {
                if (RunProcess(text, args).ToLower().Contains("successfully signed"))
                {
                    Checking = false;
                    InfoText.Text = "Package Signing succeeded. Please install the '.cer' files before installing Application";
                }
                else
                {
                    Checking = false;
                    InfoText.Text = "Failed Sign the Package!";
                }
            }

            else
            {
                Checking = false;
                InfoText.Text = "Can't Sign the Package, 'SignTool.exe' file not found!";
            }
        }


        private void Reset()
        {
            // Clear or reset your variables and resources here
            WSAppPath = string.Empty;
            WSAppOutputPath = string.Empty;
            WSAppFileName = string.Empty;
            WSAppName = string.Empty;
            WSAppVersion = string.Empty;
            WSAppProcessorArchitecture = string.Empty;
            WSAppPublisher = string.Empty;

            // Clear any UI elements if needed
            //InputPath.Text = string.Empty;
            //OutputPath.Text = string.Empty;
            InfoText.Text = string.Empty;
        }
    }
}
