import json
from pathlib import Path
import tempfile
import unittest
import uuid
from unittest.mock import patch

from native_suite_host import Host
import run_windows_tab5_native_suite as suite


class HostTest(unittest.TestCase):
    def test_locked_desktop_keeps_request_unreserved(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder).resolve()
            dll=root/"AIBotBridge.dll"
            dll.write_bytes(b"fixture")
            entry={"path":str(dll),"sha256":suite.digest(dll)}
            host=Host({"root":str(root),"hosts":{"vscode":"A"*64},"resume":entry})
            request=host.requests/(str(uuid.uuid4())+".json")
            request.write_text("{}")
            host.expires=2
            with patch("native_suite_host.time.monotonic",side_effect=[0,3]),\
                 patch("native_suite_host.time.sleep"),\
                 patch("native_suite_host.desktop_ready",return_value=False),\
                 patch("native_suite_host.suite.execute",return_value=type("Result",(),{"returncode":0})()),\
                 patch.object(host,"job") as run:
                self.assertEqual(host.loop(entry),0)
                run.assert_not_called()
            self.assertFalse(request.with_suffix(".reserved").exists())

    def test_scoped_plan_and_single_job_reservation(self):
        with tempfile.TemporaryDirectory() as folder:
            # Windows TEMP can use an 8.3 alias; validation returns a resolved path.
            root=Path(folder).resolve()
            proof=root/"photo.json"
            proof.write_text("{}")
            entry=lambda path: {"path":str(path),"sha256":suite.digest(path)}
            proof_entry=entry(proof)
            hosts={"vscode":"A"*64}
            host=Host({"root":str(root),"hosts":hosts,"resume":proof_entry})
            dll=root/"candidate"/"AIBotBridge.dll"
            exe=dll.with_suffix(".exe")
            dll.parent.mkdir()
            dll.write_bytes(b"binary")
            exe.write_bytes(b"apphost")
            plan={"candidate":entry(dll),"resident":entry(exe),"residentDll":entry(dll),
                  "hosts":hosts,"resume":{"vscode":proof_entry},"output":str(root/"results")}
            manifest=root/"plan.json"
            manifest.write_text(json.dumps(plan))
            manifest_entry=entry(manifest)
            self.assertEqual(host.validate(manifest_entry),manifest)
            for key,value in [("hosts",{"vscode":"B"*64}),("output",str(root.parent/"outside")),
                              ("resume",{"vscode":{"path":str(proof),"sha256":"b"*64}})]:
                changed={**plan,key:value}
                manifest.write_text(json.dumps(changed))
                with self.assertRaises(ValueError):
                    host.validate(entry(manifest))
            manifest.write_text(json.dumps(plan))
            job_id=str(uuid.uuid4())
            request=host.requests/(job_id+".json")
            request.write_text(json.dumps({"id":job_id,"kind":"controls-suite","plan":manifest_entry}))
            with patch("native_suite_host.suite.run",return_value=0) as run:
                host.job(request)
                host.job(request)
                self.assertEqual(run.call_count,1)
            # An interrupted reserved job remains unresolved and must never replay.
            job_id=str(uuid.uuid4())
            request=host.requests/(job_id+".json")
            request.write_text(json.dumps({"id":job_id,"kind":"controls-suite","plan":manifest_entry}))
            request.with_suffix(".reserved").write_text("reserved")
            with patch("native_suite_host.suite.run") as run:
                host.job(request)
                run.assert_not_called()
            job_id=str(uuid.uuid4())
            request=host.requests/(job_id+".json")
            request.write_text(json.dumps({"id":job_id,"kind":"shell","plan":manifest_entry}))
            with patch("native_suite_host.suite.run") as run:
                host.job(request)
                run.assert_not_called()
            self.assertFalse(json.loads((host.results/request.name).read_text())["passed"])


if __name__=="__main__":
    unittest.main()
