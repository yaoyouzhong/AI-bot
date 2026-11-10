"""Freeze once, record hash-bound acceptance, and promote accepted bytes.

No command builds a product. Mutating GitHub operations require --execute;
--publish adds public publication to the same authorized operation.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys

from artifact_names import COMPONENT_KEYS, UPDATE_ALIASES, name as artifact_name, names, resolve
from component_versions import ROOT, components, versions
from github_cli import run_cli
from prepare_release_assets import prepare
from upload_release_resumable import upload


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".partial")
    temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def safe_name(value):
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]*", value):
        raise ValueError("Unsafe file name")
    return value


def sha(value):
    if not re.fullmatch(r"[0-9a-f]{40}", value):
        raise ValueError("Expected full source SHA")
    return value


def file_record(path):
    if path.is_symlink() or not path.is_file():
        raise ValueError("Expected a regular candidate file: " + path.name)
    return {"bytes": path.stat().st_size, "sha256": digest(path)}


def canonical_id(data):
    return hashlib.sha256(json.dumps(data, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()).hexdigest()


def stamp(directory, source, platform):
    """Run after successful native packaging; bind each artifact to its checkout."""
    sha(source)
    directory = Path(directory)
    files = {p.name: file_record(p) for p in sorted(directory.iterdir()) if not p.name.startswith("BUILD-SOURCE-")}
    if not files:
        raise ValueError("No native build artifacts")
    write(directory / f"BUILD-SOURCE-{safe_name(platform)}.json", {"sourceCommit": source, "platform": platform, "files": files})


def selected_keys(component, platform):
    if component not in ("bridge", "esp8266", "tab5") or platform not in ("all", "windows", "macos"):
        raise ValueError("Unsupported component or platform")
    if component != "bridge" and platform != "all":
        raise ValueError("Platform selection applies to the desktop bridge only")
    keys = COMPONENT_KEYS[component]
    if component == "bridge" and platform == "windows":
        keys = tuple(k for k in keys if k != "macos")
    if component == "bridge" and platform == "macos":
        keys = ("macos", "bridge-source")
    return keys


def assemble(raw, output, component, platform, source, *, require_stamps=False, tab5_source=None, notes=None):
    """Assemble native packages, including separately built desktop platforms."""
    raw, output = Path(raw), Path(output)
    sha(source)
    current = versions()
    selected = [resolve(raw, key, current[component]) for key in selected_keys(component, platform)]
    selected = [n + suffix for n in selected for suffix in ("", ".sha256")]
    if require_stamps:
        stamps = [read(p) for p in raw.glob("BUILD-SOURCE-*.json")]
        if not stamps or any(s["sourceCommit"] != source for s in stamps):
            raise ValueError("Native build source SHAs disagree")
        for name in selected:
            records = [s["files"][name] for s in stamps if name in s["files"]]
            if not records or any(record != file_record(raw / name) for record in records):
                raise ValueError("Native build provenance mismatch: " + name)
    if output.exists():
        candidate = verify(output, expected_source=source)
        if candidate['component'] != component or candidate['desktopPlatform'] != platform:
            raise ValueError("Existing candidate scope differs")
        if any(file_record(raw / name) != candidate['files'].get(name) for name in selected):
            raise ValueError("Existing candidate differs from native packages")
        return seal(output, notes=notes, tab5_source=tab5_source)
    output.mkdir(parents=True)
    for name in selected:
        file_record(raw / name)
        shutil.copyfile(raw / name, output / name)
    prepare(output, current[component], source, component=component, versions=current,
            desktop_platform=platform, tab5_source=tab5_source)
    return seal(output, notes=notes, tab5_source=tab5_source)


def seal(directory, *, notes=None, tab5_source=None):
    directory = Path(directory)
    if (directory / "CANDIDATE.json").exists():
        candidate = verify(directory)
        if notes and digest(notes) != digest(directory / "RELEASE-NOTES.md"):
            raise ValueError("Frozen candidate notes differ; use a new candidate directory")
        return candidate
    build = read(directory / "BUILD.json")
    component, platform = build["component"], build["desktopPlatform"]
    keys = selected_keys(component, platform)
    expected = {resolve(directory, key, build["version"]) for key in keys}
    if expected != set(build["packageHashes"]):
        raise ValueError("Build scope does not match its package list")
    expected_files = {n + suffix for n in expected for suffix in ("", ".sha256")} | {"BUILD.json", "LICENSE", "THIRD_PARTY_NOTICES.md", "SHA256SUMS.txt"}
    if {p.name for p in directory.iterdir()} != expected_files:
        raise ValueError("Unexpected files in validated candidate directory")
    for name, expected_hash in build["packageHashes"].items():
        if digest(directory / name) != expected_hash or (directory / (name + ".sha256")).read_text().strip() != f"{expected_hash}  {name}":
            raise ValueError("Validated package changed: " + name)
    if component == "tab5":
        record = build["tab5Source"]
        if not tab5_source or Path(tab5_source).name != record["file"] or digest(tab5_source) != record["sha256"]:
            raise ValueError("Matching TAB5 corresponding source is required")
        shutil.copyfile(tab5_source, directory / safe_name(record["file"]))
    target_notes = directory / "RELEASE-NOTES.md"
    if notes:
        shutil.copyfile(notes, target_notes)
    else:
        if versions()[component] != build["version"]:
            raise ValueError("Provide notes for this older candidate version")
        tag = components()[component]["tagPrefix"] + build["version"]
        subprocess.run([sys.executable, str(ROOT / "scripts/extract_release_notes.py"), tag, str(target_notes.resolve()), "--require-final"], cwd=ROOT, check=True)
    if not target_notes.read_text(encoding="utf-8-sig").strip():
        raise ValueError("Release notes must not be empty")
    checks = ["release-review"]
    checks += [key + "-install-startup" for key in ("windows", "macos") if key in keys]
    if component != "bridge":
        checks.append(component + "-device")
    data = {"schemaVersion": 1, "sourceCommit": sha(build["sourceCommit"]), "component": component,
            "version": build["version"], "desktopPlatform": platform,
            "tag": components()[component]["tagPrefix"] + build["version"],
            "requiredChecks": checks,
            "files": {p.name: file_record(p) for p in sorted(directory.iterdir())}}
    data["candidateId"] = canonical_id(data)
    write(directory / "CANDIDATE.json", data)
    return data


def verify(directory, *, expected_source=None):
    directory = Path(directory)
    candidate = read(directory / "CANDIDATE.json")
    core = {k: v for k, v in candidate.items() if k != "candidateId"}
    if candidate.get("schemaVersion") != 1 or candidate["candidateId"] != canonical_id(core):
        raise ValueError("Candidate manifest identity mismatch")
    sha(candidate["sourceCommit"])
    if expected_source and candidate["sourceCommit"] != expected_source:
        raise ValueError("Candidate differs from the successful native workflow source")
    if {p.name for p in directory.iterdir()} != set(candidate["files"]) | {"CANDIDATE.json"}:
        raise ValueError("Candidate inventory changed")
    for name, expected in candidate["files"].items():
        safe_name(name)
        if file_record(directory / name) != expected:
            raise ValueError("Candidate bytes changed: " + name)
    build = read(directory / "BUILD.json")
    keys = selected_keys(candidate["component"], candidate["desktopPlatform"])
    wanted = ["release-review"] + [k + "-install-startup" for k in ("windows", "macos") if k in keys]
    if candidate["component"] != "bridge":
        wanted.append(candidate["component"] + "-device")
    if (candidate["requiredChecks"] != wanted or build["sourceCommit"] != candidate["sourceCommit"]
            or build["version"] != candidate["version"] or build["component"] != candidate["component"]
            or build["desktopPlatform"] != candidate["desktopPlatform"]
            or build["componentVersions"][candidate["component"]] != candidate["version"]
            or candidate["tag"] != components()[candidate["component"]]["tagPrefix"] + candidate["version"]):
        raise ValueError("Candidate scope/acceptance requirements disagree")
    return candidate


def record(directory, acceptance, check, result, evidence, *, baseline=None, reason=None):
    candidate = verify(directory)
    if check not in candidate["requiredChecks"] or result not in ("passed", "reused", "failed", "deferred") or not evidence.strip():
        raise ValueError("Record a required check with concrete evidence")
    if result == "deferred" and (not reason or not reason.strip()):
        raise ValueError("Deferred acceptance requires explicit authorization and reason")
    if result == "reused" and (not baseline or not reason or not reason.strip()):
        raise ValueError("Reused evidence needs a baseline source and change-scope reason")
    if baseline:
        sha(baseline)
    path = Path(acceptance)
    data = read(path) if path.exists() else {"schemaVersion": 1, "candidateId": candidate["candidateId"], "sourceCommit": candidate["sourceCommit"], "checks": {}}
    if data.get("candidateId") != candidate["candidateId"] or data.get("sourceCommit") != candidate["sourceCommit"]:
        raise ValueError("Acceptance belongs to a different candidate")
    data["checks"][check] = {"result": result, "evidence": evidence, "baselineSource": baseline, "reason": reason}
    from check_public_content import findings
    if list(findings("release-acceptance.json", json.dumps(data).encode())):
        raise ValueError("Acceptance contains disallowed public content")
    write(path, data)
    return data


def readiness(directory, acceptance, *, expected_source=None):
    candidate = verify(directory, expected_source=expected_source)
    data = read(acceptance)
    if data.get("schemaVersion") != 1 or data.get("candidateId") != candidate["candidateId"] or data.get("sourceCommit") != candidate["sourceCommit"]:
        raise ValueError("Acceptance is stale or belongs to a different candidate")
    if set(data.get("checks", {})) - set(candidate["requiredChecks"]):
        raise ValueError("Acceptance contains checks outside this scope")
    pending = []
    for check in candidate["requiredChecks"]:
        item = data["checks"].get(check, {})
        result = item.get("result")
        if result not in ("passed", "reused") or not item.get("evidence", "").strip():
            pending.append(check)
        if result == "reused":
            if not item.get("reason", "").strip():
                raise ValueError("Reused evidence has no scope justification")
            sha(item.get("baselineSource", ""))
        if result == "deferred" and (not item.get("reason", "").strip() or not item.get("evidence", "").strip()):
            raise ValueError("Deferred acceptance requires authorization evidence and reason")
    from check_public_content import findings
    if list(findings("release-acceptance.json", json.dumps(data).encode())):
        raise ValueError("Acceptance contains disallowed public content")
    return candidate, data, pending


def stage(directory, acceptance, output, *, allow_deferred=False):
    candidate, data, pending = readiness(directory, acceptance)
    if pending and not (allow_deferred and all(data['checks'].get(c, {}).get('result') == 'deferred' and data['checks'][c].get('reason', '').strip() for c in pending)):
        raise ValueError("Pending or failed acceptance: " + ", ".join(pending))
    directory, output = Path(directory), Path(output)
    build = read(directory / "BUILD.json")
    wanted = {n: n for n in ("LICENSE", "THIRD_PARTY_NOTICES.md")}
    for key in ("windows", "macos", "esp8266", "tab5-first", "tab5-upgrade"):
        found = [n for n in names(key, candidate["version"]) if n in build["packageHashes"]]
        if found:
            wanted[artifact_name(key, candidate["version"])] = found[0]
            if key in UPDATE_ALIASES:
                wanted[names(key, candidate["version"])[1]] = found[0]
    if build.get("tab5Source"):
        name = build["tab5Source"]["file"]
        wanted[name] = name
    output.mkdir(parents=True, exist_ok=True)
    for target, source in wanted.items():
        dest = output / safe_name(target)
        if dest.exists() and file_record(dest) != file_record(directory / source):
            raise ValueError("Existing publication payload differs")
        if not dest.exists():
            shutil.copyfile(directory / source, dest)
        if file_record(dest) != candidate['files'][source]:
            raise ValueError('Copied publication asset differs from accepted bytes: ' + target)
    write(output / "CANDIDATE.json", candidate)
    if not pending:
        write(output / "ACCEPTANCE.json", data)
    expected = set(wanted) | {"CANDIDATE.json", "SHA256SUMS.txt"} | (set() if pending else {"ACCEPTANCE.json"})
    if {p.name for p in output.iterdir()} - expected:
        raise ValueError("Unexpected publication payload files")
    checksums = "".join(f"{digest(p)}  {p.name}\n" for p in sorted(output.iterdir()) if p.name != "SHA256SUMS.txt")
    (output / "SHA256SUMS.txt").write_text(checksums, encoding="ascii")
    return {p.name: file_record(p) for p in output.iterdir()}


def gh_json(args):
    return json.loads(run_cli(args, timeout=120).stdout)


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def ensure_tag(repository, candidate, remote="origin"):
    url = git("remote", "get-url", remote).removesuffix(".git")
    if url not in (f"https://github.com/{repository}", f"git@github.com:{repository}"):
        raise ValueError("Git remote and requested repository disagree")
    tag, source = candidate["tag"], candidate["sourceCommit"]
    remote_tags = git("ls-remote", remote, "refs/tags/" + tag, "refs/tags/" + tag + "^{}")
    if remote_tags:
        peeled = [line.split()[0] for line in remote_tags.splitlines() if line.endswith("^{}")]
        if peeled != [source]:
            raise ValueError("Remote tag must be annotated and point to the accepted source")
        return
    # Tag events read workflow definitions at the tagged source, not today's
    # main. An older candidate must not silently trigger the old rebuild flow.
    workflow = git('show', source + ':.github/workflows/release.yml')
    if '# aibot-tag-validation-only: 1' not in workflow:
        raise ValueError('Candidate source predates metadata-only tags; stop to avoid the historical tag-triggered rebuild')
    if git("tag", "--list", tag):
        if git("cat-file", "-t", "refs/tags/" + tag) != "tag" or git("rev-parse", tag + "^{}") != source:
            raise ValueError("Local tag does not match accepted source")
    else:
        subprocess.run(["git", "tag", "-a", tag, source, "-m", "Promote accepted candidate " + candidate["candidateId"]], cwd=ROOT, check=True)
    subprocess.run(["git", "push", remote, "refs/tags/" + tag], cwd=ROOT, check=True)


def remote_identity(repository, tag):
    # Drafts cannot reliably be fetched through GET releases/tags/:tag.
    view = gh_json(["release", "view", tag, "--repo", repository, "--json", "databaseId,tagName,isDraft"])
    value = gh_json(["api", f"repos/{repository}/releases/{view['databaseId']}"])
    if value["tag_name"] != tag:
        raise ValueError("Remote release identity mismatch")
    return value


def verify_remote(release, manifest):
    assets = {asset["name"]: asset for asset in release["assets"]}
    if set(assets) != set(manifest):
        raise ValueError("Remote asset inventory mismatch")
    for name, item in manifest.items():
        if assets[name]["size"] != item["bytes"] or assets[name].get("digest") != "sha256:" + item["sha256"]:
            raise ValueError("Remote digest mismatch: " + name)


def promote(directory, acceptance, repository, *, execute=False, publish=False, expected_source=None, commit_closeout=False, run_closeout=True, allow_deferred=False):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository):
        raise ValueError("Invalid GitHub repository")
    candidate, data, pending = readiness(directory, acceptance, expected_source=expected_source)
    if pending and not (allow_deferred and all(data['checks'].get(c, {}).get('result') == 'deferred' and data['checks'][c].get('reason', '').strip() for c in pending)):
        raise ValueError("Pending or failed acceptance: " + ", ".join(pending))
    if commit_closeout and not publish:
        raise ValueError("Committing closeout requires public publication")
    report = {"candidateId": candidate["candidateId"], "tag": candidate["tag"], "sourceCommit": candidate["sourceCommit"], "repository": repository, "publish": publish, "execute": execute}
    if not execute:
        return report
    if commit_closeout:
        check_closeout_workspace()
    directory = Path(directory)
    payload = directory.parent / ("publication-" + candidate["candidateId"][:16])
    manifest = stage(directory, acceptance, payload, allow_deferred=allow_deferred)
    ensure_tag(repository, candidate)
    existing = run_cli(["release", "view", candidate["tag"], "--repo", repository, "--json", "databaseId"], check=False, timeout=90)
    if existing.returncode:
        # Distinguish a missing release from auth/network failures before mutation.
        releases = gh_json(["api", f"repos/{repository}/releases?per_page=100"])
        if any(x["tag_name"] == candidate["tag"] for x in releases):
            raise ValueError("Existing release lookup failed")
        run_cli(["release", "create", candidate["tag"], "--repo", repository, "--verify-tag", "--draft", "--latest=false", "--title", ("AI-bot " if candidate["component"] == "bridge" else "") + candidate["tag"], "--notes-file", str((directory / "RELEASE-NOTES.md").resolve())], timeout=120)
    release = remote_identity(repository, candidate["tag"])
    if release["draft"]:
        unexpected = {a["name"] for a in release["assets"]} - set(manifest)
        if unexpected:
            raise ValueError("Draft contains assets outside this accepted candidate")
        upload(repository, candidate["tag"], payload, manifest, payload.parent / (payload.name + "-upload-state.json"), workers=2, require_draft=True)
        # The resumable upload journal is not a public payload asset.
        release = remote_identity(repository, candidate["tag"])
    verify_remote(release, manifest)
    if publish and release["draft"]:
        run_cli(["release", "edit", candidate["tag"], "--repo", repository, "--draft=false", "--latest=" + ("true" if candidate["component"] == "bridge" else "false")], timeout=120)
        release = remote_identity(repository, candidate["tag"])
        if release["draft"]:
            raise ValueError("Release remained a draft")
    report.update(url=release["html_url"], published=not release["draft"], verifiedAssets=len(manifest))
    write(directory.parent / ("promotion-" + candidate["candidateId"][:16] + ".json"), report)
    if publish and run_closeout:
        try:
            report["closeout"] = closeout(repository=repository, tags=[candidate["tag"]], commit=commit_closeout)
        except Exception as error:
            report["closeout"] = {"completed": False, "errorClass": type(error).__name__, "retry": "Repeat promotion; accepted packages will not be rebuilt or replaced"}
            write(directory.parent / ("promotion-" + candidate["candidateId"][:16] + ".json"), report)
            raise
        write(directory.parent / ("promotion-" + candidate["candidateId"][:16] + ".json"), report)
    return report


def promote_set(plan, *, execute=False, publish=False, commit_closeout=False, allow_deferred=False):
    """Preflight every selected component, publish original bytes, close out once."""
    plan = Path(plan).resolve()
    data = read(plan)
    if data.get('schemaVersion') != 1 or not data.get('candidates'):
        raise ValueError('Expected a nonempty release-set plan')
    entries, preflight = [], []
    for item in data['candidates']:
        directory = (plan.parent / item['directory']).resolve()
        acceptance = (plan.parent / item['acceptance']).resolve()
        args = dict(directory=directory, acceptance=acceptance, repository=data['repository'], expected_source=item.get('sourceCommit'), allow_deferred=allow_deferred)
        preflight.append(promote(**args, publish=publish, commit_closeout=commit_closeout))
        entries.append(args)
    tags = [item['tag'] for item in preflight]
    if len(tags) != len(set(tags)):
        raise ValueError('Release-set contains duplicate tags')
    result = {'execute': execute, 'publish': publish, 'components': preflight}
    if not execute:
        return result
    if commit_closeout:
        check_closeout_workspace()
    state = plan.parent / ('release-set-' + canonical_id(preflight)[:16] + '.json')
    result['components'] = []
    try:
        for args in entries:
            result['components'].append(promote(**args, execute=True, publish=publish, run_closeout=False))
            write(state, result)
        if publish:
            result['closeout'] = closeout(repository=data['repository'], tags=tags, commit=commit_closeout)
        result['completed'] = True
        write(state, result)
    except Exception as error:
        result.update(completed=False, errorClass=type(error).__name__, retry='Repeat this release-set command; published bytes are verified without replacement')
        write(state, result)
        raise
    return result


GENERATED_DOWNLOADS = ["download-catalog.json", "DOWNLOADS.md", "DOWNLOADS.en.md", "README.md", "README.en.md"]


def check_closeout_workspace():
    if git("branch", "--show-current") != "main" or git("diff", "--cached", "--name-only"):
        raise ValueError("Automatic closeout commit requires main with an empty index")
    if git("status", "--porcelain", "--", *GENERATED_DOWNLOADS):
        raise ValueError("Preserve existing download/homepage edits before automatic closeout commit")
    behind, ahead = map(int, git("rev-list", "--left-right", "--count", "origin/main...HEAD").split())
    if behind:
        raise ValueError("Update main before automatic closeout; no automatic rebase")
    if ahead:
        # Permit retry after a failed push, but never push other local work.
        for commit in git("rev-list", "origin/main..HEAD").splitlines():
            changed = set(git("diff-tree", "--no-commit-id", "--name-only", "-r", commit).splitlines())
            if (not changed or changed - set(GENERATED_DOWNLOADS)
                    or git("show", "-s", "--format=%s", commit) != "Sync published download metadata"):
                raise ValueError("Automatic closeout would push unrelated local commits")


def closeout(*, repository=None, tags=(), commit=False):
    # No builds, flashes, or media re-encoding. Git writes require explicit opt-in.
    generated = GENERATED_DOWNLOADS
    if commit:
        check_closeout_workspace()
    for step in ("refresh", "sync", "check"):
        subprocess.run([sys.executable, str(ROOT / "scripts/download_catalog.py"), step], cwd=ROOT, check=True)
    if tags:
        if not repository:
            raise ValueError("Repository required to refresh Release download tables")
        from component_versions import release_target
        from download_catalog import release_intro
        for tag in tags:
            _, version = release_target(tag)
            release = gh_json(["release", "view", tag, "--repo", repository, "--json", "body,isDraft"])
            if release["isDraft"]:
                raise ValueError("Cannot close out an unpublished release")
            body = release["body"]
            header = re.search(r"^## " + re.escape(version) + r" - \d{4}-\d{2}-\d{2}", body, re.M)
            if not header:
                raise ValueError("Custom Release body needs an explicit editorial download-table update")
            updated = release_intro(tag) + body[header.start():]
            if updated != body:
                import tempfile
                with tempfile.TemporaryDirectory(prefix="aibot-release-notes-") as temp:
                    notes = Path(temp) / "notes.md"
                    notes.write_text(updated, encoding="utf-8")
                    run_cli(["release", "edit", tag, "--repo", repository, "--notes-file", str(notes)], timeout=120)
    committed = False
    if commit and git("diff", "--name-only", "--", *generated):
        subprocess.run(["git", "add", "--", *generated], cwd=ROOT, check=True)
        subprocess.run(["git", "diff", "--cached", "--check"], cwd=ROOT, check=True)
        subprocess.run(["git", "commit", "-m", "Sync published download metadata"], cwd=ROOT, check=True)
        committed = True
    if commit and git("rev-list", "origin/main..HEAD"):
        subprocess.run(["git", "push", "origin", "HEAD:refs/heads/main"], cwd=ROOT, check=True)
        committed = True
    return {"downloads": "updated and verified", "downloadMetadataCommitted": committed,
            "remainingEditorialReview": ["affected release highlights and installation prose", "media reuse or changed scenes"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("stamp")
    p.add_argument("directory", type=Path); p.add_argument("--source", required=True); p.add_argument("--platform", required=True)
    p = sub.add_parser("assemble")
    p.add_argument("raw", type=Path); p.add_argument("output", type=Path); p.add_argument("--source", required=True)
    p.add_argument("--component", required=True, choices=("bridge", "esp8266", "tab5")); p.add_argument("--platform", choices=("all", "windows", "macos"), default="all")
    p.add_argument("--require-stamps", action="store_true"); p.add_argument("--tab5-source", type=Path); p.add_argument("--notes", type=Path)
    p = sub.add_parser("seal")
    p.add_argument("directory", type=Path); p.add_argument("--notes", type=Path); p.add_argument("--tab5-source", type=Path)
    p = sub.add_parser("record")
    p.add_argument("directory", type=Path); p.add_argument("acceptance", type=Path); p.add_argument("--check", required=True)
    p.add_argument("--result", required=True, choices=("passed", "reused", "failed", "deferred")); p.add_argument("--evidence", required=True)
    p.add_argument("--baseline"); p.add_argument("--reason")
    for command in ("status", "promote"):
        p = sub.add_parser(command)
        p.add_argument("directory", type=Path); p.add_argument("acceptance", type=Path); p.add_argument("--expected-source")
        if command == "promote":
            p.add_argument("--repo", required=True); p.add_argument("--execute", action="store_true"); p.add_argument("--publish", action="store_true"); p.add_argument("--commit-closeout", action="store_true"); p.add_argument("--allow-deferred", action="store_true")
    p = sub.add_parser("closeout"); p.add_argument("--execute", action="store_true"); p.add_argument("--repo"); p.add_argument("--tag", action="append", default=[]); p.add_argument("--commit", action="store_true")
    p = sub.add_parser("promote-set"); p.add_argument("plan", type=Path)
    p.add_argument("--execute", action="store_true"); p.add_argument("--publish", action="store_true"); p.add_argument("--commit-closeout", action="store_true"); p.add_argument("--allow-deferred", action="store_true")
    args = vars(parser.parse_args()); command = args.pop("command")
    if command == "stamp": result = stamp(**args)
    elif command == "assemble": result = assemble(**args)
    elif command == "seal": result = seal(**args)
    elif command == "record": result = record(**args)
    elif command == "status":
        candidate, _, pending = readiness(**args)
        result = {"candidateId": candidate["candidateId"], "ready": not pending, "pending": pending}
    elif command == "promote":
        args["repository"] = args.pop("repo"); result = promote(**args)
    elif command == "promote-set": result = promote_set(**args)
    else:
        result = closeout(repository=args["repo"], tags=args["tag"], commit=args["commit"]) if args["execute"] else {"wouldRun": ["download_catalog.py refresh", "download_catalog.py sync", "download_catalog.py check"], "builds": False, "commit": args["commit"]}
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        # External command errors may contain signed URLs or auth headers.
        print("RELEASE_PIPELINE_FAILED: " + (str(error) if isinstance(error, ValueError) else type(error).__name__), file=sys.stderr)
        raise SystemExit(1)
