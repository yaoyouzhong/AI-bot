"""Independent Git-pinned release scope, checked against actual commit and file diffs."""
import argparse
import json
import os
from pathlib import Path
import subprocess


def git(repo, *args):
    result = subprocess.run(['git', '-C', str(repo), *args], capture_output=True)
    if result.returncode:
        raise ValueError('Scope Git evidence unavailable; fetch full source and scope history')
    return result.stdout.decode('utf-8')


def inventory(repo, repository, base, source):
    from cross_platform_coordination import ancestor, sha, require
    sha(base); sha(source)
    require(base != source and ancestor(repo, base, source), 'Release baseline must precede source')
    commits = sorted(git(repo, 'rev-list', source, '^' + base).splitlines())
    files = sorted(filter(None, git(repo, 'diff', '--no-renames', '--name-only', '-z', base, source, '--').split('\0')))
    return dict(schemaVersion=1, repository=repository, releaseBase=base, releaseSource=source,
                commits=commits, files=files, reviewed=False, scopeEvidence='', requiredTaskIds=[],
                commitTasks={commit: [] for commit in commits}, fileTasks={path: [] for path in files})


def record_path(source):
    from cross_platform_coordination import sha
    return 'docs/development/coordination-ledger/release-scopes/' + sha(source) + '.json'


def verify_scope(bundle, repo):
    from cross_platform_coordination import sha, require, nonempty
    # Pin separately from the mutable submitted plan. No fallback to plan-provided scope IDs.
    pinned = os.environ.get('AIBOT_COORDINATION_SCOPE_COMMIT', '')
    require(bool(pinned), 'AIBOT_COORDINATION_SCOPE_COMMIT must pin the reviewed scope independently')
    sha(pinned)
    source = sha(bundle['releaseSource'])
    path = record_path(source)
    record = json.loads(git(repo, 'show', pinned + ':' + path))
    require(isinstance(record, dict) and type(record.get('schemaVersion')) is int and record['schemaVersion'] == 1,
            'Unsupported release scope schema')
    require(record.get('reviewed') is True and nonempty(record.get('scopeEvidence')), 'Release scope is not reviewed')
    require(record.get('repository') == bundle['repository'] and record.get('releaseSource') == source,
            'Scope repository/source mismatch')
    actual = inventory(repo, bundle['repository'], record.get('releaseBase'), source)
    for field in ('commits', 'files'):
        require(record.get(field) == actual[field], 'Scope ' + field + ' differ from actual Git changes')
    required = record.get('requiredTaskIds')
    require(isinstance(required, list) and bool(required) and all(nonempty(x) for x in required), 'Empty scope task registry')
    require(len(set(required)) == len(required), 'Duplicate scope task ID')
    require(set(bundle['requiredTaskIds']) == set(required), 'Plan omits or adds tasks relative to independently pinned scope')
    referenced = set()
    for field, keys in (('commitTasks', actual['commits']), ('fileTasks', actual['files'])):
        mapping = record.get(field)
        require(isinstance(mapping, dict) and set(mapping) == set(keys), 'Incomplete ' + field + ' coverage')
        for values in mapping.values():
            require(isinstance(values, list) and bool(values) and all(nonempty(x) for x in values), 'Unassigned source change')
            require(len(set(values)) == len(values) and set(values) <= set(required), 'Invalid task mapping')
            referenced.update(values)
    require(referenced == set(required), 'Scope contains tasks unrelated to its change mapping')
    return dict(commit=pinned, path=path, releaseBase=record['releaseBase'],
                commitCount=len(actual['commits']), fileCount=len(actual['files']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', default='.')
    parser.add_argument('--repository', required=True)
    parser.add_argument('--base', required=True)
    parser.add_argument('--source', required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(inventory(args.repo, args.repository, args.base, args.source), ensure_ascii=False, indent=2))
    except (ValueError, TypeError, OSError) as error:
        parser.exit(1, 'Scope blocked: ' + str(error) + '\n')


if __name__ == '__main__':
    main()
