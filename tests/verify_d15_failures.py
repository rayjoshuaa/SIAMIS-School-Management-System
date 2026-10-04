"""Unavailable loopback SQL failure response. No SIAMIS connection or database writes."""
import pathlib, subprocess, urllib.request, urllib.error, http.cookiejar, json, time, sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
out=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
observing='--observe' in sys.argv
results=[]
for environment in ['Development','Production']:
    log=(out/('d15-unavailable-'+environment+'.log')).open('w')
    args=['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--environment',environment,'--urls','http://localhost:5157',
          '--ConnectionStrings:SIAMIS','Server=tcp:localhost,1;Database=Unused;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=1;ConnectRetryCount=0',
          '--Logging:LogLevel:Default','None','--Security:KnownProxies:0','127.0.0.1','--Security:KnownProxies:1','::1']
    p=subprocess.Popen(args,cwd=ROOT/'src/SIAMIS.Api',stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    try:
        client=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        headers={'X-Forwarded-Proto':'https'} if environment=='Production' else {}
        for n in range(100):
            try:
                with client.open(urllib.request.Request('http://localhost:5157/api/auth/csrf',headers=headers),timeout=2) as r:token=json.load(r)['token']
                break
            except (urllib.error.URLError,TimeoutError):time.sleep(.1)
        else:raise AssertionError('Isolated failure API did not start')
        # Local trusted proxy relay, only for the Production-mode HTTP test transport.
        if environment=='Production':
            jar=client.handlers[1].cookiejar if hasattr(client.handlers[1],'cookiejar') else None
            for handler in client.handlers:
                if hasattr(handler,'cookiejar'):headers['Cookie']='; '.join(c.name+'='+c.value for c in handler.cookiejar)
        headers.update({'X-CSRF-TOKEN':token,'Content-Type':'application/json','Accept':'application/json'})
        req=urllib.request.Request('http://localhost:5157/api/auth/login',data=json.dumps({'userName':'d15-unavailable','password':'invalid'}).encode(),headers=headers)
        try:
            with client.open(req,timeout=15) as r:status=r.status;body=r.read();mime=r.headers.get('Content-Type','')
        except urllib.error.HTTPError as e:status=e.code;body=e.read();mime=e.headers.get('Content-Type','')
        text=body.decode(errors='replace')
        leaked=any(x in text for x in ['SqlException','SIAMIS.Infrastructure','System.Data','ClientConnectionId','Program.cs','StackTrace','HEADERS','C:\\Users'])
        item={'environment':environment,'status':status,'contentType':mime,'leakedInternalDetails':leaked,'bodyLength':len(body)}
        if not observing:
            assert status==500 and not leaked and 'application/problem+json' in mime,item
            value=json.loads(body);assert value['status']==500 and value['title']=='An unexpected error occurred.'
            assert 'detail' not in value and 'exception' not in value
        results.append(item);print(json.dumps(item),flush=True)
    finally:
        p.terminate();p.wait(timeout=20);log.close()
(out/('d15-failure-observation.json' if observing else 'd15-failure-results.json')).write_text(json.dumps({'checks':len(results),'results':results,'error':None},indent=2))
