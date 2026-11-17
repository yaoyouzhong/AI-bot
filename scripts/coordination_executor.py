"""Fixed Auto Loop CLI binding. Checkpoint delivery; match actual runs, never infer code work."""
import argparse
import json
import hashlib
from pathlib import Path
import subprocess
from coordination_checkpoint import Checkpoints, require, text, canonical


class Executor:
    def __init__(self, binding, store, runner=None):
        require(isinstance(binding, dict), 'Expected trusted binding')
        prefix = binding.get('commandPrefix')
        require(isinstance(prefix, list) and bool(prefix) and all(text(x) for x in prefix), 'Missing command prefix')
        require(Path(prefix[0]).is_absolute(), 'Executable must be an explicitly bound absolute path')
        for field in ('expectedRule', 'expectedAgent', 'eventSource', 'eventType', 'targetPlatform'):
            require(text(binding.get(field)), 'Missing fixed binding field ' + field)
        require(binding['targetPlatform'] in ('windows', 'macos'), 'Invalid target')
        require(binding.get('bindingVerified') is True, 'Binding must be verified, not guessed')
        require(text(binding.get('bindingEvidence')), 'Missing binding verification evidence')
        self.binding, self.store = binding, store
        self.fingerprint = hashlib.sha256(canonical(binding).encode()).hexdigest()
        self.runner = runner or self.run

    def run(self, args):
        # No shell, no source/token extraction, no command chosen by event data.
        result = subprocess.run(self.binding['commandPrefix'] + args, capture_output=True,
                                text=True, encoding='utf-8', timeout=60)
        require(result.returncode == 0, 'Auto Loop CLI failed; inspect local logs without exposing credentials')
        return result.stdout

    def find_run(self, delivery):
        before, cursors, matches = None, set(), []
        for _ in range(100):
            args = ['runs', self.binding['expectedRule'], '--json']
            if before is not None: args += ['--before', str(before)]
            page = json.loads(self.runner(args))
            require(isinstance(page, dict) and isinstance(page.get('runs'), list), 'Unexpected runs response')
            for run in page['runs']:
                if run.get('rule') != self.binding['expectedRule'] or run.get('agent') != self.binding['expectedAgent']:
                    continue
                events = run.get('events', [])
                require(isinstance(events, list), 'Unexpected event evidence')
                if any(e.get('subject') == delivery and e.get('source') == self.binding['eventSource']
                       and e.get('type') == self.binding['eventType'] for e in events):
                    require(type(run.get('id')) is int and run['id'] > 0, 'Missing actual run ID')
                    matches.append(run)
            require(len(matches) <= 1, 'Multiple target runs matched one delivery; manual reconciliation required')
            before = page.get('before')
            if before is None: return matches[0] if matches else None
            require(type(before) is int and before > 0 and before not in cursors, 'Invalid/repeated pagination cursor')
            cursors.add(before)
        raise ValueError('Run history incomplete; no absence conclusion')

    def reconcile(self, delivery):
        row = self.store.get(delivery)
        require(json.loads(row['envelope']).get('dispatchBinding') == self.fingerprint, 'Binding changed; do not redirect existing delivery')
        if row['state'] in ('confirmed', 'suppressed-loop'): return row
        require(row['state'] in ('inflight', 'uncertain'), 'Nothing sent to reconcile')
        run = self.find_run(delivery)
        if run is None:
            return self.store.mark_uncertain(delivery, row['token'], 'No matching run observed yet; query only, no resend')
        reference = 'autoloop:' + self.binding['expectedRule'] + ':run:' + str(run['id'])
        evidence = canonical(dict(rule=run['rule'], agent=run['agent'], run=run['id'], state=run.get('state'),
                                  subject=delivery, meaning='Handoff observed; not development completion'))
        return self.store.confirm(delivery, row['token'], reference, evidence)

    def track(self, delivery):
        """Read actual run status, including recovery after confirm-before-observe interruption."""
        row = self.store.get(delivery)
        require(json.loads(row['envelope']).get('dispatchBinding') == self.fingerprint, 'Binding changed; do not redirect existing delivery')
        if row['state'] == 'suppressed-loop':
            return dict(delivery=row['id'], handoffState=row['state'], execution=None, developmentCompletionAsserted=False, publicationPerformed=False)
        if row['state'] != 'confirmed':
            row = self.reconcile(delivery)
        if row['state'] == 'confirmed':
            run = self.find_run(delivery)
            if run is not None:
                reference = 'autoloop:' + self.binding['expectedRule'] + ':run:' + str(run['id'])
                self.store.observe_run(delivery, reference, run.get('state'),
                    canonical(dict(run=run['id'], rule=run['rule'], state=run.get('state'),
                                   meaning='Executor status only; target session supplies technical result')))
        return dict(delivery=row['id'], handoffState=row['state'], execution=self.store.observation(delivery),
                    developmentCompletionAsserted=False, publicationPerformed=False)

    def deliver(self, envelope, worker):
        require(envelope.get('targetPlatform') == self.binding['targetPlatform'], 'Envelope target differs from fixed binding')
        envelope = {**envelope, "dispatchBinding": self.fingerprint}
        row = self.store.enqueue(envelope)
        if row['state'] in ('confirmed', 'suppressed-loop'): return row
        if row['state'] in ('inflight', 'uncertain'): return self.reconcile(row['id'])
        row = self.store.claim(row['id'], worker)
        try:
            detail = canonical(envelope)
            require(len(detail) <= 4000, 'Envelope exceeds event detail limit')
            self.runner(['emit', self.binding['eventType'], '--id', row['id'], '--subject', row['id'],
                         '--summary', 'AI-bot coordination handoff; target session owns analysis', '--detail', detail])
            return self.reconcile(row['id'])
        except Exception:
            # CLI stderr can contain private configuration; do not copy it into durable state.
            current = self.store.get(row['id'])
            if current['state'] != 'confirmed':
                self.store.mark_uncertain(row['id'], row['token'], 'Dispatch or receipt lookup interrupted; reconcile target before retry')
            raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--binding', required=True)
    parser.add_argument('--database', required=True)
    commands = parser.add_subparsers(dest='command', required=True)
    send = commands.add_parser('deliver'); send.add_argument('envelope'); send.add_argument('--worker', required=True)
    query = commands.add_parser('reconcile'); query.add_argument('delivery_id')
    args = parser.parse_args()
    store = Checkpoints(args.database)
    try:
        executor = Executor(json.loads(Path(args.binding).read_text(encoding='utf-8')), store)
        result = (executor.deliver(json.loads(Path(args.envelope).read_text(encoding='utf-8')), args.worker)
                  if args.command == 'deliver' else executor.reconcile(args.delivery_id))
        print(json.dumps({key: result[key] for key in ('id', 'state', 'effect_ref', 'evidence')}, ensure_ascii=False, indent=2))
    except Exception as error:
        parser.exit(1, 'Executor blocked: ' + type(error).__name__ + '; inspect trusted local binding and target run evidence\n')
    finally:
        store.close()


if __name__ == '__main__':
    main()
