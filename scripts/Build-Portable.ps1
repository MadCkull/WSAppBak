[CmdletBinding()]
param(
    [string]$Version = 'dev',
    [string]$OutputRoot,
    [string]$MSBuildPath,
    [switch]$SmokeTest
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ($Version -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'Version must be a filename-safe version label.' }
if (!$OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts/portable' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (!$MSBuildPath) {
    $msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($msbuildCommand) { $MSBuildPath = $msbuildCommand.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild Microsoft.NetCore.Component.SDK -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
}
if (!$MSBuildPath -or !(Test-Path -LiteralPath $MSBuildPath)) { throw 'Visual Studio MSBuild is required (including the WinUI build tools).' }

# A fresh staging directory prevents stale files from concealing missing publish items.
$runRoot = Join-Path $OutputRoot ([Guid]::NewGuid().ToString('N'))
$publishPath = Join-Path $runRoot 'publish'
$extractedPath = Join-Path $runRoot 'extracted'
$zipPath = Join-Path $OutputRoot "WSAppBak $Version (Portable - x64).zip"
if (Test-Path -LiteralPath $zipPath) { throw "Output already exists; choose another version or output folder: $zipPath" }
$null = New-Item -ItemType Directory -Path $runRoot -Force
$projectDir = Join-Path $repoRoot 'WSAppBak/WSAppBak'
$buildArguments = @(
    (Join-Path $projectDir 'WSAppBak.csproj'), '/restore', '/t:Publish', '/verbosity:minimal',
    '/p:Configuration=Release', '/p:Platform=x64', '/p:RuntimeIdentifier=win10-x64',
    '/p:WindowsPackageType=None', '/p:WindowsAppSDKSelfContained=true', '/p:SelfContained=true',
    '/p:PublishSingleFile=false', '/p:UseSharedCompilation=false',
    '/p:IntermediateOutputPath=obj\portable\Release\x64\',
    "/p:MSBuildProjectExtensionsPath=$projectDir\obj\portable\",
    '/p:OutputPath=bin\portable\Release\x64\',
    "/p:PublishDir=$publishPath\", "/bl:$runRoot\build.binlog"
)
& $MSBuildPath @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Portable build failed with exit code $LASTEXITCODE." }
& "$PSScriptRoot/Test-Portable.ps1" -Path $publishPath

$stagedZip = Join-Path $runRoot 'portable.zip'
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $stagedZip
Expand-Archive -LiteralPath $stagedZip -DestinationPath $extractedPath
& "$PSScriptRoot/Test-Portable.ps1" -Path $extractedPath -Launch:$SmokeTest
# Only expose the final ZIP once its extracted contents have passed validation.
Copy-Item -LiteralPath $stagedZip -Destination $zipPath
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Format-List
Write-Host "Portable ZIP: $zipPath"
