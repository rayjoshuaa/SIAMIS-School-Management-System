"""D10 temporary local Identity fixtures, real cookie/CSRF clients and authorization verification."""
import pathlib, json, uuid, secrets, subprocess, os, http.cookiejar, urllib.request, urllib.error, traceback
ROOT=pathlib.Path(__file__).resolve().parents[1]
helper=ROOT/'tests/verify_d5a_live.py'
exec(helper.read_text().split('baseline=snapshot()')[0].replace("ROOT=pathlib.Path(__file__).resolve().parents[1]","ROOT=pathlib.Path("+repr(str(ROOT))+")"))
BASE='http://localhost:5155';PREFIX='D10-'+uuid.uuid4().hex[:12]
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
fixtures={'users':[],'employees':[]};results=[]
password=secrets.token_urlsafe(24);new_password=secrets.token_urlsafe(24)

class Client:
    def __init__(self):
        self.jar=http.cookiejar.CookieJar();self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar));self.token=None
    def request(self,method,path,body=None,status=200,csrf=True,headers=None):
        if method not in ['GET','HEAD','OPTIONS'] and csrf:
            self.token=self.request('GET','/api/auth/csrf')['token']
        hdr={'Content-Type':'application/json',**(headers or {})}
        if self.token and csrf:hdr['X-CSRF-TOKEN']=self.token
        request=urllib.request.Request(BASE+path,data=None if body is None else json.dumps(body).encode(),method=method,headers=hdr)
        try:
            with self.opener.open(request,timeout=40) as r:code,data=r.status,r.read()
        except urllib.error.HTTPError as e:code,data=e.code,e.read()
        if code!=status:raise AssertionError(f'{method} {path}: expected {status}, got {code}; {data.decode()[:500]}')
        return json.loads(data) if data else None
    def login(self,name,pw=password):return self.request('POST','/api/auth/login',{'userName':name,'password':pw},204)

def cleanup():
    if fixtures['users']:
        ids=','.join(map(ident,fixtures['users']))
        sql(f'DELETE UserRoles WHERE UserId IN ({ids}); DELETE UserClaims WHERE UserId IN ({ids}); DELETE UserLogins WHERE UserId IN ({ids}); DELETE UserTokens WHERE UserId IN ({ids}); DELETE Users WHERE Id IN ({ids});')
    # The initial security baseline is empty apart from seeded roles. Test audit IDs are captured explicitly.
    audit_ids=[r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents') if r['Id'] not in baseline_audit_ids]
    if audit_ids:sql('DELETE SecurityAuditEvents WHERE Id IN ('+','.join(map(ident,audit_ids))+')')
    for id in fixtures['employees']:
        sql(f'DELETE EmploymentRecords WHERE EmployeeId={ident(id)}; DELETE Employees WHERE EmployeeId={ident(id)};')

baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')}
error=None
try:
    check(len(baseline)==85 and len(baseline['Roles'])==5 and not baseline['Users'],'85-table migrated baseline; five permanent roles only')
    anonymous=Client()
    for path in ['/api/employees','/api/payroll-components','/api/attendance/today','/api/admin/users','/api/leave-requests','/api/auth/me']:
        anonymous.request('GET',path,status=401);check(True,'anonymous denied '+path)
    anonymous.request('POST','/api/auth/login',{'userName':'missing','password':'invalid'},400,csrf=False);check(True,'login CSRF required')
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-admin';env['Bootstrap__Password']=password
    run=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    if run.returncode:raise AssertionError('Bootstrap failed: '+run.stderr[-1000:]+run.stdout[-1000:])
    admin_id=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-admin'")[0]['Id'];fixtures['users'].append(admin_id)
    admin=Client();admin.login(PREFIX+'-admin');me=admin.request('GET','/api/auth/me')
    check(me['requiresPasswordChange'] and not me['capabilities'],'temporary bootstrap account has no HR capabilities')
    admin.request('GET','/api/employees',status=403)
    admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    admin.request('GET','/api/employees');check(True,'password change activates explicit capabilities')
    check(not any(k in json.dumps(me).lower() for k in ['passwordhash','securitystamp','concurrencystamp']),'safe identity DTO excludes security internals')
    check(all(any(k.lower()=='httponly' for k in c._rest) for c in admin.jar),'cookies are HttpOnly')
    # Existing Employee API creates authoritative initial employment; fixtures never modify TEST-EMP-001.
    employee_ids=[]
    for suffix in ['A','B']:
        employee=admin.request('POST','/api/employees',{'employeeNumber':PREFIX+'-'+suffix,'firstName':'Security','lastName':suffix,'departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001','hireDate':'2026-09-29'},201)
        employee_ids.append(employee['employeeId']);fixtures['employees'].append(employee['employeeId'])
    clients={};user_dtos={}
    for label,role,employee in [('hr','HRAdmin',None),('payroll','PayrollAdmin',None),('management','Management',None),('a','Employee',employee_ids[0]),('b','Employee',employee_ids[1]),('disabled','Employee',None)]:
        if label=='disabled':role='Management'
        dto=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-'+label,'temporaryPassword':password,'employeeId':employee,'roles':[role]},201)
        fixtures['users'].append(dto['userId']);user_dtos[label]=dto
        client=Client();client.login(dto['userName']);client.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
        clients[label]=client
    # Role boundaries.
    for label,path,expected in [('hr','/api/payroll-components',403),('hr','/api/employees',200),('hr','/api/admin/users',403),('payroll','/api/payroll-components',200),('payroll','/api/admin/users',403),('management','/api/hr/staff-overview',200),('management','/api/hr/leave-status',200),('management','/api/employees',403),('management','/api/payroll-components',403),('a','/api/employee-payrolls',403),('a','/api/admin/users',403),('a','/api/self/profile',200),('a','/api/self/payrolls',200)]:
        clients[label].request('GET',path,status=expected);check(True,label+' boundary '+path)
    for source,target in [('a',0),('b',1)]:
        own=employee_ids[target];other=employee_ids[1-target]
        for route in ['attendance-history?from=2026-10-01&to=2026-10-02','leave','leave-balances?leaveYear=2026']:
            clients[source].request('GET',f'/api/employees/{own}/{route}')
            clients[source].request('GET',f'/api/employees/{other}/{route}',status=403);check(True,source+' IDOR isolation '+route)
        clients[source].request('POST',f'/api/employees/{own}/attendance-days/2026-10-01/finalize',{},403)
        clients[source].request('POST',f'/api/employees/{own}/leave/{uuid.uuid4()}/approve',{},403);check(True,source+' cannot finalize or approve own leave')
    check('reason' not in json.dumps(clients['management'].request('GET','/api/hr/leave-status')).lower(),'management Leave DTO has no confidential reason')
    # Status, stamp revocation and optimistic concurrency.
    disabled=admin.request('GET','/api/admin/users/'+user_dtos['disabled']['userId'])
    admin.request('PATCH','/api/admin/users/'+disabled['userId']+'/status',{'isActive':False,'version':disabled['version']})
    clients['disabled'].request('GET','/api/auth/me',status=401)
    fresh=Client();fresh.request('POST','/api/auth/login',{'userName':disabled['userName'],'password':new_password},401)
    check(True,'disabled account cannot authenticate; prior session rejected')
    admin.request('PUT','/api/admin/users/'+disabled['userId']+'/roles',{'roles':['SystemAdmin'],'version':disabled['version']},409);check(True,'stale administration version rejected')
    clients['a'].request('PUT','/api/admin/users/'+user_dtos['a']['userId']+'/roles',{'roles':['SystemAdmin'],'version':user_dtos['a']['version']},403);check(True,'self role escalation denied')
    admin.request('POST','/api/admin/users',{'userName':PREFIX+'-duplicate','temporaryPassword':password,'employeeId':employee_ids[0],'roles':['Employee']},409);check(True,'duplicate employee identity rejected')
    admin.request('POST','/api/admin/users',{'userName':PREFIX+'-unknown','temporaryPassword':password,'roles':['Principal']},400);check(True,'job title is not a security role')
    clients['a'].request('POST','/api/auth/logout',status=204);clients['a'].request('GET','/api/auth/me',status=401);check(True,'logout clears browser authentication')
    check(rows('SELECT Id FROM SecurityAuditEvents WHERE ActorUserId='+ident(admin_id)+" AND Operation='AccountCreated'")!=[],'account provisioning audit is authenticated')
    check(rows('SELECT Id FROM SecurityAuditEvents WHERE ActorUserId='+ident(admin_id)+" AND ResourceType='Employee'")!=[],'employee mutation audit has authenticated actor')
    # Do not expose credentials; runtime fixture metadata remains in memory only.
    swagger=anonymous.request('GET','/swagger/v1/swagger.json')
    check('/api/auth/login' in swagger['paths'] and '/api/auth/me' in swagger['paths'],'Swagger login/current-user contracts')
    check('401' in swagger['paths']['/api/employees']['get']['responses'] and '403' in swagger['paths']['/api/employees']['get']['responses'],'Swagger authorization codes')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:cleanup();check(snapshot()==baseline,'exact migrated Development baseline after temporary users/employees/audit cleanup')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d10-security-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS:',len(results),'D10 live security assertions; baseline restored',flush=True)
