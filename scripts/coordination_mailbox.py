"""Isolated GitHub mailbox: immutable packets and run receipts, production routing disabled."""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import re
import subprocess
from urllib.parse import quote
from coordination_checkpoint import Checkpoints, canonical, require, text
from coordination_executor import Executor
from coordination_flow import envelope_from_event, ingest


class Mailbox:
    def __init__(self, config, runner=None):
        require(config.get('mode') == 'isolated-test' and config.get('enabled') is True,
                'Only explicitly enabled isolated-test mailbox is implemented; production disabled')
        require(re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', config.get('repository', '')), 'Invalid fixed repository')
        require(text(config.get('branch')) and config['branch'] not in ('main', 'master'), 'Product main branch forbidden')
        require(re.fullmatch(r'isolated-test/[A-Za-z0-9_.-]+', config.get('namespace', '')), 'Explicit isolated namespace required')
        require(config.get('writeAuthorized') is True and text(config.get('authorizationEvidence')), 'Explicit mailbox scope authorization required')
        require(text(config.get('ghExecutable')) and Path(config['ghExecutable']).is_absolute(), 'Fixed GitHub CLI executable required')
        self.config = config
        self.prefix = 'repos/' + config['repository'] + '/'
        self.directory = 'docs/development/coordination-ledger/transport/' + config['namespace']
        self.runner = runner or self.run

    def run(self, args, payload=None):
        result = subprocess.run([self.config['ghExecutable'], 'api'] + args,
                                input=json.dumps(payload) if payload is not None else None,
                                capture_output=True, text=True, encoding='utf-8', timeout=60)
        if result.returncode:
            if 'HTTP 404' in result.stderr: return None
            raise ValueError('GitHub request failed; preserve local state and reconcile remote')
        return json.loads(result.stdout)

    def get_file(self, path, ref):
        result = self.runner([self.prefix + 'contents/' + path + '?ref=' + quote(ref, safe='')])
        if result is None: return None
        require(isinstance(result, dict) and result.get('encoding') == 'base64', 'Unexpected mailbox content')
        return json.loads(base64.b64decode(result['content']).decode('utf-8'))

    def create(self, filename, content):
        require(re.fullmatch(r'[0-9a-f]{64}(?:\.receipt)?\.json', filename), 'Invalid mailbox filename')
        path = self.directory + '/' + filename
        existing = self.get_file(path, self.config['branch'])
        if existing is not None:
            require(existing == content, 'Immutable mailbox conflict; do not overwrite')
            return dict(path=path, existed=True)
        payload = dict(branch=self.config['branch'], message='Record isolated coordination transport evidence',
                       content=base64.b64encode((canonical(content) + '\n').encode()).decode())
        try:
            response = self.runner([self.prefix + 'contents/' + path, '--method', 'PUT', '--input', '-'], payload)
            require(response is not None, 'Mailbox write not acknowledged')
        except Exception:
            # A failed/lost reply does not prove that GitHub rejected the write.
            actual = self.get_file(path, self.config['branch'])
            require(actual == content, 'Mailbox write outcome unknown; do not create another packet')
            return dict(path=path, existed=False, reconciled=True)
        actual = self.get_file(path, self.config['branch'])
        require(actual == content, 'Mailbox post-write verification failed')
        return dict(path=path, existed=False)

    def publish(self, event, route, provenance):
        envelope = envelope_from_event(event, route, provenance)
        require(route['repository'] == self.config['repository'], 'Route differs from mailbox repository')
        core = {key: event[key] for key in ('id', 'repo', 'branch', 'before', 'after')}
        public_provenance = {key: provenance[key] for key in ('sourceEventId', 'sourceCommit', 'originChangeId', 'kind', 'visitedPlatforms', 'verified')}
        if 'parentDeliveryId' in provenance: public_provenance['parentDeliveryId'] = provenance['parentDeliveryId']
        public_provenance['evidence'] = 'Explicit isolated replay provenance recorded by publisher'
        packet = dict(schemaVersion=1, mode='isolated-test', event=core, provenance=public_provenance,
                      targetPlatform=route['targetPlatform'], sourcePlatform=route['sourcePlatform'])
        # No raw scanner logs, local paths, credential fields or code diffs are transported.
        packet_id = hashlib.sha256(canonical(packet).encode()).hexdigest()
        packet['packetId'] = packet_id
        return dict(packetId=packet_id, **self.create(packet_id + '.json', packet))

    def poll(self, route, executor, worker):
        require(route['repository'] == self.config['repository'], 'Route differs from mailbox repository')
        ref = self.runner([self.prefix + 'git/ref/heads/' + quote(self.config['branch'], safe='/')])
        require(isinstance(ref, dict), 'Mailbox branch unavailable')
        commit = ref['object']['sha']
        require(re.fullmatch('[0-9a-f]{40}', commit), 'Invalid mailbox snapshot')
        rows = self.runner([self.prefix + 'contents/' + self.directory + '?ref=' + commit])
        if rows is None:
            # The branch exists; a missing isolated namespace means there are no packets yet.
            return dict(snapshot=commit, results=[])
        require(isinstance(rows, list) and len(rows) < 1000, 'Mailbox listing incomplete or invalid')
        results = []
        for row in rows:
            name = row.get('name', '')
            if not re.fullmatch(r'[0-9a-f]{64}\.json', name): continue
            require(row.get('type') == 'file' and row.get('path') == self.directory + '/' + name, 'Unexpected mailbox entry')
            packet = self.get_file(row['path'], commit)
            require(isinstance(packet, dict) and packet.get('schemaVersion') == 1 and packet.get('mode') == 'isolated-test', 'Invalid packet')
            identity = hashlib.sha256(canonical({k: v for k, v in packet.items() if k != 'packetId'}).encode()).hexdigest()
            require(packet.get('packetId') == identity and name == identity + '.json', 'Packet identity mismatch')
            if packet.get('targetPlatform') != route['targetPlatform']: continue
            require(packet.get('sourcePlatform') == route['sourcePlatform'], 'Packet source differs from fixed route')
            result = ingest(packet['event'], route, packet['provenance'], executor, worker)
            observation = result.get('execution')
            if observation and observation['state'] in ('done', 'nothing', 'cancelled'):
                receipt = dict(schemaVersion=1, mode='isolated-test', packetId=identity,
                               delivery=result['delivery'], targetPlatform=route['targetPlatform'],
                               handoffState=result['handoffState'], executorState=observation['state'],
                               effectRef=observation['effect_ref'], developmentCompletionAsserted=False, publicationPerformed=False)
                self.create(identity + '.receipt.json', receipt)
            results.append(dict(packetId=identity, **result))
        return dict(snapshot=commit, results=results)


def read(path): return json.loads(Path(path).read_text(encoding='utf-8'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mailbox', required=True); parser.add_argument('--route', required=True)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('publish'); p.add_argument('--event', required=True); p.add_argument('--provenance', required=True)
    p = commands.add_parser('poll'); p.add_argument('--binding', required=True); p.add_argument('--database', required=True); p.add_argument('--worker', required=True)
    args = parser.parse_args()
    store = None
    try:
        mailbox = Mailbox(read(args.mailbox)); route = read(args.route)
        if args.command == 'publish': result = mailbox.publish(read(args.event), route, read(args.provenance))
        else:
            store = Checkpoints(args.database)
            result = mailbox.poll(route, Executor(read(args.binding), store), args.worker)
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except Exception as error:
        parser.exit(1, 'Mailbox blocked: ' + type(error).__name__ + '; preserve packet identity and reconcile, no blind retry\n')
    finally:
        if store: store.close()


if __name__ == '__main__': main()
