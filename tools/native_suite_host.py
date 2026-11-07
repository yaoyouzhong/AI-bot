"""Task-scoped normal-desktop host for the fixed TAB5 native acceptance CLI."""
import argparse
import json
from pathlib import Path
import time
import uuid
import os
import psutil

import run_windows_tab5_native_suite as suite


def desktop_ready():
    import ctypes
    user=ctypes.WinDLL("user32",use_last_error=True)
    kernel=ctypes.WinDLL("kernel32",use_last_error=True)
    user.GetForegroundWindow.restype=ctypes.c_void_p
    user.GetWindowThreadProcessId.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_ulong)]
    user.GetThreadDesktop.argtypes=[ctypes.c_ulong]
    user.GetThreadDesktop.restype=ctypes.c_void_p
    user.GetUserObjectInformationW.argtypes=[ctypes.c_void_p,ctypes.c_int,ctypes.c_void_p,
                                            ctypes.c_ulong,ctypes.POINTER(ctypes.c_ulong)]
    active=ctypes.c_int()
    needed=ctypes.c_ulong()
    desktop=user.GetThreadDesktop(kernel.GetCurrentThreadId())
    if not user.GetUserObjectInformationW(desktop,6,ctypes.byref(active),ctypes.sizeof(active),ctypes.byref(needed)) or not active.value:
        return False
    pid=ctypes.c_ulong()
    user.GetWindowThreadProcessId(user.GetForegroundWindow(),ctypes.byref(pid))
    if not pid.value:
        return False
    try:
        return psutil.Process(pid.value).name().lower() not in {"lockapp.exe","logonui.exe","credentialuibroker.exe"}
    except psutil.Error:
        return False


class Host:
    def __init__(self, config):
        self.root = Path(config["root"]).resolve(strict=True)
        self.hosts = config["hosts"]
        self.resume = config["resume"]
        if set(self.hosts) != {"vscode"}:
            raise ValueError("Only the authorized VS Code control continuation is supported")
        self.checked(self.resume)
        self.folder = self.root / "native-suite-host"
        self.requests = self.folder / "requests"
        self.results = self.folder / "results"
        self.requests.mkdir(parents=True, exist_ok=True)
        self.results.mkdir(exist_ok=True)
        lease=self.folder/"lease.json"
        if not lease.exists():
            old=self.folder/"state.json"
            started=json.loads(old.read_text()).get("processStart",time.time()) if old.exists() else time.time()
            with lease.open("x",encoding="utf-8") as file:
                json.dump({"expiresAt":started+8*3600},file)
        deadline=json.loads(lease.read_text())["expiresAt"]
        self.expires = time.monotonic() + max(0,min(8*3600,deadline-time.time()))
        self.process_start = psutil.Process().create_time()

    def inside(self, path):
        resolved = Path(path).resolve()
        if not resolved.is_relative_to(self.root):
            raise ValueError("Path escapes the authorized task")
        return resolved

    def checked(self, entry):
        path = self.inside(entry["path"])
        return suite.checked_path({"path": str(path), "sha256": entry["sha256"]})

    def validate(self, entry):
        manifest = self.checked(entry)
        if manifest.stat().st_size > 16384:
            raise ValueError("Plan too large")
        plan = json.loads(manifest.read_text(encoding="utf-8"))
        if set(plan) != {"candidate", "resident", "residentDll", "hosts", "output", "resume"}:
            raise ValueError("Unsupported plan")
        if plan["hosts"] != self.hosts or plan["resume"] != {"vscode": self.resume}:
            raise ValueError("Original target or accepted photo changed")
        for name, expected in [("candidate", "AIBotBridge.dll"), ("resident", "AIBotBridge.exe"),
                               ("residentDll", "AIBotBridge.dll")]:
            if self.checked(plan[name]).name != expected:
                raise ValueError("Unsupported executable")
        self.inside(plan["output"])
        return manifest

    def state(self, **data):
        value = {"pid": os.getpid(), "processStart": self.process_start, **data}
        temp = self.folder / "state.tmp"
        temp.write_text(json.dumps(value), encoding="utf-8")
        temp.replace(self.folder / "state.json")

    def job(self, request):
        result = self.results / request.name
        marker = request.with_suffix(".reserved")
        if result.exists() or marker.exists():
            return
        reply = {"passed": False}
        try:
            if request.stat().st_size > 16384:
                raise ValueError("Request too large")
            job = json.loads(request.read_text(encoding="utf-8"))
            if str(uuid.UUID(request.stem)) != request.stem or job["id"] != request.stem:
                raise ValueError("Invalid request identity")
            if job["kind"] != "controls-suite":
                raise ValueError("Unsupported operation")
            manifest = self.validate(job["plan"])
            with marker.open("x", encoding="utf-8") as file:
                file.write(json.dumps({"id": job["id"], "plan": job["plan"]}))
                file.flush()
                os.fsync(file.fileno())
            self.state(stage="running", job=job["id"])
            reply = {"passed": suite.run(manifest) == 0, "id": job["id"]}
        except Exception as exc:
            reply["error"] = type(exc).__name__
        result.write_text(json.dumps(reply), encoding="utf-8")
        self.state(stage="ready", lastJob=request.stem, result=reply)

    def run(self, bootstrap):
        import msvcrt
        with (self.folder / "host.lock").open("a+b") as lock:
            if lock.tell() == 0:
                lock.write(b"1")
                lock.flush()
            lock.seek(0)
            try:
                msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
            except OSError:
                return 0
            return self.loop(bootstrap)

    def loop(self, bootstrap):
        # The real bridge owns profile verification. No environment/path override.
        binary = self.checked(bootstrap)
        if binary.name != "AIBotBridge.dll":
            raise ValueError("Invalid bootstrap")
        self.state(stage="starting")
        try:
            started = suite.execute(["dotnet", str(binary), "--status-once"], 60)
        except Exception as exc:
            self.state(stage="blocked", error=type(exc).__name__)
            return 1
        if started.returncode:
            self.state(stage="blocked", error="desktop_profile_or_startup")
            return 1
        self.state(stage="ready")
        waiting=False
        while time.monotonic() < self.expires and not (self.folder / "stop").exists():
            if not desktop_ready():
                if not waiting:self.state(stage="waiting_for_unlock")
                waiting=True
                time.sleep(0.5)
                continue
            if waiting:
                self.state(stage="ready")
                waiting=False
            for request in sorted(self.requests.glob("*.json")):
                self.job(request)
            time.sleep(0.5)
        self.state(stage="stopped")
        return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", type=Path, required=True)
    args = parser.parse_args()
    config = json.loads(args.config.read_text(encoding="utf-8"))
    raise SystemExit(Host(config).run(config["bootstrap"]))
