# Portable startup repair: issues #15 and #16

## Diagnosis

The published v2.0.0 x64 ZIP has SHA-256
`271FD2109E8E73C156EC34B3E7126043D645ED56AF2A99283B7D829D18947519`.
Launching its freshly extracted executable reproduced Event ID 1000:
`Microsoft.UI.Xaml.dll` 3.1.7.0, exception `0xc000027b`, fault offset
`0x00000000003a11f5`. This matches issue #15.

The ZIP includes the framework resource indexes but omits `WSAppBak.pri`.
The ordinary build produces the application index; the .NET publish output
does not include it. Its compiled XAML is embedded in that index. Adding only
the freshly generated, complete index to the original released binaries made
them launch. A stale local index did not work, so an arbitrary old PRI is not
a reliable repair.

The upstream report is https://github.com/microsoft/WindowsAppSDK/issues/6720.
The project now includes the generated PRI in unpackaged publish items and
fails if generation did not produce it. This target is inactive for MSIX.

## Build safeguards

- Separate portable intermediates and NuGet-generated imports from the MSIX build.
- Explicitly include both .NET and Windows App SDK runtimes.
- Stage every publish in a new directory; never silently overwrite an existing ZIP.
- Validate required files and self-contained runtime configuration before compression.
- Extract the ZIP and repeat validation, optionally launching that extracted app
  from another working directory and requiring a responsive main window.
- Use the same build script for release and pull-request CI. CI configuration
  has been added locally; hosted Actions have not yet run for this change.

## Verification on 28 October 2026

Candidate: `WSAppBak v2.0.1-candidate (Portable - x64).zip` (local, unpublished).
SHA-256: `615164A5152F0CC6BF808096F57FF85F0CD7E919FE82EFAD803865865AB54879`.

| Check | Result |
| --- | --- |
| Published v2.0.0 startup | Reproduced the exact reported crash |
| Original binaries plus freshly generated PRI only | Window opened |
| Build from fresh source copy without old bin/obj | Passed |
| Original ZIP through new validator | Correctly rejected for missing WSAppBak.pri |
| Candidate ZIP after extraction | Layout and launch checks passed |
| Windows 11 Pro Insider, build 26340 | Candidate opened and remained responsive |
| Clean Windows 11 Enterprise Insider Sandbox, build 26100 | Exact candidate ZIP hash recorded; layout and launch passed with networking disabled and no additional runtime installation |
| Relocated folder containing spaces, different working directory | Passed |
| Installed-app search and selection | Windows Calculator appeared; selection enabled Start |
| Help window | Opened and rendered instructions |
| Folder picker | Opened; selection completion is not covered by the automatic smoke test |
| MSIX-only evaluation of the new target | Skipped successfully |

Local builds used Visual Studio MSBuild 17.14 and .NET SDK 9.0.300 targeting
.NET 6. Hosted CI installs SDK 6 but selected the runner's SDK 10.0.113. The separate MSIX build failure
originally reproduced with the unmodified project has since been diagnosed and
fixed; see [MSIX validation notes](msix-validation.md). The portable publish target
remains restricted to unpackaged builds.

## Remaining release checks

- Run the candidate on Windows 10 22H2 and a stable Windows 11 machine. Both
  currently tested environments are Windows 11 Insider builds.
- Hosted portable CI passed, including extracted-ZIP launch, for `43188e2`:
  https://github.com/MadCkull/WSAppBak/actions/runs/40782591817. The signed release
  workflow is the next release check.
- Publish a tested replacement before closing #15 and #16 as released fixes.

These checks concern portable packaging and startup. Exporting/signing a
third-party app and its later installation or licensing are separate workflows.
