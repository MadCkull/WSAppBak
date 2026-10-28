[CmdletBinding()]
param(
    [string]$OutputRoot,
    [string]$MSBuildPath,
    [string]$CertificatePath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts/msix' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (!$MSBuildPath) {
    $command = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($command) { $MSBuildPath = $command.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild Microsoft.NetCore.Component.SDK -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
}
if (!$MSBuildPath -or !(Test-Path -LiteralPath $MSBuildPath)) { throw 'Visual Studio MSBuild with the .NET and Windows packaging tools is required.' }
if ($CertificatePath) { $CertificatePath = (Resolve-Path -LiteralPath $CertificatePath).Path }

# Visual Studio's MrmSupportLibrary.GetLocation dereferences this variable without
# a null check. Some automation hosts omit it. Restore the caller's environment.
$originalArchitecture = $env:PROCESSOR_ARCHITECTURE
$originalPassword = $env:PackageCertificatePassword
$runRoot = Join-Path $OutputRoot ([Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $runRoot -Force
try {
    if (!$env:PROCESSOR_ARCHITECTURE) {
        $env:PROCESSOR_ARCHITECTURE = switch ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
            'X64' { 'AMD64' }
            'Arm64' { 'ARM64' }
            'X86' { 'x86' }
            default { throw 'Unsupported Windows build host architecture.' }
        }
    }
    $arguments = @(
        (Join-Path $repoRoot 'WSAppBak/WSAppBak (Package)/WSAppBak (Package).wapproj'),
        '/restore', '/t:Build', '/verbosity:minimal', '/p:Configuration=Release', '/p:Platform=x64',
        '/p:WindowsPackageType=MSIX', '/p:WindowsAppSDKSelfContained=false',
        '/p:UapAppxPackageBuildMode=SideloadOnly', '/p:AppxBundle=Always', '/p:AppxBundlePlatforms=x64|arm64',
        # The .NET 6 crossgen compiler crashes when producing ARM64 ReadyToRun
        # images on the tested host. Use normal runtime compilation for both.
        '/p:PublishReadyToRun=false', '/p:SelfContained=true', '/p:UseSharedCompilation=false',
        "/p:AppxPackageDir=$runRoot\"
    )
    if ($CertificatePath) {
        if (!$env:WSAPPBAK_CERTIFICATE_PASSWORD) { throw 'Set WSAPPBAK_CERTIFICATE_PASSWORD to sign the release.' }
        $env:PackageCertificatePassword = $env:WSAPPBAK_CERTIFICATE_PASSWORD
        $arguments += @('/p:AppxPackageSigningEnabled=true', "/p:PackageCertificateKeyFile=$CertificatePath", '/p:PackageCertificateThumbprint=')
        # Do not record a binary log of a build that can contain signing secrets.
    }
    else {
        $arguments += @('/p:AppxPackageSigningEnabled=false', "/bl:$runRoot\build.binlog")
    }
    & $MSBuildPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "MSIX build failed with exit code $LASTEXITCODE." }
    $bundles = @(Get-ChildItem -LiteralPath $runRoot -Recurse -Filter *.msixbundle)
    if ($bundles.Count -ne 1) { throw "Expected one new MSIX bundle; found $($bundles.Count)." }
    & "$PSScriptRoot/Test-MSIX.ps1" -Path $bundles[0].FullName
    Get-FileHash -LiteralPath $bundles[0].FullName -Algorithm SHA256 | Format-List
    Write-Host "MSIX bundle: $($bundles[0].FullName)"
}
finally {
    $env:PROCESSOR_ARCHITECTURE = $originalArchitecture
    $env:PackageCertificatePassword = $originalPassword
}
