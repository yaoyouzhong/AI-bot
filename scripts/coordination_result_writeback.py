"""Native terminal observations -> existing immutable coordination mailbox I/O.

No task creation, queue, code execution, notification or release acceptance.
Only reviewed enums leave the machine; never export summary/events/session/error.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import urlparse
from urllib.request import Request, build_opener, ProxyHandler
import os
from coordination_checkpoint import canonical, require
from coordination_mailbox import Mailbox
from coordination_main_review import alignment_event, review_event, sha
from coordination_main_watch import NoRedirect

REPOSITORY = 'yaoyouzhong/AI-bot'
BRANCH = 'codex/autoloop-coordination'
DIRECTORY = 'docs/development/coordination-ledger/results'
TERMINAL = {'done', 'nothing', 'cancelled'}
CHECKS = {'syntax', 'workspace', 'targeted-runtime', 'native-runtime', 'device'}
OUTCOMES = {'passed', 'failed', 'sandbox-blocked', 'not-run', 'not-reported'}
GAPS = {'targeted-runtime-sandbox-blocked', 'native-runtime-not-verified',
        'device-not-verified', 'verification-not-reported-via-native-api'}


def timestamp(value):
    require(isinstance(value, str) and re.fullmatch(r'\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,6})?Z', value), 'Invalid UTC time')
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


def validate_config(config):
    require(config.get('enabled') is True and config.get('mode') == 'native-terminal-result', 'Explicit result adapter required')
    require(config.get('repository') == REPOSITORY and config.get('branch') == BRANCH, 'Outside authorized coordination branch')
    require(config.get('writeAuthorized') is True and bool(config.get('authorizationEvidence')), 'Write scope required')
    require(Path(config.get('ghExecutable', '')).is_absolute(), 'Fixed gh executable required')
    allowed = config.get('authorizedSourceCommits')
    require(isinstance(allowed, list) and bool(allowed) and all(sha(x) for x in allowed)
            and len(set(allowed)) == len(allowed), 'Explicit authorized SHA allowlist required')
    timestamp(config.get('activatedAt'))
    require(set(config.get('publicFacts', {})) <= set(allowed), 'Facts outside authorized scope')
    return config


def public_facts(facts):
    # Reject unknown fields instead of filtering arbitrary local or model text.
    require(isinstance(facts, dict) and set(facts) == {'decision', 'changes', 'checks', 'gaps'}, 'Unexpected public facts')
    require(facts['decision'] in {'no-code-change', 'modified', 'blocked', 'not-asserted'}, 'Invalid decision')
    require(facts['changes'] in {'none', 'local-only', 'not-asserted'}, 'Invalid change scope')
    require(isinstance(facts['checks'], dict) and set(facts['checks']) <= CHECKS
            and all(v in OUTCOMES for v in facts['checks'].values()), 'Unreviewed check fields')
    require(isinstance(facts['gaps'], list) and set(facts['gaps']) <= GAPS
            and len(set(facts['gaps'])) == len(facts['gaps']), 'Unreviewed gap fields')
    return json.loads(canonical(facts))


def receipt(config, watch, observation, native):
    validate_config(config)
    binding = watch['binding']; head = observation['head']
    require(binding['repository'] == REPOSITORY and watch['branch'] == 'main', 'Not main observation')
    require(head in config['authorizedSourceCommits'], 'Source outside authorized scope')
    expected = (alignment_event if binding['mode'] == 'isolated-alignment' else review_event)(REPOSITORY, 'main', head, binding)
    require(observation.get('event') == expected and observation.get('repository') == REPOSITORY
            and observation.get('branch') == 'main', 'Observation binding changed')
    event_id = native.get('event')
    require(type(event_id) is int and event_id > 0 and isinstance(native.get('runs'), list), 'Invalid native event')
    runs = native['runs']; rule = watch['expectedRule']
    require(len(runs) <= 1 and all(r.get('rule') == rule for r in runs), 'Unexpected/multiple native runs')
    normal = native.get('routed') == [rule] and not native.get('paused')
    recovered = bool(runs) and native.get('routed') == [] and native.get('paused') == [rule]
    require(normal or recovered, 'Unconfirmed native route')
    if not runs: return None
    run = runs[0]
    require(type(run.get('run')) is int and run['run'] > 0, 'Invalid native run')
    require(run.get('state') in TERMINAL | {'queued', 'claimed', 'unknown'}, 'Unknown native state')
    if run['state'] not in TERMINAL: return None
    finished = run.get('finishedAt'); timestamp(finished)
    facts = config.get('publicFacts', {}).get(head)
    facts = public_facts(facts if facts is not None else dict(decision='not-asserted', changes='not-asserted', checks={},
                  gaps=['verification-not-reported-via-native-api']))
    if run['state'] == 'cancelled':
        require(facts['decision'] in {'blocked', 'not-asserted'}, 'Cancelled is not successful analysis')
    identity = dict(repository=REPOSITORY, sourceCommit=head, platform=binding['targetPlatform'])
    result_id = hashlib.sha256(canonical([REPOSITORY.casefold(), head, binding['targetPlatform'], 'native-result-v1']).encode()).hexdigest()
    return dict(schemaVersion=1, mode='execution-result', resultId=result_id, **identity, sourceBranch='main',
                kind='alignment-result', dispatchEligible=False, terminalState=run['state'], finishedAt=finished,
                provenance=dict(transport='native-source-get', trigger='existing-main-watch',
                    observation='backfill' if timestamp(finished) < timestamp(config['activatedAt']) else 'live'),
                facts=facts, factsOrigin='reviewed-local-enums' if head in config.get('publicFacts', {}) else 'native-state-only',
                productAccepted=False, developmentCompletionAsserted=False,
                productCommitPerformed=False, productPushPerformed=False, mergePerformed=False,
                publicationPerformed=False, rawSummaryExported=False)


class ResultMailbox(Mailbox):
    def __init__(self, config, runner=None):
        # Keep the legacy isolated-test constructor strict. Reuse only its I/O.
        validate_config(config)
        self.config = config; self.prefix = 'repos/' + REPOSITORY + '/'
        self.runner = runner or self.run

    def publish_result(self, value):
        head = value['sourceCommit']; platform = value['platform']
        require(head in self.config['authorizedSourceCommits'] and platform in {'macos', 'windows'}, 'Wrong result route')
        require(value['repository'] == REPOSITORY and value['dispatchEligible'] is False
                and value['publicationPerformed'] is False and value['productAccepted'] is False, 'Invalid result semantics')
        path = DIRECTORY + '/' + head + '/' + platform + '.json'
        return self.create_at(path, value, 'Record native terminal execution result (not product acceptance)', attempts=3)


def writeback_once(config, watch, hub, mailbox=None):
    validate_config(config)
    require(watch.get('enabled') is True and watch.get('branch') == 'main', 'Fixed enabled watch required')
    root = Path(watch['auditRoot']); require(root.is_absolute(), 'Fixed audit root required')
    # Consume the native references already recorded by the existing watcher.
    # Never POST events, replay, or synthesize notifications here.
    snapshot = json.loads(Path(config['watchSnapshot']).read_text(encoding='utf-8'))
    rows = snapshot.get('results'); require(isinstance(rows, list) and len(rows) <= 1000, 'Invalid native watch snapshot')
    authorized = [x for x in rows if x.get('head') in config['authorizedSourceCommits']]
    require(len({x['head'] for x in authorized}) == len(authorized), 'Duplicate source observations')
    box = mailbox or ResultMailbox(config); results = []
    for row in authorized:
        head = row['head']; observation = json.loads((root/'observations'/(head+'.json')).read_text(encoding='utf-8'))
        require(observation['head'] == head, 'Wrong observation source')
        event_id = row.get('nativeEvent')
        require(type(event_id) is int and event_id > 0, 'No native event reference')
        native = hub('GET', '/v1/events/' + str(event_id))
        require(native.get('event') == event_id, 'Wrong native event returned')
        if native['runs'] and row.get('nativeRun') is not None:
            require(native['runs'][0].get('run') == row['nativeRun'], 'Native run association changed')
        # Existing immutable watch audit is the event -> source association.
        value = receipt(config, watch, observation, native)
        if value is None: results.append(dict(sourceCommit=head, state='not-terminal')); continue
        results.append(dict(sourceCommit=head, terminalState=value['terminalState'], **box.publish_result(value)))
    return dict(results=results, publicationPerformed=False, eventsEmitted=0)


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--config', required=True)
    args = parser.parse_args(); config = json.loads(Path(args.config).read_text(encoding='utf-8'))
    validate_config(config)
    watch = json.loads(Path(config['watchConfig']).read_text(encoding='utf-8'))
    url = watch['hubUrl']; parsed = urlparse(url)
    require(parsed.scheme == 'http' and parsed.hostname in ('127.0.0.1', '::1') and parsed.port
            and parsed.path in ('', '/') and not parsed.query and not parsed.fragment and not parsed.username, 'Loopback only')
    key = os.environ.get('AIBOT_HUB_SOURCE_KEY'); require(bool(key), 'Existing source key required in memory')
    opener = build_opener(ProxyHandler({}), NoRedirect())
    def hub(method, path):
        require(method == 'GET' and re.fullmatch(r'/v1/events/[1-9][0-9]*', path), 'Read-only native query only')
        request = Request(url.rstrip('/')+path, headers={'Authorization': 'Bearer '+key}, method='GET')
        with opener.open(request, timeout=30) as response: return json.load(response)
    print(json.dumps(writeback_once(config, watch, hub), ensure_ascii=False))


if __name__ == '__main__':
    try: main()
    except Exception as error:
        raise SystemExit('Result writeback blocked: '+type(error).__name__+'; preserve native and remote evidence')
