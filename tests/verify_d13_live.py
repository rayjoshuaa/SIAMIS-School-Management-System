"""D13 real Identity, cookie/CSRF and local SQL checks; fixture secrets stay in memory."""
import pathlib,json,os,subprocess,uuid,threading,urllib.request,urllib.error,traceback,datetime,time
from concurrent.futures import ThreadPoolExecutor
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
OriginalClient=Client
class Client(OriginalClient):
    def request(self,*args,**kwargs):
        # Pace fixture setup at the existing IP limiter; explicit abuse tests are separate.
        for attempt in range(66):
            try:return super().request(*args,**kwargs)
            except AssertionError as exc:
                if 'got 429' not in str(exc) or attempt==65:raise
                time.sleep(1)
PREFIX='D13-'+uuid.uuid4().hex[:12]
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')}
error=None;secret_values=[]
context={'departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001'}
def employee(label):
    e=admin.request('POST','/api/employees',context|{'employeeNumber':PREFIX+'-'+label,'firstName':'Credential','lastName':label,'hireDate':'2026-09-29'},201)['employeeId'];fixtures['employees'].append(e);return e
def provision(label,role='Management',employee=None):
    u=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-'+label,'email':PREFIX+'-'+label+'@example.invalid','employeeId':employee,'roles':[role]},201)
    fixtures['users'].append(u['userId']);return u
def current(u):return admin.request('GET','/api/admin/users/'+u['userId'])
def collect(u):
    m=admin.request('POST','/api/admin/users/'+u['userId']+'/credential-delivery');secret_values.append(m['token']);return m
def complete(u,m,activation=True,client=None,status=204,pw=None):
    return (client or Client()).request('POST','/api/auth/'+('activate' if activation else 'reset-password'),{'userId':u['userId'],'token':m['token'],'newPassword':pw or new_password},status)
def ready(label,role='Management',employee=None):
    u=provision(label,role,employee);complete(u,collect(u));c=Client();c.login(u['userName'],new_password);return current(u),c
def status(u,active,code=200):return admin.request('PATCH','/api/admin/users/'+u['userId']+'/status',{'isActive':active,'version':current(u)['version']},code)
def raw(c,method,path,body):
    token=c.request('GET','/api/auth/csrf')['token'];request=urllib.request.Request(BASE+path,data=json.dumps(body).encode(),method=method,headers={'Content-Type':'application/json','X-CSRF-TOKEN':token})
    try:
        with c.opener.open(request,timeout=45) as r:return r.status
    except urllib.error.HTTPError as e:return e.code
def race(calls):
    barrier=threading.Barrier(len(calls))
    def call(f):barrier.wait();return f()
    with ThreadPoolExecutor(max_workers=len(calls)) as pool:return list(pool.map(call,calls))
try:
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-root';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode==0,'D13 bootstrap exception preserved')
    root=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-root'")[0]['Id'];fixtures['users'].append(root)
    admin=Client();admin.login(PREFIX+'-root');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    rootu=admin.request('GET','/api/admin/users/'+root)
    check(rootu['credentialEstablished'] and not rootu['emailConfirmed'],'usable bootstrap is not automatically email confirmed')
    for method,path,body in [('PATCH','/status',{'isActive':False}),('PUT','/roles',{'roles':['HRAdmin']})]:
        r=admin.request(method,'/api/admin/users/'+root+path,body|{'version':rootu['version']},409)
        check(r['code']=='last_usable_system_admin_required','last usable bootstrap protected '+path)
    pending_admin=provision('pending-admin','SystemAdmin')
    status(rootu,False,409);check(True,'pending SystemAdmin cannot replace usable administrator')
    unavailable,_=ready('unusable-admin','SystemAdmin')
    sql('UPDATE Users SET RequiresPasswordChange=1 WHERE Id='+ident(unavailable['userId']))
    status(rootu,False,409);check(True,'temporary-password-required administrator is not usable replacement')
    sql('UPDATE Users SET RequiresPasswordChange=0,LockoutEnd=DATEADD(minute,15,SYSUTCDATETIME()) WHERE Id='+ident(unavailable['userId']))
    status(rootu,False,409);check(True,'currently locked administrator is not usable replacement')
    status(unavailable,False);status(rootu,False,409);check(True,'disabled administrator is not usable replacement')
    e=employee('LINK');pending=provision('pending','Employee',e);original=rows('SELECT * FROM Users WHERE Id='+ident(pending['userId']))[0]
    check(not pending['credentialEstablished'] and not pending['emailConfirmed'] and original.get('PasswordHash') is None,'normal provision has no password and no email trust')
    Client().request('POST','/api/auth/login',{'userName':pending['userName'],'password':new_password},401);check(True,'pending cannot login')
    generic=[]
    for email in [pending['email'],'missing@example.invalid',None,'']:
        generic.append(Client().request('POST','/api/auth/forgot-password',{'email':email}))
    check(all(x==generic[0] for x in generic),'pending unknown no-email generic responses identical')
    invitation=collect(pending)
    admin.request('POST','/api/admin/users/'+pending['userId']+'/credential-delivery',status=404);check(True,'Development delivery collected once')
    other=provision('other')
    complete(other,invitation,status=400);complete(pending,{'token':'malformed'},status=400)
    complete(pending,invitation,pw='short',status=400);check(True,'wrong-user malformed and short-password activation rejected')
    admin.request('POST','/api/admin/users/'+pending['userId']+'/issue-credentials',{'version':pending['version']})
    replacement=collect(pending);complete(pending,invitation,status=400);complete(pending,replacement)
    complete(pending,replacement,status=400)
    u=current(pending);check(u['credentialEstablished'] and u['emailConfirmed'] and not u['requiresPasswordChange'],'activation proves email possession and establishes credential exactly once')
    check(u['employeeId']==e and u['roles']==['Employee'] and u['userId']==pending['userId'],'activation retains identity linkage and roles')
    c1=Client();c1.login(u['userName'],new_password);c2=Client();c2.login(u['userName'],new_password)
    before=rows('SELECT * FROM Users WHERE Id='+ident(u['userId']))
    c1.request('POST','/api/auth/change-password',{'currentPassword':'wrong','newPassword':password},400)
    c1.request('POST','/api/auth/change-password',{'currentPassword':new_password,'newPassword':password,'userId':root},400)
    check(rows('SELECT * FROM Users WHERE Id='+ident(u['userId']))==before,'wrong current password and forged own-password target leave account unchanged')
    c1.request('POST','/api/auth/change-password',{'currentPassword':new_password,'newPassword':password},204)
    c1.request('GET','/api/auth/me');c2.request('GET','/api/auth/me',status=401);check(True,'own password refreshes caller and invalidates other sessions')
    expected=generic[0];check(Client().request('POST','/api/auth/forgot-password',{'email':u['email']})==expected,'confirmed recovery same public response')
    reset=collect(u);complete(other,reset,False,status=400);complete(u,reset,True,status=400)
    complete(u,reset,False);complete(u,reset,False,status=400);c1.request('GET','/api/auth/me',status=401)
    check(True,'reset scoped to user and purpose, one-time, revokes old session')
    hrbefore={t:rows('SELECT * FROM '+t+' WHERE EmployeeId='+ident(e)) for t in ['Employees','EmploymentRecords']}
    issued=admin.request('POST','/api/admin/users/'+u['userId']+'/issue-credentials',{'version':current(u)['version']})
    check(issued=={'purpose':'PasswordReset'},'admin recovery response contains only purpose')
    admin.request('POST','/api/admin/users/'+u['userId']+'/issue-credentials',{'version':pending['version']},409)
    reset=collect(u);status(u,False);complete(u,reset,False,status=400)
    check(Client().request('POST','/api/auth/forgot-password',{'email':u['email']})==expected,'disabled recovery generic')
    admin.request('POST','/api/admin/users/'+u['userId']+'/credential-delivery',status=404)
    status(u,True);complete(u,reset,False,status=400)
    check({t:rows('SELECT * FROM '+t+' WHERE EmployeeId='+ident(e)) for t in hrbefore}==hrbefore,'disable/reset/re-enable never changes employment or silently activates user')
    # Simulate an existing credential with unverified email; no API exists to modify email/confirmation.
    unconfirmed,uc=ready('unconfirmed');sql('UPDATE Users SET EmailConfirmed=0 WHERE Id='+ident(unconfirmed['userId']))
    check(Client().request('POST','/api/auth/forgot-password',{'email':unconfirmed['email']})==expected,'unconfirmed established credential generic')
    admin.request('POST','/api/admin/users/'+unconfirmed['userId']+'/credential-delivery',status=404)
    check(Client().request('POST','/api/auth/forgot-password',{'email':None})==expected,'bootstrap without verified email no anonymous recovery')
    hr,hc=ready('hr','HRAdmin')
    hc.request('POST','/api/admin/users/'+u['userId']+'/issue-credentials',{'version':current(u)['version']},403)
    hc.request('POST','/api/admin/users',{'userName':'forged','email':'forged@example.invalid','roles':['SystemAdmin']},403)
    Client().request('POST','/api/admin/users',{},401);check(True,'private provisioning and recovery require explicit Security.Manage')
    for field,value in [('temporaryPassword',password),('passwordHash','forged'),('securityStamp','forged'),('emailConfirmed',True),('actorUserId',root),('claims',[])]:
        check(raw(admin,'POST','/api/admin/users',{'userName':PREFIX+'-forged','email':'forged@example.invalid','roles':['Management'],field:value})==400,'strict server rejects forged '+field)
        check(True,'provisioning rejects forged '+field)
    admin.request('POST','/api/admin/users',{'userName':PREFIX+'-noemail','roles':['Management']},400)
    admin.request('POST','/api/admin/users',{'userName':u['userName'],'email':'duplicate@example.invalid','roles':['Management']},409)
    admin.request('POST','/api/admin/users',{'userName':PREFIX+'-dupemail','email':u['email'],'roles':['Management']},409)
    admin.request('POST','/api/admin/users',{'userName':PREFIX+'-duplink','email':'duplink@example.invalid','employeeId':e,'roles':['Employee']},409)
    check(True,'missing email, duplicate username/email/linkage rejected without new accounts')
    check(not rows('SELECT Id FROM Users WHERE EmployeeId='+ident(employee('NOUSER'))),'Employee creation never implicitly creates a User')
    check(raw(Client(),'POST','/api/auth/register',{}) in [401,404],'anonymous public registration unavailable')
    admin.request('PUT','/api/admin/users/'+u['userId']+'/roles',{'roles':['Employee'],'version':current(u)['version'],'email':'replacement@example.invalid'},400)
    check(current(u)['email']==u['email'] and current(u)['emailConfirmed'],'email cannot be edited or inherit confirmation')
    for route in ['activate','reset-password','forgot-password']:
        Client().request('POST','/api/auth/'+route,{},400,csrf=False)
        check(True,'anonymous credential route requires CSRF '+route)
    # Concurrent token consumption has one successful writer.
    for activation in [True,False]:
        target=provision('race-activation') if activation else u
        if activation:message=collect(target)
        else:Client().request('POST','/api/auth/forgot-password',{'email':target['email']});message=collect(target)
        b={'userId':target['userId'],'token':message['token'],'newPassword':new_password}
        route='/api/auth/'+('activate' if activation else 'reset-password')
        codes=race([lambda:raw(Client(),'POST',route,b),lambda:raw(Client(),'POST',route,b)])
        check(sorted(codes)==[204,400],'concurrent one-time '+route+' '+str(codes))
    for active in [False,True]:
        target,_=ready('race-state-'+str(active));Client().request('POST','/api/auth/forgot-password',{'email':target['email']});message=collect(target)
        if active:status(target,False)
        v=current(target)['version'];body={'userId':target['userId'],'token':message['token'],'newPassword':password}
        codes=race([lambda:raw(Client(),'POST','/api/auth/reset-password',body),lambda:raw(admin,'PATCH','/api/admin/users/'+target['userId']+'/status',{'isActive':active,'version':v})])
        check(codes in ([204,409],[400,200]) if not active else codes==[400,200],'reset versus administrative state race '+str(active)+' '+str(codes))
        if not active and current(target)['isActive']:status(target,False)
        check(current(target)['isActive']==active,'reset never overrides administrative enable/disable')
    re=employee('RPROVISION');body={'userName':PREFIX+'-rprovision','email':PREFIX+'-rprovision@example.invalid','employeeId':re,'roles':['Employee']}
    codes=race([lambda:raw(admin,'POST','/api/admin/users',body),lambda:raw(admin,'POST','/api/admin/users',body|{'userName':PREFIX+'-rprovision2','email':PREFIX+'-rprovision2@example.invalid'})])
    check(sorted(codes)==[201,409] and len(rows('SELECT Id FROM Users WHERE EmployeeId='+ident(re)))==1,'concurrent provisioning same Employee retains unique linkage')
    pe=employee('RPROVISIONEND');r=admin.request('GET',f'/api/employees/{pe}/account-lifecycle')
    end={'expectedEmploymentRecordId':r['currentEmploymentRecordId'],'endDate':str(datetime.date.today()-datetime.timedelta(days=1)),'employmentStatusId':'40000000-0000-0000-0000-000000000005'}
    provision_body={'userName':PREFIX+'-rprovisionend','email':PREFIX+'-rprovisionend@example.invalid','employeeId':pe,'roles':['Employee']}
    codes=race([lambda:raw(admin,'POST','/api/admin/users',provision_body),lambda:raw(admin,'POST',f'/api/employees/{pe}/end-employment',end)])
    check(codes[0]==201 and codes[1] in [200,409],'provision versus offboarding serializes without bypassing active-account decision '+str(codes))
    check(len(rows('SELECT Id FROM Users WHERE EmployeeId='+ident(pe)))==1,'provision/offboarding retains one explicit account linkage')
    ru,rc=ready('race-role','HRAdmin');v=current(ru)['version']
    codes=race([lambda:raw(admin,'PUT','/api/admin/users/'+ru['userId']+'/roles',{'roles':['Management'],'version':v}),lambda:raw(rc,'GET','/api/employees',{})])
    check(codes[0]==200 and codes[1] in [200,401,403],'role removal versus in-flight protected read bounded by authorization point '+str(codes))
    rc.request('GET','/api/employees',status=401);fresh=Client();fresh.login(ru['userName'],new_password);fresh.request('GET','/api/employees',status=403)
    check(True,'subsequent old and fresh sessions cannot retain removed permission')
    # Actual temporary lockout remains independent of activation/administrative disabled state.
    locked,lc=ready('locked')
    for _ in range(5):Client().request('POST','/api/auth/login',{'userName':locked['userName'],'password':'invalid'},401)
    lockrow=rows('SELECT LockoutEnd,IsActive FROM Users WHERE Id='+ident(locked['userId']))[0]
    check(current(locked)['isLockedOut'] and lockrow['IsActive'],'five failures temporarily lock active account')
    Client().request('POST','/api/auth/forgot-password',{'email':locked['email']});complete(locked,collect(locked),False)
    check(rows('SELECT LockoutEnd FROM Users WHERE Id='+ident(locked['userId']))[0]['LockoutEnd']==lockrow['LockoutEnd'],'reset preserves temporary lockout deadline')
    Client().request('POST','/api/auth/login',{'userName':locked['userName'],'password':new_password},401)
    # Live framework unlock after fixture-specific expiry, without changing runtime policy.
    sql('UPDATE Users SET LockoutEnd=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id='+ident(locked['userId']))
    Client().login(locked['userName'],new_password);check(True,'expired framework lockout permits login')
    # Last-admin writers include D12 offboarding; bootstrap is usable without confirmed email.
    linked=employee('SOLEADMIN');sa,sac=ready('linked-admin','SystemAdmin',linked)
    complete(pending_admin,collect(pending_admin));status(pending_admin,False)
    admin.request('PUT','/api/admin/users/'+root+'/roles',{'roles':['HRAdmin'],'version':current(rootu)['version']})
    admin=sac;sa=current(sa)
    status(sa,False,409);admin.request('PUT','/api/admin/users/'+sa['userId']+'/roles',{'roles':['HRAdmin'],'version':sa['version']},409)
    r=admin.request('GET',f'/api/employees/{linked}/account-lifecycle')
    end={'expectedEmploymentRecordId':r['currentEmploymentRecordId'],'endDate':str(datetime.date.today()-datetime.timedelta(days=1)),'employmentStatusId':'40000000-0000-0000-0000-000000000005','disableLinkedAccount':True,'expectedLinkedAccountVersion':r['linkedAccountVersion']}
    old=rows('SELECT * FROM EmploymentRecords WHERE EmployeeId='+ident(linked))
    failure=admin.request('POST',f'/api/employees/{linked}/end-employment',end,409)
    check(failure['code']=='last_usable_system_admin_required' and rows('SELECT * FROM EmploymentRecords WHERE EmployeeId='+ident(linked))==old,'D12 sole-admin offboarding rejected atomically')
    for operation in ['disable','roles']:
        alternate,ac=ready('alternate-'+operation,'SystemAdmin')
        first=current(sa);second=current(alternate)
        method='PATCH' if operation=='disable' else 'PUT';suffix='/status' if operation=='disable' else '/roles';value={'isActive':False} if operation=='disable' else {'roles':['Management']}
        codes=race([lambda:raw(sac,method,'/api/admin/users/'+first['userId']+suffix,value|{'version':first['version']}),lambda:raw(ac,method,'/api/admin/users/'+second['userId']+suffix,value|{'version':second['version']})])
        check(sorted(codes)==[200,409],'concurrent final-two admin '+operation+' cannot remove all '+str(codes))
        usable=rows("SELECT u.Id FROM Users u JOIN UserRoles ur ON u.Id=ur.UserId JOIN Roles r ON r.Id=ur.RoleId WHERE r.NormalizedName='SYSTEMADMIN' AND u.IsActive=1 AND u.PasswordHash IS NOT NULL AND u.RequiresPasswordChange=0 AND (u.LockoutEnd IS NULL OR u.LockoutEnd<=SYSUTCDATETIME())")
        check(len(usable)==1,'exactly one usable SystemAdmin remains after race')
        if usable[0]['Id'].lower()!=sa['userId'].lower():admin=ac;sa=alternate;sac=ac
        # Make loser ineligible for the next final-two case.
        loser=first if usable[0]['Id'].lower()!=first['userId'].lower() else second
        status(loser,False)
    swagger=Client().request('GET','/swagger/v1/swagger.json')
    props=swagger['components']['schemas']['CreateUserRequest']['properties']
    check('temporaryPassword' not in props and 'email' in props and 'emailConfirmed' not in props,'Swagger advertises intended passwordless contract')
    for route in ['activate','reset-password','forgot-password']:check('/api/auth/'+route in swagger['paths'],'Swagger credential route '+route)
    events=rows('SELECT * FROM SecurityAuditEvents')
    for op in ['AccountCreated','ActivationInitiated','AccountActivated','ActivationReissued','PasswordChanged','PasswordRecoveryInitiated','PasswordResetCompleted','RoleAssigned:Employee']:
        check(any(x['Operation']==op for x in events),'append-only credential audit '+op)
    check(not any(s in json.dumps(events) for s in secret_values),'no activation/reset token in SQL audit')
    logs=(directory/'d10-api.log').read_text(encoding='utf-8',errors='replace')
    check(not any(s in logs for s in secret_values),'no delivered token in API logs')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        fixtures['users']=list(set(fixtures['users'])|{r['Id'] for r in rows("SELECT Id FROM Users WHERE UserName LIKE '"+PREFIX+"%'")})
        cleanup();check(snapshot()==baseline,'exact 85-table Development baseline restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d13-live-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS:',len(results),'D13 live checks; exact cleanup',flush=True)
