# MSIX packaging validation

Validated locally on 28 October 2026. These changes are not published.

## Reproduced failures and fixes

1. `APPX0002` in `GenerateAppxPackageRecipe`, with a null reference in
   `MrmSupportLibrary.GetLocation`, reproduced with the original project as well
   as the portable fix. Inspection of the installed Visual Studio task showed an
   unconditional `Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")`
   followed by `ToLowerInvariant()`. This automation environment omitted that
   variable. Setting it to `AMD64` made the original x64 package build succeed.
   `Build-MSIX.ps1` now fills it from the host architecture only when absent and
   restores the caller's environment afterward.
2. A complete x64/ARM64 build then reproduced `NETSDK1096` and an
   `AccessViolationException` in the .NET 6 ReadyToRun compiler while publishing
   ARM64. The packaged profiles and build script disable this optional ahead of
   time optimization. The applications use normal runtime compilation.
3. The publish profiles were ignored by `*.pubxml`; the x64 profile also pointed
   to a directory named `win10-arm64`. Both profiles are now included in source
   control and publish to their matching architecture directories.
4. The profiles formerly required a separately installed .NET 6 runtime. They
   now include that runtime. Windows App SDK and VC framework dependencies stay
   as package dependencies and are supplied alongside the bundle.
5. The release ZIP formerly discarded the generated `Dependencies` folder.
   It now contains that folder and an architecture-aware `Install.ps1`. Its public
   certificate is exported from the actual signed bundle. The project no longer
   embeds a signing password or a machine-specific output directory.

## Validation

| Check | Result |
| --- | --- |
| Source-only build, no previous bin/obj files | Signed x64/ARM64 bundle built successfully |
| Unsigned CI script executed locally | Build and validation passed |
| Bundle contents | Both application architectures present; PE headers of app hosts and coreclr agree |
| Runtime and resources | Package-root resources.pri, .NET runtime, runtimeconfig and required WSTools present for both architectures |
| Dependency manifests | Matching name, publisher, architecture and minimum version available for every declared framework dependency |
| Signing | Bundle signature valid, using the existing CN=MadCkull certificate |
| Clean offline Windows Sandbox | Installer ZIP extracted; certificate trusted inside Sandbox; Install.ps1 installed bundle and dependencies successfully |
| Installed x64 app | Registered under WindowsApps; main window responsive after 10 seconds; Help opened and rendered |
| Missing dependency directory | Validator rejected the incomplete installer |
| ARM64 package removed from a test copy | Validator rejected the damaged bundle |
| Portable regression | Rebuilt, extracted, checked and launched successfully; responsive window remained open |
| PowerShell syntax and git diff whitespace | Passed |

Local build tools: Visual Studio MSBuild 17.14.8, .NET SDK 9.0.300 targeting
.NET 6, Windows App SDK 1.7.250606001 and Windows SDK BuildTools 10.0.26100.4188.
The GitHub workflows select .NET SDK 6 and Windows Server 2022; the hosted jobs
have not been run in this session.

The Sandbox was Windows 11 Enterprise Insider, reporting version 10.0.26100
(desktop watermark 26100.9750). Networking and clipboard redirection were
disabled. It had CBS Windows App Runtime packages but not the declared
Microsoft.WindowsAppRuntime.1.7 dependency before installation. No separate
.NET runtime installation was performed. The signing certificate was imported
only into the disposable Sandbox during this test.

One existing CA1416 warning remains in AppRetrievalService.cs: the logo API needs
Windows build 19041 while the declared minimum is 17763. The portable regression
restore also emitted NU1900 because NuGet's vulnerability service was unavailable;
the build and launch passed using restored dependencies.

## Candidate artifacts

- Installer ZIP: `artifacts/msix-candidate/WSAppBak MSIX candidate (x64-ARM64).zip`
- ZIP SHA-256: `343D3AC03D9CB56F30DAC2D9EEBE0801519D8F9816E4EABD6851BF5C1F95E9E9`
- Signed bundle SHA-256: `C533C8BB167166783A11223C33050BEC93A607F2FA9A58FC68E8A29FF41F2861`
- Sandbox evidence: `artifacts/msix-investigation/sandbox-results/`
- Portable regression ZIP SHA-256: `A389EAD69224F3BF8CE9C5B7F066B8B23C5E3A5DAF14E02A6F7A6D3A80624BB4`

The candidate retains manifest version 2.0.0.0. It is a local test artifact, not
a versioned release or an upgrade test against an already installed 2.0.0.0.

Remaining checks: runtime testing on ARM64 hardware, stable Windows 11 and
Windows 10; hosted CI/release execution; and an appropriately versioned release
before closing issues as released fixes. App export, patching, and third-party
package installation are outside this packaging/startup validation.
