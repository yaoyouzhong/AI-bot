"""Test installed Windows Claude against a loopback synthetic API and temporary profile."""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import threading
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class API(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers.get('Content-Length', '0'))))
        self.server.calls += 1
        latest = next((m.get('content', '') for m in reversed(body.get('messages', [])) if m.get('role') == 'user'), '')
        if 'slow' in str(latest).lower():
            self.server.release.wait(90)
        response = json.dumps({'id': 'msg_fixture_' + str(self.server.calls), 'type': 'message',
            'role': 'assistant', 'model': 'claude-sonnet-4-6', 'content': [{'type': 'text', 'text': 'Synthetic Windows public reply'}],
            'stop_reason': 'end_turn', 'stop_sequence': None, 'usage': {'input_tokens': 5, 'output_tokens': 2}}).encode()
        try:
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.end_headers()
            self.wfile.write(response)
        except (BrokenPipeError, ConnectionResetError):
            pass


def main():
    if os.name != 'nt' or len(sys.argv) != 2:
        raise SystemExit('Usage on Windows: probe_windows_claude_workspace.py BRIDGE_DLL')
    cli = shutil.which('claude.exe')
    assert cli, 'Installed Claude CLI required'
    api = ThreadingHTTPServer(('127.0.0.1', 0), API)
    api.calls, api.release = 0, threading.Event()
    threading.Thread(target=api.serve_forever, daemon=True).start()
    try:
        with tempfile.TemporaryDirectory(prefix='aibot-windows-native-claude-') as directory:
            root = Path(directory).resolve()
            config = root / 'config'
            config.mkdir()
            (root / '.claude.json').write_text(json.dumps({'hasCompletedOnboarding': True}))
            env = {key: os.environ[key] for key in ('SystemRoot', 'WINDIR', 'PATH') if key in os.environ}
            env.update(HOME=str(root), USERPROFILE=str(root), TEMP=str(root), TMP=str(root),
                APPDATA=str(root / 'roaming'), LOCALAPPDATA=str(root / 'local'), CLAUDE_CONFIG_DIR=str(config),
                ANTHROPIC_BASE_URL=f'http://127.0.0.1:{api.server_port}', ANTHROPIC_API_KEY='synthetic-key',
                CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC='1', DISABLE_TELEMETRY='1', DISABLE_ERROR_REPORTING='1')
            args = ['--model', 'claude-sonnet-4-6', '--setting-sources', 'user', '--strict-mcp-config', '--tools', '', '--disable-slash-commands', '--max-budget-usd', '0.05']
            seed = subprocess.run([cli, '--print', '--output-format', 'json', '--session-id=' + str(uuid.uuid4()), *args, 'Synthetic seed'],
                env=env, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=45, creationflags=subprocess.CREATE_NO_WINDOW)
            assert seed.returncode == 0, 'Synthetic seed CLI exit=' + str(seed.returncode)
            (root / 'environment.json').write_text(json.dumps(env), encoding='utf-8')
            (root / 'arguments.json').write_text(json.dumps(args), encoding='utf-8')
            (root / 'cli-path.txt').write_text(cli, encoding='utf-8')
            (root / '.aibot-synthetic-claude').write_text('1')
            run = subprocess.run(['dotnet', str(Path(sys.argv[1]).resolve()), '--self-test-claude-native', str(root)],
                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=100, creationflags=subprocess.CREATE_NO_WINDOW)
            print(run.stdout, end='')
            assert run.returncode == 0
            print('WINDOWS_NATIVE_WORKSPACE_API_CALLS', api.calls)
    finally:
        api.release.set()
        api.shutdown()
        api.server_close()


if __name__ == '__main__':
    main()
