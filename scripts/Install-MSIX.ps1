# Run from the extracted installer ZIP after trusting its signing certificate.
$ErrorActionPreference = 'Stop'
$bundles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter *.msixbundle)
if ($bundles.Count -ne 1) { throw 'Extract the complete installer ZIP before running Install.ps1.' }
$architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
if ($architecture -notin @('x64', 'arm64')) { throw 'WSAppBak requires x64 or ARM64 Windows.' }
$dependencies = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "Dependencies/$architecture") -File | Where-Object Extension -in @('.appx', '.msix'))
if (!$dependencies.Count) { throw "The installer is missing its $architecture dependencies. Extract the complete ZIP." }
Add-AppxPackage -Path $bundles[0].FullName -DependencyPath $dependencies.FullName
Write-Host 'WSAppBak installed successfully. Open WSAppBak from the Start menu.'
