"""Synthetic CLI peer for Windows transport tests; no network, files or credentials."""
import json
import sys

session = next(a.split('=', 1)[1] for a in sys.argv if a.startswith('--resume='))
current = None
queued = []
permission = False


def emit(value):
    print(json.dumps(value, ensure_ascii=True), flush=True)


def result(message, reason='completed'):
    emit({'type': 'result', 'session_id': session, 'user_message_uuid': message['uuid'],
          'terminal_reason': reason, 'is_error': reason != 'completed', 'num_turns': 1})


def finish(message):
    emit(message)
    emit({'type': 'assistant', 'session_id': session, 'message': {'content': [
        {'type': 'thinking', 'thinking': 'PRIVATE_REASONING_SENTINEL'},
        {'type': 'tool_use', 'name': 'Read', 'input': {'secret': 'PRIVATE_TOOL_SENTINEL'}},
        {'type': 'text', 'text': 'Synthetic public reply'}]}})
    result(message)


for line in sys.stdin:
    frame = json.loads(line)
    if frame['type'] == 'control_request':
        interrupt = frame['request']['subtype'] == 'interrupt'
        emit({'type': 'control_response', 'response': {'subtype': 'success',
              'request_id': frame['request_id'], 'response': {
                  'cancelled': [m['uuid'] for m in queued] if interrupt else [], 'still_queued': []}}})
        if interrupt:
            queued.clear()
            if current:
                emit(current)
                result(current, 'interrupted')
                current = None
            permission = False
    elif frame['type'] == 'user':
        text = ''.join(c.get('text', '') for c in frame['message']['content'])
        if current:
            queued.append(frame)
            emit(frame)
            continue
        current = frame
        if text == 'wait':
            continue
        if text == 'hold':
            emit(frame)
            continue
        if text == 'permission':
            permission = True
            for _ in range(2):
                emit({'type': 'control_request', 'request_id': 'fixture-permission',
                      'request': {'subtype': 'can_use_tool', 'tool_name': 'Read',
                                  'input': {'file_path': 'synthetic.txt'}}})
            continue
        finish(frame)
        current = None
    elif frame['type'] == 'control_response' and permission:
        permission = False
        if frame['response']['response']['behavior'] == 'allow':
            finish(current)
        else:
            emit(current)
            result(current, 'denied')
        current = None
