[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [switch]$Launch,
    [ValidateRange(1, 120)][int]$StartupTimeoutSeconds = 30,
    [ValidateRange(1, 60)][int]$ObserveSeconds = 5
)

$ErrorActionPreference = 'Stop'
$portablePath = (Resolve-Path -LiteralPath $Path).Path
$requiredFiles = @(
    'WSAppBak.exe', 'WSAppBak.dll', 'WSAppBak.pri',
    'WSAppBak.deps.json', 'WSAppBak.runtimeconfig.json',
    'Microsoft.UI.Xaml.dll', 'Microsoft.UI.pri', 'Microsoft.UI.Xaml.Controls.pri',
    'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
    'WSTools/MakeAppx.exe', 'WSTools/MakeCert.exe',
    'WSTools/Pvk2Pfx.exe', 'WSTools/SignTool.exe'
)
foreach ($relativePath in $requiredFiles) {
    $file = Join-Path $portablePath $relativePath
    if (!(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -eq 0) {
        throw "Portable package is missing a required nonempty file: $relativePath"
    }
}
$runtime = Get-Content -LiteralPath (Join-Path $portablePath 'WSAppBak.runtimeconfig.json') -Raw | ConvertFrom-Json
if (!$runtime.runtimeOptions.includedFrameworks -or $runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks) {
    throw 'Portable package must include the .NET runtime, not depend on a machine-wide installation.'
}
Write-Host 'Portable layout and self-contained runtime checks passed.'

if ($Launch) {
    # Test the extracted artifact from a different working directory, like a shortcut.
    $app = Start-Process -FilePath (Join-Path $portablePath 'WSAppBak.exe') -WorkingDirectory $env:TEMP -WindowStyle Hidden -PassThru
    try {
        $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
        do {
            Start-Sleep -Milliseconds 250
            $app.Refresh()
            if ($app.HasExited) { throw "Portable app exited during startup: $($app.ExitCode)" }
            $ready = $app.MainWindowHandle -ne 0 -and $app.MainWindowTitle -eq 'WSAppBak' -and $app.Responding
        } until ($ready -or (Get-Date) -ge $deadline)
        if (!$ready) { throw 'Portable app did not expose a responsive WSAppBak window before the timeout.' }
        if ($app.WaitForExit($ObserveSeconds * 1000)) { throw "Portable app exited after showing its window: $($app.ExitCode)" }
        $app.Refresh()
        if (!$app.Responding) { throw 'Portable app stopped responding after startup.' }
        Write-Host "Portable launch passed: responsive window remained open for $ObserveSeconds seconds."
    }
    finally {
        if (!$app.HasExited) {
            $null = $app.CloseMainWindow()
            if (!$app.WaitForExit(5000)) { $app.Kill(); $app.WaitForExit() }
        }
        $app.Dispose()
    }
}
