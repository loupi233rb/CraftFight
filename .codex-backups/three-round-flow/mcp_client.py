import json
import sys
import urllib.request
from pathlib import Path

SESSION = Path(__file__).with_name('mcp_session.txt')
URL = 'http://127.0.0.1:8080/mcp'


def request(payload, session=None):
    headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
    if session:
        headers['Mcp-Session-Id'] = session
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(), headers=headers)
    with urllib.request.urlopen(req, timeout=90) as response:
        raw = response.read().decode('utf-8')
        sid = response.headers.get('Mcp-Session-Id')
    if not raw.strip():
        return None, sid
    if raw.lstrip().startswith('{'):
        return json.loads(raw), sid
    for line in raw.splitlines():
        if line.startswith('data: '):
            message = json.loads(line[6:])
            if message.get('id') == payload.get('id'):
                return message, sid
    raise RuntimeError(raw[:500])


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    session = SESSION.read_text().strip() if SESSION.exists() else None
    if session is None:
        _, session = request({'jsonrpc': '2.0', 'id': 1, 'method': 'initialize', 'params': {
            'protocolVersion': '2024-11-05', 'capabilities': {},
            'clientInfo': {'name': 'codex-craftfight-flow', 'version': '1.0'}}})
        SESSION.write_text(session)
        request({'jsonrpc': '2.0', 'method': 'notifications/initialized'}, session)
    payloads = json.load(sys.stdin)
    if not isinstance(payloads, list):
        payloads = [payloads]
    for payload in payloads:
        result, _ = request({'jsonrpc': '2.0', 'id': 2, **payload}, session)
        if payload['method'] == 'tools/list' and len(sys.argv) > 1:
            names = sys.argv[1].split(',')
            result['result']['tools'] = [t for t in result['result']['tools'] if t['name'] in names]
        data = result.get('result', result)
        if 'structuredContent' in data:
            data = data['structuredContent']
        elif 'contents' in data:
            data = [json.loads(c['text']) for c in data['contents'] if 'text' in c]
        print(json.dumps(data, ensure_ascii=False))


if __name__ == '__main__':
    main()
