using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WSAppBak
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            this.InitializeComponent();

            WindowSize(1300, 600, false);
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
    }
}
