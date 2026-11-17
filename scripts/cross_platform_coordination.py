"""Offline task/receipt contract and fail-closed release preflight. No GitHub writes."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

PLATFORMS = ("windows", "macos")
TERMINAL = ("completed", "already-present", "not-applicable")


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(value):
    require(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{40}", value), "Expected full lowercase commit SHA")
    return value


def nonempty(value):
    return isinstance(value, str) and bool(value.strip())


def identity(task):
    core = {k: v for k, v in task.items() if k != "taskId"}
    return hashlib.sha256(json.dumps(core, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def validate_task(task):
    require(isinstance(task, dict), "Task must be an object")
    require(type(task.get("schemaVersion")) is int and task["schemaVersion"] == 1, "Unsupported task schema")
    require(nonempty(task.get("repository")) and nonempty(task.get("branch")), "Missing repository/branch")
    sha(task.get("baseCommit")); sha(task.get("sourceCommit"))
    require(task["baseCommit"] != task["sourceCommit"], "Task has no source change")
    require(nonempty(task.get("summary")), "Missing change summary")
    require(task.get("taskId") == identity(task), "Task identity mismatch")
    return task


def new_task(repository, branch, base, head, summary):
    task = dict(schemaVersion=1, repository=repository, branch=branch,
                baseCommit=base, sourceCommit=head, summary=summary)
    task["taskId"] = identity(task)
    return validate_task(task)


def inspection_only(task):
    constraints = task.get('constraints', {})
    return (str(task.get('kind', '')).startswith('read-only')
            or (isinstance(constraints, dict) and constraints.get('inspectionOnly') is True))


def validate_receipt(task, receipt):
    validate_task(task)
    require(not task.get("simulationOnly"), "Simulation task cannot satisfy release gate")
    require(not inspection_only(task), "Read-only inspection cannot satisfy release gate")
    require(isinstance(receipt, dict), "Receipt must be an object")
    require(type(receipt.get("schemaVersion")) is int and receipt["schemaVersion"] == 1, "Unsupported receipt schema")
    require(not receipt.get("simulationOnly"), "Simulation receipt cannot satisfy release gate")
    require(receipt.get("taskId") == task["taskId"] and receipt.get("sourceCommit") == task["sourceCommit"], "Stale receipt")
    require(receipt.get("platform") in PLATFORMS, "Unknown platform")
    require(receipt.get("status") in TERMINAL, "Platform is pending, failed, or unacknowledged")
    require(nonempty(receipt.get("evidence")), "Missing evidence reference")
    require(nonempty(receipt.get("reason")), "Missing change-scope explanation")
    sha(receipt.get("implementationCommit"))
    if receipt["status"] != "not-applicable":
        verification = receipt.get("verification", {})
        require(isinstance(verification, dict), "Verification must be an object")
        require(verification.get("platform") == receipt["platform"], "Verification platform mismatch")
        require(verification.get("result") in ("passed", "reused"), "Native verification is not passed/reused")
        require(nonempty(verification.get("evidence")), "Missing native verification evidence")
        sha(verification.get("commit"))
        if verification["result"] == "passed":
            require(verification["commit"] == receipt["implementationCommit"], "Native evidence belongs to different implementation")
        else:
            require(nonempty(verification.get("reuseReason")), "Reused validation needs scope justification")
    return receipt


def ancestor(repo, earlier, later):
    # Both IDs validated before invoking git; no shell or user-controlled options.
    sha(earlier); sha(later)
    result = subprocess.run(["git", "-C", str(repo), "merge-base", "--is-ancestor", earlier, later], capture_output=True)
    require(result.returncode in (0, 1), "Commit ancestry unavailable; fetch required commits first")
    return result.returncode == 0


def gate(bundle, source, repo):
    sha(source)
    require(isinstance(bundle, dict), "Plan must be an object")
    require(type(bundle.get("schemaVersion")) is int and bundle["schemaVersion"] == 1, "Unsupported plan schema")
    require(bundle.get("releaseSource") == source, "Release plan belongs to another source")
    require(nonempty(bundle.get("repository")), "Missing repository")
    require(nonempty(bundle.get("scopeEvidence")), "Missing evidence that task list covers release scope")
    expected = bundle.get("requiredTaskIds")
    require(isinstance(expected, list) and bool(expected) and all(isinstance(x, str) for x in expected), "Missing required task IDs")
    require(len(set(expected)) == len(expected), "Duplicate required task ID")
    tasks = bundle.get("tasks")
    require(isinstance(tasks, list), "Missing task list")
    ids = [validate_task(t)["taskId"] for t in tasks]
    require(len(set(ids)) == len(ids) and set(ids) == set(expected), "Task coverage mismatch")
    receipts = bundle.get("receipts")
    require(isinstance(receipts, list), "Missing receipts")
    by_key = {}
    for receipt in receipts:
        require(isinstance(receipt, dict), "Receipt must be an object")
        key = (receipt.get("taskId"), receipt.get("platform"))
        require(key not in by_key and key[0] in expected and key[1] in PLATFORMS, "Duplicate or out-of-scope receipt")
        by_key[key] = receipt
    for task in tasks:
        require(task["repository"] == bundle["repository"], "Task repository mismatch")
        require(ancestor(repo, task["baseCommit"], task["sourceCommit"]), "Task range is divergent; review force-push history before release")
        require(ancestor(repo, task["sourceCommit"], source), "Source change is absent from release")
        for platform in PLATFORMS:
            require((task["taskId"], platform) in by_key, "Missing " + platform + " receipt")
            receipt = validate_receipt(task, by_key[(task["taskId"], platform)])
            require(ancestor(repo, receipt["implementationCommit"], source), "Platform implementation is absent from release")
            verification = receipt.get("verification", {})
            if receipt["status"] != "not-applicable" and verification["result"] == "reused":
                require(ancestor(repo, verification["commit"], receipt["implementationCommit"]), "Reused native evidence is not from implementation history")
    return {"status": "ready", "releaseSource": source, "taskCount": len(tasks), "publishPerformed": False}


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    task = commands.add_parser("task")
    for field in ("repository", "branch", "base", "head", "summary"):
        task.add_argument("--" + field, required=True)
    receipt = commands.add_parser("receipt")
    receipt.add_argument("task"); receipt.add_argument("receipt")
    preflight = commands.add_parser("gate")
    preflight.add_argument("plan"); preflight.add_argument("--source", required=True)
    preflight.add_argument("--repo", default=".")
    args = parser.parse_args()
    try:
        if args.command == "task":
            result = new_task(args.repository, args.branch, args.base, args.head, args.summary)
        elif args.command == "receipt":
            result = validate_receipt(read(args.task), read(args.receipt))
        else:
            result = gate(read(args.plan), args.source, args.repo)
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(1, "Coordination blocked: " + str(error) + "\n")


if __name__ == "__main__":
    main()
