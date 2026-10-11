import json,sys,pathlib,requests
ROOT=pathlib.Path(__file__).parent
URL='http://127.0.0.1:8080/mcp'
HEADERS={'Accept':'application/json, text/event-stream'}
def send(method,params=None,ident=1):
    payload={'jsonrpc':'2.0','method':method}
    if ident is not None: payload['id']=ident
    if params is not None: payload['params']=params
    response=requests.post(URL,headers=HEADERS,json=payload,timeout=180)
    response.raise_for_status()
    if response.headers.get('mcp-session-id'): HEADERS['mcp-session-id']=response.headers['mcp-session-id']
    if not response.text: return {}
    if response.text.lstrip().startswith('{'): return response.json()
    for line in response.text.splitlines():
        if line.startswith('data: '):
            item=json.loads(line[6:])
            if item.get('id')==ident: return item
    return {'raw':response.text[:1000]}
send('initialize',{'protocolVersion':'2024-11-05','capabilities':{},'clientInfo':{'name':'codex-migration','version':'1'}})
send('notifications/initialized',ident=None)
if sys.argv[1]=='list':
    result=send('tools/list');(ROOT/'mcp-tools.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps([{'name':t['name'],'schema':t['inputSchema']} for t in result.get('result',{}).get('tools',[])],ensure_ascii=False))
elif sys.argv[1]=='resources': print(json.dumps(send('resources/list'),ensure_ascii=False))
elif sys.argv[1]=='resource': print(json.dumps(send('resources/read',{'uri':sys.argv[2]}),ensure_ascii=False))
else:
    args=json.loads(pathlib.Path(sys.argv[2]).read_text(encoding='utf-8-sig')) if len(sys.argv)>2 else {}
    result=send('tools/call',{'name':sys.argv[1],'arguments':args})
    (ROOT/'mcp-last-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
