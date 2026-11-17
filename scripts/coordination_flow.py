"""Scan event -> explicitly bound executor -> durable run tracking. No code interpretation."""
import argparse
import json
from pathlib import Path
import re
from coordination_checkpoint import Checkpoints, require, text
from coordination_executor import Executor


def envelope_from_event(event, route, provenance):
    require(isinstance(event, dict) and isinstance(route, dict), 'Expected event and trusted route')
    require(route.get('mappingVerified') is True and text(route.get('mappingEvidence')), 'Route must be verified explicitly')
    for field in ('repository', 'branch', 'sourcePlatform', 'targetPlatform'):
        require(text(route.get(field)), 'Missing route field ' + field)
    require(event.get('repo') == route['repository'] and event.get('branch') == route['branch'],
            'Observed repository/branch is outside fixed route')
    require(route['sourcePlatform'] in ('windows', 'macos') and route['targetPlatform'] in ('windows', 'macos')
            and route['sourcePlatform'] != route['targetPlatform'], 'Invalid fixed platform route')
    require(text(event.get('id')), 'Stable scanner event ID required')
    for field in ('before', 'after'):
        require(isinstance(event.get(field), str) and re.fullmatch('[0-9a-f]{40}', event[field]), 'Full observed SHA required')
    require(event['before'] != event['after'], 'No source change')
    require(isinstance(provenance, dict) and provenance.get('verified') is True
            and text(provenance.get('evidence')), 'Explicit development-session provenance required; do not guess')
    require(provenance.get('sourceEventId') == event['id'] and provenance.get('sourceCommit') == event['after'],
            'Provenance belongs to another observation')
    result = dict(repository=route['repository'], originChangeId=provenance.get('originChangeId'),
                  sourceReference='https://github.com/' + route['repository'] + '/compare/' + event['before'] + '...' + event['after'],
                  sourcePlatform=route['sourcePlatform'], targetPlatform=route['targetPlatform'],
                  kind=provenance.get('kind'), visitedPlatforms=provenance.get('visitedPlatforms'),
                  observationId=event['id'], sourceBranch=event['branch'], baseCommit=event['before'], sourceCommit=event['after'])
    if 'parentDeliveryId' in provenance:
        result['parentDeliveryId'] = provenance['parentDeliveryId']
    return result


def ingest(event, route, provenance, executor, worker):
    envelope = envelope_from_event(event, route, provenance)
    row = executor.deliver(envelope, worker)
    return executor.track(row['id'])


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--binding', required=True); parser.add_argument('--database', required=True)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('ingest'); p.add_argument('--route', required=True); p.add_argument('--provenance', required=True)
    p.add_argument('--event', required=True); p.add_argument('--worker', required=True)
    p = commands.add_parser('track'); p.add_argument('delivery_id')
    args = parser.parse_args()
    store = Checkpoints(args.database)
    try:
        executor = Executor(read(args.binding), store)
        result = (ingest(read(args.event), read(args.route), read(args.provenance), executor, args.worker)
                  if args.command == 'ingest' else executor.track(args.delivery_id))
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except Exception as error:
        parser.exit(1, 'Flow blocked: ' + type(error).__name__ + '; inspect route, provenance and target evidence locally\n')
    finally:
        store.close()


if __name__ == '__main__':
    main()
