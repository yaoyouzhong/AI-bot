# Wi-Fi unlock trial

Local, one-attempt experiment for the paired TAB5 and current Windows console session. This is not installed or started by building it. Never pass a real PIN as a command-line argument, test fixture, log, or chat message.

Build with `dotnet build tools/wifi-unlock-trial/WifiUnlockTrial.csproj -c Release -o artifacts/development/wifi-unlock-20261017/trial`; run the output DLL with `--self-test` for synthetic tests without input injection.

The review package includes the four runtime files, `sha256.json`, `Run-Trial.ps1`, and `Remove-Trial.ps1`. After explicit approval, Run-Trial runs through normal Windows administrator consent, verifies hashes, creates a fresh protected installation directory and a bounded SYSTEM task. It does not change Windows authentication providers. Existing installation paths are retained and cause refusal. The user must select the PIN page and enter the PIN only on TAB5 over Wi-Fi with USB/Bluetooth disconnected. One submission consumes the run; uncertain results are never retried automatically.

Remove-Trial validates exact directory, ownership receipt, files and task before removing this tool's executable files. Reports contain only status and remain for review. Do not run either elevated script merely to validate syntax.

Protocol and limitations: [WIFI-UNLOCK-TRIAL-PROTOCOL.md](../../docs/development/WIFI-UNLOCK-TRIAL-PROTOCOL.md).
