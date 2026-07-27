using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;
using WSAppBak.Models;

namespace WSAppBak.Services
{
    public class AppRetrievalService
    {
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

        public async Task<List<InstalledApp>> GetInstalledAppsAsync()
        {
            var packageManager = new PackageManager();
            // Ensure we are running with enough privileges or handle access denied, though for current user it should be fine.
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

                    var installedLocation = package.InstalledLocation;
                    if (installedLocation == null) continue;

                    var manifestPath = Path.Combine(installedLocation.Path, "AppxManifest.xml");
                    if (File.Exists(manifestPath))
                    {
                        installedApps.Add(new InstalledApp
                        {
                            Name = package.DisplayName,
                            ManifestPath = manifestPath,
                            IconSource = await GetAppIconAsync(package)
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
            try
            {
                // Filter out system apps, empty or generic names, known system app prefixes, framework packages, and resource packages
                return package.SignatureKind == PackageSignatureKind.System
                    || string.IsNullOrWhiteSpace(package.DisplayName)
                    || package.DisplayName == "App"
                    || package.DisplayName == "Application"
                    || SystemAppPrefixes.Any(prefix => package.Id.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    || package.IsFramework
                    || package.IsResourcePackage;
            }
            catch
            {
                return true; // If we can't check, assume it's safe to skip
            }
        }

        private async Task<BitmapImage> GetAppIconAsync(Package package)
        {
            try
            {
                var appListEntries = await package.GetAppListEntriesAsync();
                var entry = appListEntries.FirstOrDefault();
                if (entry != null)
                {
                    var logoStreamRef = entry.DisplayInfo.GetLogo(new Windows.Foundation.Size(50, 50));
                    if (logoStreamRef != null)
                    {
                        using (var stream = await logoStreamRef.OpenReadAsync())
                        {
                            var bitmapImage = new BitmapImage();
                            await bitmapImage.SetSourceAsync(stream);
                            return bitmapImage;
                        }
                    }
                }
                
                // Fallback to package logo if AppListEntries is empty
                var packageLogoStreamRef = package.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(50, 50));
                if (packageLogoStreamRef != null)
                {
                    using (var stream = await packageLogoStreamRef.OpenReadAsync())
                    {
                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(stream);
                        return bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting app logo: {ex.Message}");
            }

            return null;
        }
    }
}
