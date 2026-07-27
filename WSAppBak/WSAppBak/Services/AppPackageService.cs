using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WSAppBak.Models;

namespace WSAppBak.Services
{
    public class AppPackageService
    {
        private readonly string _appCurrentDir;

        public AppPackageService()
        {
            _appCurrentDir = AppContext.BaseDirectory;
        }

        public async Task CreateAppxPackageAsync(WSAppInfo appInfo, Action<string> statusCallback)
        {
            statusCallback?.Invoke("Creating Appx File.");
            var makeAppxPath = Path.Combine(_appCurrentDir, "WSTools", "MakeAppx.exe");
            var outputAppxPath = Path.Combine(appInfo.OutputPath, $"{appInfo.FileName}.appx");

            if (!File.Exists(makeAppxPath))
            {
                throw new FileNotFoundException("Can't create '.appx' file. 'MakeAppx.exe' file not found!");
            }

            if (File.Exists(outputAppxPath))
            {
                File.Delete(outputAppxPath);
            }

            var args = $"pack -d \"{appInfo.Path}\" -p \"{outputAppxPath}\" -l";
            var result = await RunProcessAsync(makeAppxPath, args);

            if (!result.ToLower().Contains("succeeded"))
            {
                throw new Exception($"Failed to create '{appInfo.FileName}.appx'. Output: {result}");
            }

            statusCallback?.Invoke($"'{appInfo.FileName}.appx' Created.");
        }

        public async Task CreateCertificatesAsync(WSAppInfo appInfo, Action<string> statusCallback)
        {
            statusCallback?.Invoke("Creating Certificates...");
            
            // Create Certificate
            var makeCertPath = Path.Combine(_appCurrentDir, "WSTools", "MakeCert.exe");
            if (!File.Exists(makeCertPath))
            {
                throw new FileNotFoundException("Can't create Certificates. 'MakeCert.exe' file not found!");
            }

            var pvkPath = Path.Combine(appInfo.OutputPath, $"{appInfo.FileName}.pvk");
            var cerPath = Path.Combine(appInfo.OutputPath, $"{appInfo.FileName}.cer");

            if (File.Exists(pvkPath)) File.Delete(pvkPath);
            if (File.Exists(cerPath)) File.Delete(cerPath);

            var args = $"-n \"{appInfo.Publisher}\" -r -a sha256 -len 2048 -cy end -h 0 -eku 1.3.6.1.5.5.7.3.3 -b 01/01/2000 -e 12/31/2099 -sv \"{pvkPath}\" \"{cerPath}\"";
            var result = await RunProcessAsync(makeCertPath, args);

            if (!result.ToLower().Contains("succeeded"))
            {
                throw new Exception($"Failed to create Certificates! Output: {result}");
            }

            // Convert Certificate
            var pvk2PfxPath = Path.Combine(_appCurrentDir, "WSTools", "Pvk2Pfx.exe");
            if (!File.Exists(pvk2PfxPath))
            {
                throw new FileNotFoundException("Can't convert Certificates. 'Pvk2Pfx.exe' file not found!");
            }

            var pfxPath = Path.Combine(appInfo.OutputPath, $"{appInfo.FileName}.pfx");
            if (File.Exists(pfxPath)) File.Delete(pfxPath);

            args = $"-pvk \"{pvkPath}\" -spc \"{cerPath}\" -pfx \"{pfxPath}\"";
            result = await RunProcessAsync(pvk2PfxPath, args);

            // Pvk2Pfx may output warnings even on success, so validate by checking
            // whether the .pfx file was actually created on disk.
            if (!File.Exists(pfxPath))
            {
                 throw new Exception($"Failed to convert certificates! The .pfx file was not created. Output: {result}");
            }
        }

        public async Task SignAppAsync(WSAppInfo appInfo, Action<string> statusCallback)
        {
            statusCallback?.Invoke("Signing Application.");
            var signToolPath = Path.Combine(_appCurrentDir, "WSTools", "SignTool.exe");
            if (!File.Exists(signToolPath))
            {
                throw new FileNotFoundException("Can't Sign the Package. 'SignTool.exe' file not found!");
            }

            var pfxPath = Path.Combine(appInfo.OutputPath, $"{appInfo.FileName}.pfx");
            var appxPath = Path.Combine(appInfo.OutputPath, $"{appInfo.FileName}.appx");

            var args = $"sign -fd SHA256 -a -f \"{pfxPath}\" \"{appxPath}\"";
            var result = await RunProcessAsync(signToolPath, args);

            if (!result.ToLower().Contains("successfully signed"))
            {
                throw new Exception($"Failed to sign the Package! Output: {result}");
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
                    RedirectStandardError = true, // Capture error too
                    CreateNoWindow = true
                }
            };

            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;
            
            // If output is empty but error has something, maybe return error? 
            // Or just combine them?
            // The tools usually write to StdOut, but let's append error if present
             if (!string.IsNullOrWhiteSpace(error))
            {
                output += Environment.NewLine + error;
            }

            return output;
        }
    }
}
