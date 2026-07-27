using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using System;
using Microsoft.UI;
using WSAppBak.ViewModels;
using WSAppBak.Models;
using System.Linq;
using Windows.Storage.Pickers;

namespace WSAppBak
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            
            // Subscribe to help request
            ViewModel.RequestOpenHelp += (s, e) =>
            {
                var window = new HelpWindow();
                window.Activate();
            };

            // Subscribe to folder picker request
            ViewModel.RequestFolderPicker += async (s, e) =>
            {
                var folderPicker = new FolderPicker();
                folderPicker.FileTypeFilter.Add("*");
                
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    ViewModel.OutputPath = folder.Path;
                }
            };

            InitializeWindow();
            
            // Log current directory (keep as simple logging if needed, or remove if irrelevant, strictly following refactoring)
            // The original had logging, I'll assume it's nice to keep but maybe less obtrusive.
        }

        private void InitializeWindow()
        {
            SetWindowSize(1800, 950, false);
            ExtendsContentIntoTitleBar = true;
            
            // Set title bar if needed, but in WinUI 3 with ExtendsContentIntoTitleBar, 
            // we usually set SetTitleBar(AppTitleBar).
            // Original code didn't seem to call SetTitleBar explicity in the snippets I saw, 
            // but defined a Border x:Name="AppTitleBar". 
            // Let's ensure it works.
            SetTitleBar(AppTitleBar);
        }

        private void SetWindowSize(int width, int height, bool resizable)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });

            if (appWindow.Presenter is OverlappedPresenter overlappedPresenter)
            {
                overlappedPresenter.IsResizable = resizable;
                overlappedPresenter.IsMaximizable = resizable;
            }
        }

        private void AppSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var query = sender.Text;
                if (ViewModel.AllInstalledApps != null)
                {
                    sender.ItemsSource = ViewModel.AllInstalledApps
                        .Where(app => app.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
            }
        }

        private void AppSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is InstalledApp selectedApp)
            {
                ViewModel.SelectedApp = selectedApp;
                sender.Text = selectedApp.Name;
            }
        }

        public Visibility BoolToVis(bool b) => b ? Visibility.Visible : Visibility.Collapsed;
        public Visibility InverseBoolToVis(bool b) => b ? Visibility.Collapsed : Visibility.Visible;

    }
}