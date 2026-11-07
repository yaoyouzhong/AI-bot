"""One bounded normal-desktop run for the physical TAB5 Claude acceptance suite."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time

import psutil


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def checked_path(entry):
    path = Path(entry["path"]).resolve(strict=True)
    if digest(path) != entry["sha256"].lower():
        raise ValueError("The frozen executable changed")
    return path


def execute(args, timeout):
    return subprocess.run(args, capture_output=True, timeout=timeout,
                          creationflags=subprocess.CREATE_NO_WINDOW)


def run_cli(dotnet, dll, output, host, scope, probe, resume=None):
    flag = "--tab5-native-suite-probe" if probe else "--tab5-native-suite"
    args=[dotnet, str(dll), flag, str(output), host, scope]
    if resume is not None:
        previous=checked_path(resume)
        args[2]="--tab5-native-controls-probe" if probe else "--tab5-native-controls"
        args.extend([str(previous),resume["sha256"]])
    try:
        child = execute(args, 70 if probe else 680)
    except subprocess.TimeoutExpired:
        return {"host": host, "stage": "timeout", "reason": "child_deadline", "passed": False}
    report = Path(str(output) + ".json")
    if not report.exists():
        return {"host": host, "stage": "process", "reason": "missing_report",
                "exitCode": child.returncode, "passed": False}
    data = json.loads(report.read_text(encoding="utf-8-sig"))
    if data.get("scope") != scope or data.get("candidateSha256") != digest(dll):
        raise ValueError("Candidate or native scope changed during acceptance")
    data["exitCode"] = child.returncode
    return data


def identify_tray(resident, processes):
    """Accept exactly one tray, optionally accompanied by its own health guard."""
    if not processes:
        return None
    rows = [(p, p.cmdline()) for p in processes]
    if any(Path(p.exe()).resolve() != resident for p, _ in rows):
        raise RuntimeError("bridge_executable_path_mismatch")
    trays = [p for p, args in rows if len(args) == 1]
    if len(trays) != 1 or len(rows) > 2:
        raise RuntimeError("unrecognized_or_multiple_bridge_processes")
    tray = trays[0]
    for child, args in rows:
        if child.pid == tray.pid:
            continue
        mapped_name = f"Local\\AIBotBridge.Health.{tray.pid}."
        if (child.ppid() != tray.pid or len(args) != 4
                or args[1:3] != ["--runtime-health-guard", str(tray.pid)]
                or not re.fullmatch(re.escape(mapped_name) + "[0-9a-fA-F]{32}", args[3])):
            raise RuntimeError("unverified_bridge_child")
    return tray


def take_usb_ownership(plan):
    # Only signal the normal tray exit path. Its Dispose owns the health guard.
    resident = checked_path(plan["resident"])
    checked_path(plan["residentDll"])
    named = [p for p in psutil.process_iter(["name"]) if
             (p.info["name"] or "").lower() == resident.name.lower()]
    tray = identify_tray(resident, named)
    if tray is None:
        return None
    child = execute([str(resident), "--exit"], 10)
    if child.returncode:
        raise RuntimeError("normal_tray_exit_failed")
    tray.wait(timeout=25)
    return resident


def save(path, summary):
    tmp = path.with_suffix(".tmp")
    tmp.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    tmp.replace(path)


def run(plan_path):
    plan = json.loads(plan_path.read_text(encoding="utf-8-sig"))
    dll = checked_path(plan["candidate"])
    dotnet = shutil.which("dotnet")
    if os.name != "nt" or not dotnet:
        raise RuntimeError("Windows and dotnet required")
    root = Path(plan["output"]).resolve()
    root.mkdir(parents=True, exist_ok=True)
    stamp = str(time.time_ns())
    summary = {"candidateSha256": digest(dll), "runs": {}, "residentRestored": None}
    summary_path = root / ("batch-" + stamp + ".json")
    restore = None
    try:
        ready = []
        for host, scope in plan["hosts"].items():
            if host not in ("terminal", "vscode") or len(scope) != 64:
                raise ValueError("Invalid target")
            # A run that entered the USB suite may already have dispatched.
            # A subsequent invocation reads that host's result without resending.
            reservation = root / (host + ".reserved")
            if reservation.exists():
                original = json.loads(reservation.read_text(encoding="utf-8"))
                if original != {"scope": scope, "candidate": digest(dll)}:
                    raise ValueError("Previous campaign belongs to a different candidate or scope")
                result = root / (host + ".json")
                summary["runs"][host] = (json.loads(result.read_text(encoding="utf-8-sig"))
                    if result.is_file() else {"stage": "reserved", "passed": False})
                if result.is_file() and (summary["runs"][host].get("candidateSha256") != digest(dll)
                                         or summary["runs"][host].get("scope") != scope):
                    raise ValueError("Saved receipt identity changed")
                continue
            continuation=plan.get("resume",{}).get(host)
            options={"resume":continuation} if continuation else {}
            probe = run_cli(dotnet, dll, root / (host + "-probe-" + stamp), host, scope, True, **options)
            summary["runs"][host] = probe
            if probe["stage"] == "ready" and probe["exitCode"] == 0:
                ready.append((host, scope))
        save(summary_path, summary)
        if ready:
            status = execute([dotnet, str(dll), "--status-once"], 55)
            if status.returncode or not isinstance(json.loads(status.stdout), dict):
                raise RuntimeError("candidate_status_once_failed")
            summary["statusOncePassed"] = True
            if execute([dotnet, str(dll), "--check-tab5-suite-ocr"], 35).returncode:
                raise RuntimeError("device_label_reader_unavailable")
            restore = take_usb_ownership(plan)
        for host, scope in ready:
            with (root / (host + ".reserved")).open("x", encoding="utf-8") as reserve:
                reserve.write(json.dumps({"scope": scope, "candidate": digest(dll)}))
                reserve.flush()
                os.fsync(reserve.fileno())
            continuation=plan.get("resume",{}).get(host)
            options={"resume":continuation} if continuation else {}
            summary["runs"][host] = run_cli(dotnet, dll, root / host, host, scope, False, **options)
            save(summary_path, summary)
    except Exception as error:
        summary["batchError"] = str(error)
    finally:
        if restore is not None:
            if digest(restore) == plan["resident"]["sha256"].lower():
                tray = subprocess.Popen([str(restore)], cwd=restore.parent,
                                        creationflags=subprocess.CREATE_NO_WINDOW)
                time.sleep(2)
                summary["residentRestored"] = tray.poll() is None
            else:
                summary["residentRestored"] = False
        summary["passed"] = (len(summary["runs"]) == len(plan["hosts"])
                             and all(x.get("passed") for x in summary["runs"].values())
                             and not summary.get("batchError") and summary["residentRestored"] is not False)
        save(summary_path, summary)
    print(json.dumps({"report": str(summary_path), "passed": summary["passed"],
                      "hosts": {h: [x["stage"], x.get("reason")] for h, x in summary["runs"].items()}}))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("plan", type=Path)
    raise SystemExit(run(parser.parse_args().plan))
