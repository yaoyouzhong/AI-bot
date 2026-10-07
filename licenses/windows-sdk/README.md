# Windows SDK targeting-pack distribution evidence

Reviewed 2026-10-07 for `Microsoft.Windows.SDK.NET.Ref/10.0.22000.56`.

- Package metadata: https://www.nuget.org/packages/Microsoft.Windows.SDK.NET.Ref/10.0.22000.56
- `sdk_license.rtf`: original bytes from the package's license URL,
  https://aka.ms/WinSDKLicenseURL, resolved to
  https://download.microsoft.com/download/0/F/F/0FF2B061-47DD-4F55-89B6-FD1D8C44F14D/sdk_license.rtf.
- `sdk_license.txt`: plain-text convenience rendering of that RTF using Windows
  RichTextBox. The original RTF is authoritative; neither file is AI-bot's MIT license.
- `REDIST.html`: saved official page from https://go.microsoft.com/fwlink/?LinkId=524842,
  canonical https://learn.microsoft.com/en-us/legal/windows-sdk/redist.
  Its `Microsoft.Windows.SDK.NET.Ref` section explicitly includes
  `lib/net8.0/Microsoft.Windows.SDK.NET.dll` and `lib/net8.0//WinRT.Runtime.dll`.

The official list permits unmodified package files to accompany an application
that calls WinRT APIs, subject to the SDK terms. AI-bot distributes only the two
runtime DLLs it needs, verifies their bytes against the restored targeting pack,
and includes the original terms, this evidence and the original nuspec. It does
not relicense these DLLs under MIT or distribute the entire SDK. The SDK terms,
including section 2's distribution requirements/restrictions, continue to apply.
See the package's `DISTRIBUTION_TERMS.md` before use or redistribution.

`materials.json` records the reviewed version and file hashes. Dependency or
evidence changes fail the collector until reviewed and recorded again. This is
a check for this concrete package, not a blanket approval of future SDK versions.

The 2026-10-07 review re-fetched the package license URL and confirmed the original RTF is byte-identical. The official redist list still names both runtime DLLs. The newer targeting pack supports the guarded Windows 11 BLE connection preference API; Windows 10 remains the application target. DLL hashes were refreshed from this exact restored package and are checked against the shipped files.
