"""Exercise immutable candidates, reused acceptance, and idempotent promotion.

Uses tiny fixture artifacts and a simulated GitHub; never builds, tags, uploads,
installs, flashes, changes user settings, or modifies a real Release.
"""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import tempfile
import subprocess
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import zipfile

from artifact_names import names
from ci_scope import classify
from component_versions import versions
from prepare_release_assets import prepare
import release_pipeline as pipeline
from upload_release_resumable import upload

SOURCE = "a" * 40


class CandidateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def seed(self, platform="windows", suffix="", *, component="bridge"):
        folder = self.root / (component + platform + suffix)
        folder.mkdir()
        current = versions()
        source_zip = None
        keys = pipeline.selected_keys(component, platform)
        if component == "tab5":
            version = current[component]
            image = b'\0' * 48 + version.encode().ljust(32, b'\0')
            hashed = hashlib.sha256(image).hexdigest()
            with zipfile.ZipFile(folder / names("tab5-upgrade", version)[0], "w") as z:
                z.writestr("aibot_tab5.bin", image)
                z.writestr("aibot_tab5.bin.notes.json", json.dumps({"version": version, "sha256": hashed}))
            with zipfile.ZipFile(folder / names("tab5-first", version)[0], "w") as z:
                z.writestr("factory.bin", image)
                z.writestr("manifest.json", json.dumps({"version": version, "imageSha256": hashed, "segments": [{"name": "application.bin", "offset": 0, "size": len(image), "sha256": hashed}]}))
            source_zip = self.root / names("tab5-source", version)[0]
            with zipfile.ZipFile(source_zip, "w") as z:
                z.writestr("SOURCE-MANIFEST.json", json.dumps({"version": version, "applicationSha256": hashed, "sourceCommit": None}))
        else:
            for key in keys:
                (folder / names(key, current[component])[0]).write_bytes((key + suffix).encode())
        for artifact in list(folder.iterdir()):
            (folder / (artifact.name + ".sha256")).write_text(f"{pipeline.digest(artifact)}  {artifact.name}\n")
        prepare(folder, current[component], SOURCE, component=component, versions=current, desktop_platform=platform, tab5_source=source_zip)
        note = self.root / ("notes" + suffix + ".md")
        note.write_text(f"## {current[component]} - 2026-11-03\n\nFixture release details.\n")
        return folder, note, source_zip

    def candidate(self, platform="windows", suffix="", component="bridge"):
        folder, note, source_zip = self.seed(platform, suffix, component=component)
        candidate = pipeline.seal(folder, notes=note, tab5_source=source_zip)
        return folder, candidate

    def accepted(self, folder, candidate, suffix=""):
        evidence = self.root / ("acceptance" + suffix + ".json")
        for check in candidate["requiredChecks"]:
            pipeline.record(folder, evidence, check, "passed", "Fixture observed acceptance result")
        return evidence

    def test_same_candidate_is_sealed_once(self):
        folder, candidate = self.candidate()
        timestamp = (folder / "CANDIDATE.json").stat().st_mtime_ns
        self.assertEqual(candidate, pipeline.seal(folder))
        self.assertEqual(timestamp, (folder / "CANDIDATE.json").stat().st_mtime_ns)

    def test_changed_package_invalidates_old_acceptance(self):
        folder, candidate = self.candidate()
        acceptance = self.accepted(folder, candidate)
        artifact = next(folder.glob("*.exe"))
        artifact.write_bytes(b"changed after acceptance")
        with self.assertRaisesRegex(ValueError, "bytes changed"):
            pipeline.readiness(folder, acceptance)

    def test_other_builds_acceptance_cannot_be_reused_silently(self):
        folder, candidate = self.candidate()
        acceptance = self.accepted(folder, candidate)
        other, _ = self.candidate(suffix="other")
        with self.assertRaisesRegex(ValueError, "different candidate"):
            pipeline.readiness(other, acceptance)

    def test_missing_platform_or_failed_check_blocks_publication(self):
        folder, candidate = self.candidate(platform="all")
        acceptance = self.root / "acceptance.json"
        for check in ("release-review", "windows-install-startup"):
            pipeline.record(folder, acceptance, check, "passed", "Real check label")
        self.assertEqual(["macos-install-startup"], pipeline.readiness(folder, acceptance)[2])
        pipeline.record(folder, acceptance, "macos-install-startup", "failed", "Observed failure")
        with self.assertRaisesRegex(ValueError, "Pending or failed"):
            pipeline.stage(folder, acceptance, self.root / "publication")

    def test_unchanged_paths_can_reuse_explicit_baseline_without_retests(self):
        folder, candidate = self.candidate()
        acceptance = self.root / "acceptance.json"
        with self.assertRaisesRegex(ValueError, "baseline"):
            pipeline.record(folder, acceptance, "windows-install-startup", "reused", "Previous package acceptance")
        pipeline.record(folder, acceptance, "release-review", "passed", "Scope and unchanged media reviewed")
        pipeline.record(folder, acceptance, "windows-install-startup", "reused", "Previous native installation record", baseline="b"*40, reason="Installer, runtime dependencies and storage formats unchanged; current package startup observed")
        self.assertFalse(pipeline.readiness(folder, acceptance)[2])

    def test_unexpected_or_linked_inputs_are_not_copied(self):
        folder, note, _ = self.seed()
        (folder / "private.log").write_text("should never be published")
        with self.assertRaisesRegex(ValueError, "Unexpected"):
            pipeline.seal(folder, notes=note)

    def test_workflow_run_source_must_match_manifest(self):
        folder, candidate = self.candidate()
        acceptance = self.accepted(folder, candidate)
        with self.assertRaisesRegex(ValueError, "workflow source"):
            pipeline.readiness(folder, acceptance, expected_source="c"*40)

    def test_stage_keeps_original_bytes_aliases_and_tab5_source(self):
        for component in ("bridge", "tab5"):
            folder, candidate = self.candidate(component=component, platform="windows" if component == "bridge" else "all")
            acceptance = self.accepted(folder, candidate, suffix=component)
            output = self.root / (component + "-publication")
            first = pipeline.stage(folder, acceptance, output)
            self.assertEqual(first, pipeline.stage(folder, acceptance, output))
            self.assertIn("ACCEPTANCE.json", first)
            self.assertIn("CANDIDATE.json", first)
            key = "windows" if component == "bridge" else "tab5-upgrade"
            canonical, alias = names(key, candidate["version"])[:2]
            self.assertEqual((output / canonical).read_bytes(), (output / alias).read_bytes())
            if component == "tab5":
                self.assertIn(names("tab5-source", candidate["version"])[0], first)

    def test_dry_run_does_not_call_git_github_or_builds(self):
        folder, candidate = self.candidate()
        acceptance = self.accepted(folder, candidate)
        with patch.object(pipeline, "run_cli") as cli, patch.object(pipeline, "ensure_tag") as tag, patch.object(pipeline, "upload") as send, patch.object(pipeline.subprocess, "run") as process:
            report = pipeline.promote(folder, acceptance, "owner/repo", publish=True)
            self.assertFalse(report["execute"])
            for mock in (cli, tag, send, process): mock.assert_not_called()

    def test_promotion_retries_do_not_rebuild_reupload_or_republish(self):
        folder, candidate = self.candidate()
        acceptance = self.accepted(folder, candidate)
        release = {"id": 123, "tag_name": candidate["tag"], "draft": True, "html_url": "https://github.com/owner/repo/releases/tag/" + candidate["tag"], "assets": []}
        calls = {"upload": 0, "publish": 0}
        def fake_json(args):
            if args[0] == "api": return deepcopy(release)
            return {"databaseId": 123, "tagName": candidate["tag"], "isDraft": release["draft"]}
        def fake_cli(args, **kwargs):
            if args[:2] == ["release", "edit"]:
                calls["publish"] += 1
                release["draft"] = False
            return SimpleNamespace(returncode=0, stdout=b'{}')
        def fake_upload(repo, tag, payload, manifest, state, **kwargs):
            self.assertTrue(kwargs["require_draft"])
            self.assertNotEqual(state.parent, payload)
            calls["upload"] += 1
            release["assets"] = [{"name": n, "size": m["bytes"], "digest": "sha256:"+m["sha256"]} for n,m in manifest.items()]
        with patch.object(pipeline, "gh_json", fake_json), patch.object(pipeline, "run_cli", fake_cli), patch.object(pipeline, "ensure_tag"), patch.object(pipeline, "upload", fake_upload), patch.object(pipeline, "closeout", return_value={"downloads": "verified"}):
            first = pipeline.promote(folder, acceptance, "owner/repo", execute=True, publish=True)
            second = pipeline.promote(folder, acceptance, "owner/repo", execute=True, publish=True)
            self.assertTrue(first["published"] and second["published"])
            self.assertEqual({"upload": 1, "publish": 1}, calls)
            release["assets"][0]["digest"] = "sha256:" + "0"*64
            with self.assertRaisesRegex(ValueError, "digest mismatch"):
                pipeline.promote(folder, acceptance, "owner/repo", execute=True, publish=True)
            self.assertEqual({"upload": 1, "publish": 1}, calls)

    def test_native_sources_cannot_be_mixed(self):
        raw = self.root / "raw"; raw.mkdir()
        version = versions()["bridge"]
        for key in pipeline.selected_keys("bridge", "windows"):
            p = raw / names(key, version)[0]; p.write_bytes(key.encode())
            (raw/(p.name+".sha256")).write_text(f"{pipeline.digest(p)}  {p.name}\n")
        pipeline.stamp(raw, SOURCE, "windows")
        provenance = raw/"BUILD-SOURCE-windows.json"
        value = pipeline.read(provenance); value["sourceCommit"] = "b"*40; pipeline.write(provenance,value)
        with self.assertRaisesRegex(ValueError, "source SHAs"):
            pipeline.assemble(raw,self.root/"out","bridge","windows",SOURCE,require_stamps=True)

    def test_native_assembly_reuses_original_candidate_and_refuses_changed_input(self):
        raw, notes, _ = self.seed()
        pipeline.stamp(raw, SOURCE, 'windows')
        output = self.root/'sealed'
        first = pipeline.assemble(raw, output, 'bridge', 'windows', SOURCE, require_stamps=True, notes=notes)
        self.assertEqual(first, pipeline.assemble(raw, output, 'bridge', 'windows', SOURCE, require_stamps=True, notes=notes))
        next(raw.glob('*.exe')).write_bytes(b'changed native package')
        with self.assertRaisesRegex(ValueError, 'provenance mismatch'):
            pipeline.assemble(raw, output, 'bridge', 'windows', SOURCE, require_stamps=True, notes=notes)

    def test_real_cli_status_and_dry_run_need_no_network_or_build(self):
        folder, candidate = self.candidate()
        acceptance = self.accepted(folder, candidate)
        script = str(pipeline.ROOT/'scripts/release_pipeline.py')
        for command in (['status',str(folder),str(acceptance)],
                        ['promote',str(folder),str(acceptance),'--repo','owner/repo']):
            result = subprocess.run([sys.executable,script,*command],capture_output=True,text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            data = json.loads(result.stdout)
            if command[0] == 'status': self.assertTrue(data['ready'])
            else: self.assertFalse(data['execute'])

    def test_closeout_commit_never_stages_unrelated_work(self):
        with patch.object(pipeline, "git", side_effect=["main", "private.txt"]), patch.object(pipeline.subprocess,"run") as execute:
            with self.assertRaisesRegex(ValueError,"empty index"):
                pipeline.closeout(commit=True)
            execute.assert_not_called()

    def test_existing_tag_cannot_be_moved_or_lightweight(self):
        _, candidate = self.candidate()
        for remote in ('b'*40 + '\trefs/tags/' + candidate['tag'],
                       'b'*40 + '\trefs/tags/' + candidate['tag'] + '^{}'):
            with patch.object(pipeline, 'git', side_effect=['https://github.com/owner/repo.git', remote]), patch.object(pipeline.subprocess, 'run') as execute:
                with self.assertRaisesRegex(ValueError, 'annotated and point'):
                    pipeline.ensure_tag('owner/repo', candidate)
                execute.assert_not_called()

    def test_old_candidate_cannot_trigger_historical_tag_rebuild(self):
        _, candidate = self.candidate()
        with patch.object(pipeline, 'git', side_effect=['https://github.com/owner/repo.git', '', 'uses: ./.github/workflows/candidate.yml']), patch.object(pipeline.subprocess, 'run') as execute:
            with self.assertRaisesRegex(ValueError, 'historical tag-triggered rebuild'):
                pipeline.ensure_tag('owner/repo', candidate)
            execute.assert_not_called()

    def test_frozen_notes_cannot_be_silently_changed(self):
        folder, _ = self.candidate()
        replacement = self.root/'replacement.md'; replacement.write_text('Changed release claims')
        with self.assertRaisesRegex(ValueError, 'notes differ'):
            pipeline.seal(folder, notes=replacement)

    def test_copy_cannot_change_accepted_bytes_before_upload(self):
        folder, candidate = self.candidate()
        acceptance = self.accepted(folder, candidate)
        copy = pipeline.shutil.copyfile
        def changed_copy(source, target):
            result = copy(source, target)
            if Path(target).suffix == '.exe':
                Path(target).write_bytes(b'changed during copy')
            return result
        with patch.object(pipeline.shutil, 'copyfile', side_effect=changed_copy):
            with self.assertRaisesRegex(ValueError, 'differs from accepted bytes'):
                pipeline.stage(folder, acceptance, self.root/'publish')

    def test_batch_preflights_all_components_before_any_publication(self):
        folder, candidate = self.candidate()
        acceptance = self.accepted(folder, candidate)
        second, other = self.candidate('all', component='esp8266')
        incomplete = self.root/'incomplete.json'
        pipeline.record(second, incomplete, 'release-review', 'passed', 'Scope reviewed; device still pending')
        plan = self.root/'plan.json'
        pipeline.write(plan, {'schemaVersion': 1, 'repository': 'owner/repo', 'candidates': [
            {'directory': folder.name, 'acceptance': acceptance.name},
            {'directory': second.name, 'acceptance': incomplete.name}]})
        with patch.object(pipeline, 'ensure_tag') as tag, patch.object(pipeline, 'run_cli') as gh:
            with self.assertRaisesRegex(ValueError, 'Pending or failed'):
                pipeline.promote_set(plan, execute=True, publish=True)
            tag.assert_not_called(); gh.assert_not_called()

    def test_batch_closes_out_only_once_and_saves_partial_progress(self):
        plan = self.root/'plan.json'
        pipeline.write(plan, {'schemaVersion': 1, 'repository': 'owner/repo', 'candidates': [
            {'directory': 'first', 'acceptance': 'first.json'},
            {'directory': 'second', 'acceptance': 'second.json'}]})
        def fake_promote(**args):
            return {'tag': args['directory'].name, 'published': bool(args.get('execute')), 'execute': args.get('execute', False)}
        with patch.object(pipeline, 'promote', side_effect=fake_promote) as promote, patch.object(pipeline, 'closeout', return_value={'downloads': 'verified'}) as closeout:
            result = pipeline.promote_set(plan, execute=True, publish=True)
            self.assertTrue(result['completed'])
            self.assertEqual(4, promote.call_count)  # Two local preflights, two promotions.
            closeout.assert_called_once_with(repository='owner/repo', tags=['first','second'], commit=False)
        with patch.object(pipeline, 'promote', side_effect=[{'tag':'first'}, {'tag':'second'}, {'tag':'first','published':True}, ConnectionError()]), patch.object(pipeline, 'closeout') as closeout:
            with self.assertRaises(ConnectionError):
                pipeline.promote_set(plan, execute=True, publish=True)
            state = next(pipeline.read(p) for p in self.root.glob('release-set-*.json') if pipeline.read(p).get('errorClass'))
            self.assertFalse(state['completed'])
            self.assertEqual([{'tag':'first','published':True}], state['components'])
            closeout.assert_not_called()

    def test_closeout_does_not_push_unrelated_commits(self):
        answers = ['main', '', '', '0 1', 'a'*40, 'private.txt', 'Other work']
        with patch.object(pipeline, 'git', side_effect=answers), patch.object(pipeline.subprocess, 'run') as execute:
            with self.assertRaisesRegex(ValueError, 'unrelated local commits'):
                pipeline.closeout(commit=True)
            execute.assert_not_called()

    def test_closeout_retries_only_its_unpushed_metadata_commit(self):
        # Preflight recognizes its own pending commit; unchanged generated files
        # still need a push after the previous network attempt failed.
        answers = ['main', '', '', '0 1', 'a'*40, 'DOWNLOADS.md', 'Sync published download metadata', '', 'a'*40]
        with patch.object(pipeline, 'git', side_effect=answers), patch.object(pipeline.subprocess, 'run') as execute:
            self.assertTrue(pipeline.closeout(commit=True)['downloadMetadataCommitted'])
            commands = [call.args[0] for call in execute.call_args_list]
            self.assertEqual(['git','push','origin','HEAD:refs/heads/main'], commands[-1])
            self.assertFalse(any(command[:2] == ['git','add'] for command in commands))


class UploadAndScopeTests(unittest.TestCase):
    def test_no_write_if_draft_was_published_by_another_process(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); artifact=root/'payload.zip'; artifact.write_bytes(b'unchanged bytes')
            manifest={artifact.name:pipeline.file_record(artifact)}
            def cli(args,**kwargs):
                if args[0]=='api': return SimpleNamespace(stdout=json.dumps({'tag_name':'test','draft':False,'assets':[]}).encode())
                if args[:2]==['release','view']: return SimpleNamespace(stdout=json.dumps({'databaseId':1,'tagName':'test','isDraft':True}).encode())
                self.fail('Must not upload into a now-public Release')
            with patch('upload_release_resumable.run_cli',cli):
                with self.assertRaisesRegex(ValueError,'published release'):
                    upload('owner/repo','test',root,manifest,root/'state.json',require_draft=True)

    def test_docs_and_release_tools_do_not_compile_products(self):
        for paths in [['README.md','docs/RELEASE-CHECKLIST.md'],['scripts/release_pipeline.py','.github/workflows/promote.yml'],['firmware/CHANGELOG.zh.md']]:
            result=classify(paths)
            self.assertTrue(result['tools'])
            self.assertFalse(any(result[k] for k in ('windows','macos','firmware')))

    def test_changed_and_deleted_product_paths_are_not_suppressed(self):
        self.assertTrue(classify(['windows-app/AIBotBridge/Program.cs'])['windows'])
        self.assertTrue(classify(['mac-app/Sources/AIBotBridge/SerialBridge.swift','docs/removed.swift'])['macos'])
        self.assertTrue(classify(['firmware/src/main.cpp'])['firmware'])
        self.assertTrue(all(classify(['unknown/build.config'])[k] for k in ('windows','macos','firmware')))


if __name__ == '__main__':
    unittest.main()
