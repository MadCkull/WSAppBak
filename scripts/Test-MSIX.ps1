[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Path = (Resolve-Path -LiteralPath $Path).Path
$bundle = [IO.Compression.ZipFile]::OpenRead($Path)
function Read-ZipText($Zip, [string]$Name) {
    $entry = $Zip.GetEntry($Name)
    if (!$entry) { throw "Missing package entry: $Name" }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}
try {
    [xml]$manifest = Read-ZipText $bundle 'AppxMetadata/AppxBundleManifest.xml'
    $apps = @($manifest.Bundle.Packages.Package | Where-Object Type -eq 'application')
    if (($apps.Architecture | Sort-Object) -join ',' -ne 'arm64,x64') {
        throw 'The bundle must contain exactly one x64 and one ARM64 application package.'
    }
    foreach ($app in $apps) {
        # MSIX uses OPC URI-escaped ZIP names (spaces and parentheses included).
        $entry = $bundle.Entries | Where-Object { [Uri]::UnescapeDataString($_.FullName) -eq $app.FileName } | Select-Object -First 1
        if (!$entry -or !$entry.Length) { throw "Missing application package: $($app.FileName)" }
        $stream = [IO.MemoryStream]::new()
        $inputStream = $entry.Open()
        try { $inputStream.CopyTo($stream) } finally { $inputStream.Dispose() }
        $stream.Position = 0
        $package = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
        try {
            if (@($package.Entries | Where-Object FullName -match '\.(pfx|p12|pvk)$').Count) { throw 'Private key material must not be included in the application package.' }
            [xml]$appManifest = Read-ZipText $package 'AppxManifest.xml'
            if ($appManifest.Package.Identity.ProcessorArchitecture -ne $app.Architecture) { throw 'Bundle and application architectures disagree.' }
            $executable = $appManifest.Package.Applications.Application.Executable.Replace('\', '/')
            $prefix = $executable.Substring(0, $executable.LastIndexOf('/') + 1)
            # Packaged applications merge their PRI into the package-root index.
            if (!$package.GetEntry('resources.pri').Length) { throw 'Missing package resource index.' }
            foreach ($name in @('WSAppBak.exe', 'WSAppBak.dll', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'WSTools/makeappx.exe', 'WSTools/makecert.exe', 'WSTools/pvk2pfx.exe', 'WSTools/signtool.exe')) {
                $file = $package.GetEntry($prefix + $name)
                if (!$file -or !$file.Length) { throw "$($app.Architecture) package is missing $name" }
            }
            $runtime = (Read-ZipText $package ($prefix + 'WSAppBak.runtimeconfig.json') | ConvertFrom-Json).runtimeOptions
            if (!$runtime.includedFrameworks -or $runtime.framework -or $runtime.frameworks) { throw 'The packaged app must include its .NET runtime.' }
            # Verify native payloads, not just labels in the manifests.
            foreach ($name in @('WSAppBak.exe', 'coreclr.dll')) {
                $peStream = [IO.MemoryStream]::new()
                $source = $package.GetEntry($prefix + $name).Open()
                try { $source.CopyTo($peStream) } finally { $source.Dispose() }
                $reader = [IO.BinaryReader]::new($peStream)
                try {
                    $peStream.Position = 0x3c
                    $peOffset = $reader.ReadInt32()
                    $peStream.Position = $peOffset
                    if ($reader.ReadUInt32() -ne 0x4550) { throw "Invalid PE file: $name" }
                    $expected = if ($app.Architecture -eq 'x64') { 0x8664 } else { 0xaa64 }
                    if ($reader.ReadUInt16() -ne $expected) { throw "Wrong native architecture: $($app.Architecture)/$name" }
                }
                finally { $reader.Dispose() }
            }
            $dependencyDir = Join-Path (Split-Path $Path -Parent) "Dependencies/$($app.Architecture)"
            $dependencyIdentities = @(foreach ($file in Get-ChildItem -LiteralPath $dependencyDir -File | Where-Object Extension -in @('.appx', '.msix')) {
                $dependencyZip = [IO.Compression.ZipFile]::OpenRead($file.FullName)
                try {
                    [xml]$dependencyManifest = Read-ZipText $dependencyZip 'AppxManifest.xml'
                    $dependencyManifest.Package.Identity
                }
                finally { $dependencyZip.Dispose() }
            })
            foreach ($dependency in $appManifest.Package.Dependencies.PackageDependency) {
                $found = @($dependencyIdentities | Where-Object {
                    $_.Name -eq $dependency.Name -and $_.Publisher -eq $dependency.Publisher -and
                    [version]$_.Version -ge [version]$dependency.MinVersion -and $_.ProcessorArchitecture -in @($app.Architecture, 'neutral')
                })
                if (!$found.Count) { throw "Missing $($app.Architecture) installer dependency: $($dependency.Name)" }
            }
            Write-Host "PASS: $($app.Architecture) package, resource index, native architecture, .NET runtime and installer dependencies."
        }
        finally { $package.Dispose(); $stream.Dispose() }
    }
}
finally { $bundle.Dispose() }
