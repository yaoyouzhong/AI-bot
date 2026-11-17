"""Durable project-side delivery state. No network, dispatch, code analysis or publication."""
from contextlib import contextmanager
import hashlib
import json
from pathlib import Path
import sqlite3
import time
import uuid

PLATFORMS = {'windows', 'macos'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def text(value):
    return isinstance(value, str) and bool(value.strip())


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'))


class Checkpoints:
    def __init__(self, path):
        # Keep this local runtime database OUTSIDE repositories and shared network filesystems.
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.db = sqlite3.connect(self.path, timeout=10, isolation_level=None)
        self.db.row_factory = sqlite3.Row
        self.db.execute('PRAGMA journal_mode=WAL')
        self.db.execute('PRAGMA synchronous=FULL')
        self.db.executescript('''
          CREATE TABLE IF NOT EXISTS deliveries (
            id TEXT PRIMARY KEY, repository TEXT NOT NULL, origin TEXT NOT NULL,
            target TEXT NOT NULL, envelope TEXT NOT NULL, state TEXT NOT NULL,
            token TEXT, worker TEXT, lease_until REAL, effect_ref TEXT, evidence TEXT,
            UNIQUE(repository, origin, target));
          CREATE TABLE IF NOT EXISTS journal (
            sequence INTEGER PRIMARY KEY, delivery TEXT NOT NULL,
            action TEXT NOT NULL, evidence TEXT NOT NULL);
          CREATE UNIQUE INDEX IF NOT EXISTS unique_effect
            ON deliveries(target,effect_ref) WHERE effect_ref IS NOT NULL;
        ''')

    def close(self):
        self.db.close()

    @contextmanager
    def transaction(self):
        self.db.execute('BEGIN IMMEDIATE')
        try:
            yield
            self.db.commit()
        except BaseException:
            self.db.rollback()
            raise

    def log(self, delivery, action, evidence):
        self.db.execute('INSERT INTO journal(delivery,action,evidence) VALUES(?,?,?)',
                        (delivery, action, evidence))

    def get(self, delivery):
        row = self.db.execute('SELECT * FROM deliveries WHERE id=?', (delivery,)).fetchone()
        require(row is not None, 'Unknown delivery')
        return dict(row)

    def enqueue(self, envelope):
        require(isinstance(envelope, dict), 'Expected envelope')
        for field in ('repository', 'originChangeId', 'sourceReference'):
            require(text(envelope.get(field)), 'Missing ' + field)
        source, target = envelope.get('sourcePlatform'), envelope.get('targetPlatform')
        require(source in PLATFORMS and target in PLATFORMS and source != target, 'Invalid platforms')
        require(envelope.get('kind') in ('source-change', 'alignment-result'), 'Unknown notification kind')
        visited = envelope.get('visitedPlatforms')
        require(isinstance(visited, list) and all(p in PLATFORMS for p in visited)
                and len(set(visited)) == len(visited) and source in visited, 'Invalid explicit provenance')
        if envelope['kind'] == 'source-change':
            require(visited == [source], 'New source change must start fresh provenance')
        else:
            require(text(envelope.get('parentDeliveryId')), 'Alignment result needs its originating delivery')
        key = (envelope['repository'].casefold(), envelope['originChangeId'], target)
        delivery = hashlib.sha256(canonical(key).encode()).hexdigest()
        encoded = canonical(envelope)
        with self.transaction():
            existing = self.db.execute('SELECT * FROM deliveries WHERE id=?', (delivery,)).fetchone()
            if existing:
                require(existing['envelope'] == encoded, 'Identity reused with different provenance; do not overwrite')
                return dict(existing)
            if envelope['kind'] == 'alignment-result':
                parent = self.get(envelope['parentDeliveryId'])
                require(parent['repository'] == key[0] and parent['origin'] == key[1]
                        and parent['target'] == source and parent['state'] == 'confirmed', 'Unconfirmed or unrelated parent delivery')
                parent_envelope = json.loads(parent['envelope'])
                require(set(visited) == set(parent_envelope['visitedPlatforms']) | {source}, 'Provenance dropped an already visited platform')
            state = 'suppressed-loop' if target in visited else 'queued'
            self.db.execute('INSERT INTO deliveries(id,repository,origin,target,envelope,state) VALUES(?,?,?,?,?,?)',
                            (*((delivery,) + key), encoded, state))
            self.log(delivery, state, 'Explicit provenance; no semantic code decision')
            return self.get(delivery)

    def claim(self, delivery, worker, lease_seconds=300, now=None):
        require(text(worker) and type(lease_seconds) in (int, float) and lease_seconds > 0, 'Invalid worker/lease')
        now = time.time() if now is None else now
        with self.transaction():
            row = self.get(delivery)
            require(row['state'] == 'queued', 'Not queued; reconcile uncertain deliveries before retry')
            token = uuid.uuid4().hex
            self.db.execute("UPDATE deliveries SET state='inflight',token=?,worker=?,lease_until=? WHERE id=?",
                            (token, worker, now + lease_seconds, delivery))
            self.log(delivery, 'claimed', worker)
            return self.get(delivery)

    def recover(self, now=None):
        now = time.time() if now is None else now
        with self.transaction():
            rows = self.db.execute("SELECT id FROM deliveries WHERE state='inflight' AND lease_until<=?", (now,)).fetchall()
            for row in rows:
                self.db.execute("UPDATE deliveries SET state='uncertain' WHERE id=?", (row['id'],))
                self.log(row['id'], 'uncertain', 'Lease ended; external effect must be reconciled, not resent')
            return [r['id'] for r in rows]

    def confirm(self, delivery, token, effect_ref, evidence):
        require(text(effect_ref) and text(evidence), 'Actual target receipt and evidence required')
        with self.transaction():
            row = self.get(delivery)
            require(row['token'] == token and text(token), 'Stale claimant')
            if row['state'] == 'confirmed':
                require(row['effect_ref'] == effect_ref, 'Conflicting effect receipt')
                return row
            require(row['state'] in ('inflight', 'uncertain'), 'Delivery cannot be confirmed')
            self.db.execute("UPDATE deliveries SET state='confirmed',effect_ref=?,evidence=? WHERE id=?",
                            (effect_ref, evidence, delivery))
            self.log(delivery, 'confirmed', evidence)
            return self.get(delivery)

    def reconcile_absent(self, delivery, token, evidence, *, worker_stopped=False, absence_verified=False):
        require(worker_stopped is True and absence_verified is True and text(evidence),
                'Retry requires verified target absence and stopped old worker')
        with self.transaction():
            row = self.get(delivery)
            require(row['state'] == 'uncertain' and row['token'] == token and text(token), 'Stale or non-uncertain delivery')
            self.db.execute("UPDATE deliveries SET state='queued',token=NULL,worker=NULL,lease_until=NULL,evidence=? WHERE id=?",
                            (evidence, delivery))
            self.log(delivery, 'absence-verified-requeued', evidence)
            return self.get(delivery)
