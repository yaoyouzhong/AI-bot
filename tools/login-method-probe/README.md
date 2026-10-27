# Windows login-method feasibility probe

This independent experiment observes the active locked Windows console on the
Winlogon desktop for 55 seconds. It does not read credential values, inject input,
change authentication settings, install a credential provider, or contact a
network. It is not connected to the bridge and does not establish unlock success.

Only exact allowlisted labels are classified as PIN, password, face, fingerprint
or sign-in options. Unmatched labels are discarded. Observation mode does not read
edit names; all modes exclude Value/Text patterns. Automation IDs are recorded as hashes and coarse hints;
these hints do not prove the active authentication provider.

Build with `dotnet build tools/login-method-probe/LoginMethodProbe.csproj -c Release -o artifacts/development/login-method-20261022/probe`.
Run `LoginMethodProbe.exe --self-test` for classification/session guard checks.
Package the four runtime files, their `sha256.json` manifest and
`Run-Observation.ps1`. The script requires normal Windows administrator consent.
It creates a fresh protected Program Files directory and a one-shot SYSTEM task
with no recurring trigger. It waits up to 180 seconds for the user to lock the
console, then copies its own SYSTEM token into that same console session for the
bounded observer. Session changes or unknown lock state end the observation.

The task is unregistered on exit. Matching temporary executable files are removed
when both processes have exited; sanitized observations and the receipt remain.
Existing paths/tasks are never replaced. Keep the usual physical keyboard and
unlock Windows through the usual method after the observation.

After real observation identifies unique actionable method buttons,
`Run-Observation.ps1 -Mode cycle` installs a separate bounded experiment. Starting
from a manually displayed fingerprint page, it selects PIN/password/PIN/password/PIN
and leaves the PIN page open. There is no credential input, Enter key, authentication
attempt, default-provider setting, network or bridge integration. Each options/target
invocation is consumed before the call and never repeated after an uncertain result.
Selection mode also classifies the protected edit's semantic UIA Name label on
verified system LogonUI, discarding raw names. Per Microsoft's Edit control contract,
Name identifies the control and must not contain its textual contents; Value/Text
are separate properties and are never requested. This fixes verification when
the PIN/password label is exposed on the edit rather than a standalone Text node.
See https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporteditcontroltype.
It requires an unambiguous visible method label, one
protected edit and three fresh focused observations. UI transitions are re-observed;
unknown/ambiguous state, timeout or session/desktop changes stop the experiment.

No production integration is accepted until controlled switching confirms the
method, and real-device unlocking must be accepted separately.
