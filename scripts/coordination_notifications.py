"""Independent durable notification ledger; no transport calls or implied delivery."""
import hashlib
import sqlite3
import uuid
import time
from coordination_checkpoint import Checkpoints, canonical, require, text


def digest(value):
    return hashlib.sha256(canonical(value).encode()).hexdigest()


class Notifications:
    def __init__(self, path):
        self.store = Checkpoints(path)
        self.db = self.store.db
        self.db.execute('''CREATE TABLE IF NOT EXISTS notifications (
            key TEXT PRIMARY KEY, channel TEXT NOT NULL, recipient_digest TEXT NOT NULL,
            payload_digest TEXT NOT NULL, state TEXT NOT NULL, token TEXT, receipt_ref TEXT UNIQUE,
            evidence TEXT, started_at REAL)''')

    def close(self):
        self.store.close()

    def get(self, key):
        row = self.db.execute('SELECT * FROM notifications WHERE key=?', (key,)).fetchone()
        require(row is not None, 'Unknown notification key')
        return dict(row)

    def enqueue(self, key, channel, recipient, payload):
        require(all(text(x) for x in (key, channel, recipient)), 'Stable notification key, channel and recipient required')
        recipient_hash, payload_hash = digest(recipient), digest(payload)
        with self.store.transaction():
            row = self.db.execute('SELECT * FROM notifications WHERE key=?', (key,)).fetchone()
            if row:
                require((row['channel'], row['recipient_digest'], row['payload_digest']) == (channel, recipient_hash, payload_hash),
                        'Notification key reused for different message or recipient')
                return dict(row)
            self.db.execute("INSERT INTO notifications(key,channel,recipient_digest,payload_digest,state) VALUES(?,?,?,?,'queued')",
                            (key, channel, recipient_hash, payload_hash))
            return self.get(key)

    def begin(self, key):
        with self.store.transaction():
            row = self.get(key)
            require(row['state'] == 'queued', 'Do not resend sending/unknown/sent notifications')
            token = uuid.uuid4().hex
            self.db.execute("UPDATE notifications SET state='sending',token=?,started_at=? WHERE key=?", (token, time.time(), key))
            return self.get(key)

    def recover(self):
        # No external action. An in-progress old worker can still confirm with its original token.
        with self.store.transaction():
            self.db.execute("UPDATE notifications SET state='unknown',evidence='Transport result must be reconciled; no resend' WHERE state='sending'")

    def confirm(self, key, token, receipt_ref, *, observed_recipient, observed_payload, observed_at, evidence):
        require(text(receipt_ref) and text(evidence), 'Actual matching transport receipt is required')
        with self.store.transaction():
            row = self.get(key)
            require(row['token'] == token and text(token), 'Stale notification attempt')
            require(type(observed_at) in (int, float) and row['started_at'] <= observed_at <= time.time() + 5,
                    'Receipt predates this attempt or has invalid observation time')
            require(row['recipient_digest'] == digest(observed_recipient) and row['payload_digest'] == digest(observed_payload),
                    'Transport evidence belongs to another recipient or message')
            if row['state'] == 'sent':
                require(row['receipt_ref'] == receipt_ref, 'Conflicting receipt')
                return row
            require(row['state'] in ('sending', 'unknown'), 'Notification was not sent by this attempt')
            # UNIQUE prevents attaching an old successful receipt to a different notification key.
            self.db.execute("UPDATE notifications SET state='sent',receipt_ref=?,evidence=? WHERE key=?", (receipt_ref, evidence, key))
            return self.get(key)
