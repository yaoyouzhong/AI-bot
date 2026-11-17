"""Project-side lifecycle contract. Emits JSON only; callers perform GitHub blob-SHA CAS."""
import argparse
from copy import deepcopy
import json
from pathlib import Path
import cross_platform_coordination as c

STATES = {'offered', 'claimed', 'acknowledged', 'running', 'blocked', *c.TERMINAL}


def create(task, authorization):
    c.validate_task(task)
    c.require(not task.get('simulationOnly'), 'Simulation cannot become a real development task')
    c.require(c.nonempty(authorization), 'Missing user authorization reference')
    return dict(schemaVersion=1, mode='development-task', revision=0, task=task,
                authorization=authorization, platforms={p: {'status': 'offered'} for p in c.PLATFORMS}, history=[])


def validate(ledger):
    c.require(isinstance(ledger, dict) and ledger.get('schemaVersion') == 1
              and ledger.get('mode') == 'development-task', 'Not a real development ledger')
    c.validate_task(ledger['task'])
    c.require(not ledger['task'].get('simulationOnly'), 'Simulation cannot authorize development')
    c.require(c.nonempty(ledger.get('authorization')), 'Missing user authorization reference')
    c.require(type(ledger.get('revision')) is int and ledger['revision'] >= 0, 'Invalid revision')
    c.require(isinstance(ledger.get('history'), list) and len(ledger['history']) == ledger['revision'], 'Invalid transition history')
    c.require(set(ledger.get('platforms', {})) == set(c.PLATFORMS), 'Missing platform state')
    for platform, state in ledger['platforms'].items():
        c.require(state.get('status') in STATES, 'Invalid lifecycle state')
        if state['status'] != 'offered':
            c.require(c.nonempty(state.get('claimId')), 'Missing claim identity')
        if state['status'] in c.TERMINAL:
            receipt = c.validate_receipt(ledger['task'], state['receipt'])
            c.require(receipt['platform'] == platform and receipt['status'] == state['status'], 'State/receipt mismatch')
    return ledger


def transition(ledger, expected_revision, platform, action, claim_id, evidence, receipt=None):
    validate(ledger)
    c.require(expected_revision == ledger['revision'], 'Stale revision; reread ledger, do not overwrite')
    c.require(platform in c.PLATFORMS and c.nonempty(claim_id), 'Invalid platform/claim')
    c.require(c.nonempty(evidence), 'Missing transition evidence')
    old = ledger['platforms'][platform]
    allowed = {'claim': ('offered',), 'acknowledge': ('claimed',), 'start': ('acknowledged',),
               'block': ('claimed', 'acknowledged', 'running'), 'resume': ('blocked',),
               'finish': ('acknowledged', 'running')}
    c.require(action in allowed and old['status'] in allowed[action], 'Invalid lifecycle transition')
    if action != 'claim':
        c.require(old.get('claimId') == claim_id, 'Another worker owns this claim')
    result = deepcopy(ledger)
    status = dict(claim='claimed', acknowledge='acknowledged', start='running', block='blocked', resume='acknowledged').get(action)
    if action == 'finish':
        c.validate_receipt(ledger['task'], receipt)
        c.require(receipt['platform'] == platform, 'Receipt belongs to other platform')
        status = receipt['status']
    state = dict(status=status, claimId=claim_id, evidence=evidence)
    if action == 'finish':
        state['receipt'] = receipt
    result['platforms'][platform] = state
    result['revision'] += 1
    result['history'].append(dict(revision=result['revision'], platform=platform, action=action,
                                  claimId=claim_id, evidence=evidence))
    return validate(result)


def plan(ledgers, source, scope_evidence):
    c.sha(source)
    c.require(bool(ledgers) and c.nonempty(scope_evidence), 'Missing release scope')
    for ledger in ledgers:
        validate(ledger)
    repository = ledgers[0]['task']['repository']
    c.require(all(x['task']['repository'] == repository for x in ledgers), 'Mixed repositories')
    tasks = [x['task'] for x in ledgers]
    c.require(len({x['taskId'] for x in tasks}) == len(tasks), 'Duplicate task')
    receipts = []
    for ledger in ledgers:
        for state in ledger['platforms'].values():
            c.require(state['status'] in c.TERMINAL, 'Task is not complete on both platforms')
            receipts.append(state['receipt'])
    return dict(schemaVersion=1, repository=repository, releaseSource=source, scopeEvidence=scope_evidence,
                requiredTaskIds=[x['taskId'] for x in tasks], tasks=tasks, receipts=receipts)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('create'); p.add_argument('task'); p.add_argument('--authorization', required=True)
    p = commands.add_parser('transition'); p.add_argument('ledger'); p.add_argument('--revision', type=int, required=True)
    p.add_argument('--platform', choices=c.PLATFORMS, required=True)
    p.add_argument('--action', choices=['claim', 'acknowledge', 'start', 'block', 'resume', 'finish'], required=True)
    p.add_argument('--claim-id', required=True); p.add_argument('--evidence', required=True); p.add_argument('--receipt')
    p = commands.add_parser('plan'); p.add_argument('ledgers', nargs='+'); p.add_argument('--source', required=True)
    p.add_argument('--scope-evidence', required=True)
    args = parser.parse_args()
    try:
        if args.command == 'create':
            result = create(c.read(args.task), args.authorization)
        elif args.command == 'transition':
            result = transition(c.read(args.ledger), args.revision, args.platform, args.action, args.claim_id,
                                args.evidence, c.read(args.receipt) if args.receipt else None)
        else:
            result = plan([c.read(p) for p in args.ledgers], args.source, args.scope_evidence)
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except (ValueError, KeyError, TypeError, OSError, AttributeError) as error:
        parser.exit(1, 'Coordination blocked: ' + str(error) + '\n')


if __name__ == '__main__':
    main()
