"""Compile the installer against a local HTTP fixture; never install or run a runtime."""
import argparse
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import subprocess
import tempfile
import threading

ROOT = Path(__file__).resolve().parents[1]
PAYLOAD = b'AI-bot download integrity fixture; not executable\n'


def run(compiler: Path):
    state = {'mode': 'good', 'requests': 0}

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            state['requests'] += 1
            if state['mode'] == 'missing':
                self.send_error(404)
                return
            body = PAYLOAD if state['mode'] == 'good' else b'modified response'
            self.send_response(200)
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *_):
            pass

    server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        # All files (including the test-only EXE) are confined to a newly created
        # directory under artifacts, never to the app or user configuration paths.
        with tempfile.TemporaryDirectory(prefix='installer-download-test-', dir=ROOT / 'artifacts') as temp:
            work = Path(temp).resolve()
            assert work.is_relative_to((ROOT / 'artifacts').resolve())
            stage = work / 'stage'
            deps = work / 'deps'
            stage.mkdir()
            deps.mkdir()
            (stage / 'LICENSE').write_text('Local test fixture only\n')
            (stage / 'AIBotBridge.exe').write_bytes(PAYLOAD)
            (deps / 'webview2-bootstrapper.exe').write_bytes(PAYLOAD)
            command = [str(compiler), '/Q', '/DAppVersion=0.0.0', '/DDownloadProbe',
                       f'/DStageDir={stage}', f'/DDependencyDir={deps}', f'/DOutputDirPath={work}',
                       f'/DDesktopUrl=http://127.0.0.1:{server.server_port}/runtime',
                       f'/DDesktopSha256={hashlib.sha256(PAYLOAD).hexdigest()}',
                       str(ROOT / 'windows-app/installer/setup.iss')]
            subprocess.run(command, check=True, timeout=90)
            setup = work / 'AIBotBridge-0.0.0-setup-win-x64.exe'
            for mode, expected in [('good', 'DOWNLOAD_OK'), ('corrupt', 'DOWNLOAD_BLOCKED'),
                                   ('missing', 'DOWNLOAD_BLOCKED')]:
                state['mode'] = mode
                report = work / f'{mode}.txt'
                subprocess.run([str(setup), f'/DOWNLOADPROBE={report}', '/VERYSILENT',
                                '/SUPPRESSMSGBOXES', '/NORESTART'], timeout=45)
                assert report.read_text(encoding='utf-8-sig').strip() == expected, mode
            before = state['requests']
            report = work / 'environment.txt'
            subprocess.run([str(setup), f'/CHECKONLY={report}', '/VERYSILENT',
                            '/SUPPRESSMSGBOXES', '/NORESTART'], timeout=45)
            assert 'desktopInstalled=' in report.read_text(encoding='utf-8-sig')
            assert state['requests'] == before, 'Read-only detection must not download'
        print('INSTALLER_DOWNLOAD_TEST_OK valid/corrupt/404/no-download-detection; no installers executed')
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--compiler', type=Path, required=True)
    run(parser.parse_args().compiler.resolve())
