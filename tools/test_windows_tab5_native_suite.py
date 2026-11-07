import json
import tempfile
import unittest
from types import SimpleNamespace
from pathlib import Path
from unittest.mock import patch

import run_windows_tab5_native_suite as batch


class SuiteTest(unittest.TestCase):
    def test_single_tray_and_its_registered_health_guard(self):
        exe = Path("AIBotBridge.exe").resolve()
        def process(pid, parent, args, path=exe):
            return SimpleNamespace(pid=pid, exe=lambda:str(path), ppid=lambda:parent,
                                   cmdline=lambda:[str(path), *args])
        tray = process(41, 1, [])
        args = ["--runtime-health-guard", "41", "Local\\AIBotBridge.Health.41."+"a"*32]
        guard = process(42, 41, args)
        self.assertIs(batch.identify_tray(exe, [guard, tray]), tray)
        self.assertIs(batch.identify_tray(exe, [tray]), tray)
        self.assertIsNone(batch.identify_tray(exe, []))
        for other in [process(43, 1, []), process(43, 1, args),
                      process(43, 41, [args[0], "40", args[2]]),
                      process(43, 41, [*args[:2], "unknown"]),
                      process(43, 41, args, exe.parent/"elsewhere"/exe.name)]:
            with self.subTest(other=other.pid), self.assertRaises(RuntimeError):
                batch.identify_tray(exe, [tray, other])

    def test_blocked_host_does_not_skip_other_host_or_repeat_an_uncertain_send(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            binary = root / "candidate.dll"
            binary.write_bytes(b"fixture")
            candidate = batch.digest(binary)
            out = root / "results"
            plan = {"candidate":{"path":str(binary),"sha256":candidate}, "output":str(out),
                    "hosts":{"terminal":"A"*64, "vscode":"B"*64}}
            path = root / "plan.json"
            path.write_text(json.dumps(plan))
            calls = []

            def child(_dotnet, _dll, output, host, scope, probe):
                calls.append((host, probe))
                if probe:
                    return {"host":host,"stage":"ready" if host=="terminal" else "original-client",
                            "passed":False,"exitCode":0 if host=="terminal" else 1}
                result={"host":host,"scope":scope,"stage":"physical-device","reason":"append_unconfirmed",
                        "candidateSha256":candidate,"passed":False,"cases":{"photo":"passed","append":"unconfirmed","interrupt":"not_run"}}
                Path(str(output)+".json").write_text(json.dumps(result))
                return result

            with patch.object(batch,"run_cli",side_effect=child), patch.object(batch,"execute",return_value=SimpleNamespace(returncode=0,stdout=b"{}")), patch.object(batch,"take_usb_ownership",return_value=None) as take:
                self.assertEqual(batch.run(path),1)
                self.assertEqual(batch.run(path),1)
                self.assertEqual(calls.count(("terminal",False)),1)
                self.assertNotIn(("vscode",False),calls)
                self.assertEqual(take.call_count,1)
                # Changing the original target cannot reuse either evidence or reservation.
                plan["hosts"]["terminal"]="C"*64
                path.write_text(json.dumps(plan))
                self.assertEqual(batch.run(path),1)
                self.assertEqual(calls.count(("terminal",False)),1)


if __name__ == "__main__":
    unittest.main()
