using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading;

namespace WSAppBak
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            try
            {
                Application.Start((p) =>
                {
                    var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                });
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        private static void LogException(Exception ex)
        {
            try
            {
                // Try to write to LocalState if package info is available
                string logPath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "early_crash.log");
                string message = $"[{DateTime.Now}] CRITICAL STARTUP CRASH: {ex.Message}\nStack Trace: {ex.StackTrace}\n\n";
                File.AppendAllText(logPath, message);
            }
            catch
            {
                // Fallback: try writing to a known temp location if Package identity fails
                try 
                {
                     string tempPath = Path.Combine(Path.GetTempPath(), "WSAppBak_Crash.log");
                     File.AppendAllText(tempPath, $"[{DateTime.Now}] Fallback Crash Log: {ex.ToString()}");
                }
                catch { }
            }
        }
    }
}
