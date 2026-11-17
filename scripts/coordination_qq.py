"""Fixed QQ transport wrapper with durable unknown handling. Never infers delivery/read."""
import re
import argparse
import json
import subprocess
import time
from pathlib import Path
from coordination_checkpoint import require, text


class QQNotifier:
    def __init__(self, binding, notifications, checkpoints, runner=None):
        require(isinstance(binding, dict) and binding.get('bindingVerified') is True
                and text(binding.get('bindingEvidence')), 'Verified local QQ binding required')
        command = binding.get('command')
        require(isinstance(command, list) and bool(command) and all(text(x) for x in command)
                and Path(command[0]).is_absolute(), 'Fixed absolute transport command required')
        require(text(binding.get('recipient')), 'Fixed QQ recipient required')
        self.binding, self.notifications, self.checkpoints = binding, notifications, checkpoints
        self.runner = runner or self.run

    def run(self, body):
        result = subprocess.run(self.binding['command'], input=body, capture_output=True,
                                text=True, encoding='utf-8', timeout=60)
        require(result.returncode == 0, 'QQ transport failed; result may be unknown')
        return result.stdout

    def notify_execution(self, delivery):
        handoff = self.checkpoints.get(delivery)
        observation = self.checkpoints.observation(delivery)
        require(handoff['state'] == 'confirmed' and observation is not None,
                'Actual confirmed handoff and execution evidence required')
        require(observation['state'] in ('done', 'nothing', 'cancelled'), 'Notify only observed terminal executor state')
        key = 'executor:' + delivery + ':' + observation['state']
        body = ('AI Bot 交接执行器状态：' + observation['state'] + '\n运行记录：' + observation['effect_ref']
                + '\n仅同步执行器状态，不代表代码对齐完成或原生测试通过。')
        if self.binding.get('testMode') is True:
            body = '【协调链路测试】\n' + body
        payload = {'body': body}
        if self.binding.get('sendAuthorized') is not True:
            return dict(key=key, state='awaiting-authorization', providerAccepted=False, sentNow=False)
        row = self.notifications.enqueue(key, 'qq', self.binding['recipient'], payload)
        if row['state'] in ('sending', 'unknown', 'sent'):
            return dict(key=key, state=row['state'], providerAccepted=row['state'] == 'sent', sentNow=False,
                        receipt=row['receipt_ref'], deliveryConfirmed=False, readConfirmed=False)
        row = self.notifications.begin(key)
        try:
            output = self.runner(body).strip()
            require(re.fullmatch(r'qq:[A-Za-z0-9._:-]+', output), 'No exact QQ provider receipt; do not claim success')
            result = self.notifications.confirm(key, row['token'], output,
                observed_recipient=self.binding['recipient'], observed_payload=payload, observed_at=time.time(),
                evidence='Fresh synchronous receipt from verified fixed-recipient transport; provider acceptance only')
            return dict(key=key, state=result['state'], providerAccepted=True, sentNow=True,
                        receipt=output, deliveryConfirmed=False, readConfirmed=False)
        except Exception:
            current = self.notifications.get(key)
            if current['state'] != 'sent':
                self.notifications.mark_unknown(key, row['token'])
            raise


def main():
    from coordination_checkpoint import Checkpoints
    from coordination_notifications import Notifications
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--binding', required=True)
    parser.add_argument('--checkpoints', required=True)
    parser.add_argument('--notifications', required=True)
    parser.add_argument('--delivery', required=True)
    args = parser.parse_args()
    checkpoints, notifications = Checkpoints(args.checkpoints), Notifications(args.notifications)
    try:
        binding = json.loads(Path(args.binding).read_text(encoding='utf-8'))
        result = QQNotifier(binding, notifications, checkpoints).notify_execution(args.delivery)
        print(json.dumps(result, ensure_ascii=False, indent=2))
    except Exception as error:
        parser.exit(1, 'Notification blocked: ' + type(error).__name__ + '; result may be unknown, do not resend blindly\n')
    finally:
        notifications.close(); checkpoints.close()


if __name__ == '__main__': main()
