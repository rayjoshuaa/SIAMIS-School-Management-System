"""Read-only anonymous enforcement and Swagger contract coverage for every documented HR operation."""
import pathlib, re, json, urllib.request, urllib.error
ROOT=pathlib.Path(__file__).resolve().parents[1]
BASE='http://localhost:5155'
swagger=json.load(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json'))
checks=[]
for template,operations in swagger['paths'].items():
    if not template.startswith('/api/') or template.startswith('/api/auth/'):continue
    def argument(match):
        key=match.group(1).lower()
        if key in ['date','businessdate']:return '2030-01-07'
        if 'year' in key:return '2030'
        return '00000000-0000-0000-0000-000000000001'
    path=re.sub(r'\{([^}]+)\}',argument,template)
    for method,contract in operations.items():
        if method not in ['get','post','put','patch','delete','head']:continue
        request=urllib.request.Request(BASE+path,method=method.upper(),data=b'{}' if method not in ['get','head'] else None,headers={'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(request) as response:status=response.status
        except urllib.error.HTTPError as error:status=error.code
        if status!=401:raise AssertionError(method+' '+template+' anonymous status '+str(status))
        if not {'401','403'}<=set(contract['responses']):raise AssertionError('Swagger security codes missing '+template)
        checks.append(method+' '+template)
with urllib.request.urlopen(BASE+'/swagger/index.html') as response:html=response.read().decode()
with urllib.request.urlopen(BASE+'/swagger/index.js') as response:initializer=response.read().decode()
if 'index.js' not in html or '/api/auth/csrf' not in initializer or 'X-CSRF-TOKEN' not in initializer:raise AssertionError('Swagger UI missing real cookie CSRF interceptor')
out=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d10-route-security-results.json'
out.write_text(json.dumps({'checks':len(checks)+1,'operations':checks,'swaggerCsrfInterceptor':True,'error':None},indent=2),encoding='utf-8')
print('PASS:',len(checks),'documented HR operations return anonymous 401 and advertise 401/403; Swagger cookie/CSRF interceptor present')
