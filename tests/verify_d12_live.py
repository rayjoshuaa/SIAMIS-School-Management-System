"""D12 real Identity/offboarding races and historical preservation. Local Development only.
Fixture IDs are explicit; cleanup compares every original row and timestamp.
"""
import pathlib, datetime, threading
from concurrent.futures import ThreadPoolExecutor
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
PREFIX='D12-'+uuid.uuid4().hex[:6]
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')}
error=None;domain=None;pay=None;organization=None;historical=None
today=datetime.datetime.now(datetime.timezone.utc).date();yesterday=today-datetime.timedelta(days=1)
active='40000000-0000-0000-0000-000000000001';resigned='40000000-0000-0000-0000-000000000005'
context={'departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':active}

def employee(label):
    e=admin.request('POST','/api/employees',context|{'employeeNumber':PREFIX+'-'+label,'firstName':'Synthetic','lastName':'D12','hireDate':str(today-datetime.timedelta(days=60))},201)['employeeId']
    fixtures['employees'].append(e);return e
def readiness(e):return admin.request('GET',f'/api/employees/{e}/account-lifecycle')
def account(label,e=None,role='Employee'):
    u=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-'+label,'temporaryPassword':password,'employeeId':e,'roles':[role]},201)
    fixtures['users'].append(u['userId']);c=Client();c.login(u['userName']);c.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    return admin.request('GET','/api/admin/users/'+u['userId']),c
def user(u):return admin.request('GET','/api/admin/users/'+u['userId'])
def end_body(e,choice=None):
    r=readiness(e);b={'expectedEmploymentRecordId':r['currentEmploymentRecordId'],'endDate':str(yesterday),'employmentStatusId':resigned}
    if choice is not None:b|={'disableLinkedAccount':choice,'expectedLinkedAccountVersion':r['linkedAccountVersion']}
    return b
def end(e,b=None,status=200,client=None):return (client or admin).request('POST',f'/api/employees/{e}/end-employment',b or end_body(e),status)
def state(e):
    return {t:rows(f'SELECT * FROM {t} WHERE EmployeeId={ident(e)}') for t in ['Employees','EmploymentRecords','Users']}
def rehire(e,status=201,client=None):return (client or admin).request('POST',f'/api/employees/{e}/rehire',context|{'hireDate':str(today)},status)
def clone(c):
    x=Client();x.jar=c.jar;x.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(x.jar));return x
def raw(c,m,p,b):
    token=c.request('GET','/api/auth/csrf')['token']
    req=urllib.request.Request(BASE+p,data=json.dumps(b).encode(),method=m,headers={'Content-Type':'application/json','X-CSRF-TOKEN':token})
    try:
        with c.opener.open(req,timeout=45) as r:return r.status
    except urllib.error.HTTPError as x:return x.code
def race(calls):
    barrier=threading.Barrier(len(calls))
    def run(f):barrier.wait();return f()
    with ThreadPoolExecutor(max_workers=len(calls)) as pool:return list(pool.map(run,calls))

try:
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-admin';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode==0,'temporary authorized bootstrap')
    actor=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-admin'")[0]['Id'];fixtures['users'].append(actor)
    admin=Client();admin.login(PREFIX+'-admin');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    hr,hrc=account('hr',None,'HRAdmin')
    e=employee('NOUSER');r=readiness(e);check(not r['accountLinked'] and not r['requiresOffboardingDecision'],'no linked user readiness')
    end(e,client=hrc);check(not readiness(e)['hasCurrentEmployment'] and not state(e)['Users'],'HR end without creating fake user')
    e=employee('DISABLED');u,c=account('disabled',e)
    admin.request('PATCH','/api/admin/users/'+u['userId']+'/status',{'isActive':False,'version':u['version']})
    previous=state(e)['Users'];end(e,client=hrc)
    check(state(e)['Users']==previous,'already disabled user untouched, including stamps and versions')
    e=employee('ACTIVE');u,c=account('active',e);initial=state(e);b=end_body(e)
    failure=end(e,b,409);check(failure['code']=='active_linked_account_requires_offboarding_decision' and state(e)==initial,'omitted decision stable 409 and atomic no mutation')
    for choice in [True,False]:
        end(e,end_body(e,choice),403,hrc);check(state(e)==initial,'HRAdmin cannot resolve security access '+str(choice))
    b=end_body(e,True);b.pop('expectedLinkedAccountVersion');end(e,b,409);check(state(e)==initial,'missing account concurrency version rejected')
    b=end_body(e,True)|{'expectedLinkedAccountVersion':str(uuid.uuid4())};end(e,b,409);check(state(e)==initial,'stale account concurrency version rejected')
    b=end_body(e,True)|{'actorUserId':str(uuid.uuid4())};end(e,b,400);check(state(e)==initial,'forged body actor rejected')
    b=end_body(e,True)|{'endDate':str(today+datetime.timedelta(days=1))};end(e,b,400);check(state(e)==initial,'future end rejected before any disable')
    c.request('POST',f'/api/employees/{e}/end-employment',end_body(e,False),403)
    c.request('POST',f'/api/employees/{e}/rehire',context|{'hireDate':str(today)},403)
    c.request('PATCH','/api/admin/users/'+u['userId']+'/status',{'isActive':True,'version':u['version']},403);check(True,'employee cannot self end, rehire, retain or reactivate')
    old_record=end_body(e,True)['expectedEmploymentRecordId'];roles=rows('SELECT * FROM UserRoles WHERE UserId='+ident(u['userId']))
    admin.request('POST',f'/api/employees/{e}/end-employment',end_body(e,True),headers={'ActorUserId':str(uuid.uuid4())})
    ended=state(e);check(not ended['Users'][0]['IsActive'] and not ended['Employees'][0]['IsActive'] and not ended['EmploymentRecords'][0]['IsCurrent'],'normal offboarding ends HR and disables account')
    check(ended['Users'][0]['SecurityStamp']!=initial['Users'][0]['SecurityStamp'] and roles==rows('SELECT * FROM UserRoles WHERE UserId='+ident(u['userId'])),'disable rotates stamp while retaining roles and identity')
    c.request('GET','/api/self/profile',status=401)
    Client().request('POST','/api/auth/login',{'userName':u['userName'],'password':new_password},401);check(True,'existing session rejected next request and new login denied')
    rehire(e);check(not state(e)['Users'][0]['IsActive'] and len(state(e)['EmploymentRecords'])==2,'rehire keeps same employee and disabled user with sequential history')
    end(e,{'expectedEmploymentRecordId':old_record,'endDate':str(today),'employmentStatusId':resigned},409);check(readiness(e)['hasCurrentEmployment'],'stale old termination cannot end rehire')
    v=user(u);before_hr=state(e)['EmploymentRecords'];admin.request('PATCH','/api/admin/users/'+u['userId']+'/status',{'isActive':True,'version':v['version']})
    check(state(e)['EmploymentRecords']==before_hr and state(e)['Users'][0]['Id'].lower()==u['userId'].lower(),'explicit reactivation retains user and employment')
    c.request('GET','/api/self/profile',status=401);fresh=Client();fresh.login(u['userName'],new_password);fresh.request('GET','/api/self/profile');check(True,'reactivation requires fresh session; old cookie remains revoked')
    e=employee('RETAIN');u,c=account('retain',e);s=state(e)['Users'][0];end(e,end_body(e,False))
    now=state(e)['Users'][0];check(now['IsActive'] and now['SecurityStamp']==s['SecurityStamp'] and now['AdministrationVersion']!=s['AdministrationVersion'],'explicit retention preserves access and stamp; versions decision')
    c.request('GET','/api/self/profile');check(not readiness(e)['hasCurrentEmployment'],'retained user can use existing D10 capabilities independently of ended employment')
    old_hr=state(e)['EmploymentRecords'];v=user(u)
    admin.request('PATCH','/api/admin/users/'+u['userId']+'/status',{'isActive':False,'version':v['version']})
    v=user(u);admin.request('PATCH','/api/admin/users/'+u['userId']+'/status',{'isActive':True,'version':v['version']})
    check(state(e)['EmploymentRecords']==old_hr and not readiness(e)['hasCurrentEmployment'],'explicit enable after termination does not recreate employment')
    fresh=Client();fresh.login(u['userName'],new_password);fresh.request('GET','/api/self/profile');check(True,'explicitly reactivated ended employee uses existing D10 access contract')
    e=employee('RLOGIN');u,c=account('rlogin',e)
    login_client=Client()
    codes=race([lambda:raw(clone(admin),'PATCH','/api/admin/users/'+u['userId']+'/status',{'isActive':False,'version':u['version']}),lambda:raw(login_client,'POST','/api/auth/login',{'userName':u['userName'],'password':new_password})])
    check(codes[0]==200 and codes[1] in [204,401],'disable vs login resolves without server error '+str(codes))
    c.request('GET','/api/self/profile',status=401);check(True,'session reuse after racing disable denied')
    login_client.request('GET','/api/self/profile',status=401);check(True,'cookie from racing login cannot authorize access after disable')
    e=employee('SELFADMIN');u,c=account('selfadmin',e,'SystemAdmin');end(e,end_body(e,True),client=c)
    c.request('GET','/api/auth/me',status=401)
    check(not state(e)['Users'][0]['IsActive'],'authorized linked SystemAdmin offboarding refreshes tracked actor and revokes session')
    # Each transaction serializes on Employee then User; expected versions make races visible.
    for label in ['RDIS','REN','RTWO']:
        e=employee(label);u,c=account(label.lower(),e);b=end_body(e,True)
        if label=='RTWO':
            codes=race([lambda:raw(clone(admin),'POST',f'/api/employees/{e}/end-employment',b),lambda:raw(clone(admin),'POST',f'/api/employees/{e}/end-employment',b)])
            check(sorted(codes)==[200,409],'two simultaneous termination requests serialize, one conflict')
        else:
            target=label=='REN';path='/api/admin/users/'+u['userId']+'/status';status_body={'isActive':target,'version':u['version']}
            codes=race([lambda:raw(clone(admin),'POST',f'/api/employees/{e}/end-employment',b),lambda:raw(clone(admin),'PATCH',path,status_body)])
            check(all(x in [200,409] for x in codes) and 200 in codes,'termination vs '+label+' has no deadlock/500 '+str(codes))
            if target:check(codes.count(200)==1,'termination vs enable rejects stale competing decision')
            else:check(not state(e)['Users'][0]['IsActive'] and not readiness(e)['hasCurrentEmployment'],'termination vs disable final state coherent')
        if not readiness(e)['hasCurrentEmployment']:
            codes=race([lambda:raw(clone(admin),'POST',f'/api/employees/{e}/end-employment',b),lambda:raw(clone(admin),'POST',f'/api/employees/{e}/rehire',context|{'hireDate':str(today)})])
            check(codes==[409,201],'stale termination vs rehire cannot target new current record')
    # Historical Leave, Attendance, calendar and Payroll fixture uses existing helpers and calculators.
    path=ROOT/'tests/verify_d8d_live.py';domain={'__file__':str(path)};exec(path.read_text().split('\ntry:\n')[0],domain)
    domain['PREFIX']=PREFIX+'-';domain['api']=lambda m,p,b=None,status=200:admin.request(m,'/api/'+p,b,status)
    historical=domain['employee']('HISTORY',hire='2026-08-01',start=None)
    cal=domain['calendar']('CAL',[('08:00','16:00')]);domain['assign'](historical,cal,start='2026-01-01',end='2031-12-31')
    domain['policy'](domain['annual'],'POL',True,start='2026-01-01',end='2031-12-31')
    domain['entitlement'](historical,year=2026,minutes=10000);domain['entitlement'](historical,year=2030,minutes=10000)
    l=domain['create'](historical,domain['request']('2026-09-29'),480,'historical Leave');domain['command'](historical,l,'approve')
    domain['create'](historical,domain['request']('2030-01-07'),480,'future Pending Leave')
    for direction,time in [('In','08:00'),('Out','16:00')]:admin.request('POST',f'/api/employees/{historical}/attendance-events/manual',{'occurredAt':'2026-09-30T'+time+':00+07:00','direction':direction,'manualRequestKey':str(uuid.uuid4()),'reason':'D12 synthetic history'},201)
    r=admin.request('GET',f'/api/employees/{historical}/attendance-days/2026-09-30/review')
    admin.request('POST',f'/api/employees/{historical}/attendance-days/2026-09-30/finalize',{'expectedVersion':r['version'],'expectedSourceFingerprint':r['sourceFingerprint'],'reason':'D12 historical preservation'},201)
    path=ROOT/'tests/verify_d5a_live.py';pay={'__file__':str(path)};exec(path.read_text().split('baseline=snapshot()')[0],pay)
    pay['EMP']=historical;pay['PREFIX']=PREFIX+'-';pay['api']=domain['api'];pay['pit_opt_out']()
    ss=domain['api']('POST','statutory-schemes',{'code':'TH-SSO-33','name':PREFIX+'Synthetic opt out','jurisdiction':'TH','schemeType':'SocialSecurity'},201);pay['fixtures']['schemes'].append(ss['statutorySchemeId'])
    en=domain['api']('POST',f'employees/{historical}/statutory-enrollments',{'statutorySchemeId':ss['statutorySchemeId'],'effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','applicability':'NotApplicable'},201);pay['fixtures']['enrollments'].append(en['employeeStatutoryEnrollmentId'])
    comp=domain['api']('POST',f'employees/{historical}/compensations',{'payTypeId':rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id'],'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-08-01','isCurrent':True,'remarks':'D12 synthetic'},201);pay['fixtures']['compensations'].append(comp['compensationId'])
    organization=domain['api']('PUT','organization-profile',{'displayName':PREFIX+'Synthetic Employer'})['organizationProfileId']
    period=pay['period'](9);g=pay['generate'](period);check(g['status']=='Generated','historical Payroll generated through unchanged service')
    payroll_id=g['payrollId'];domain['api']('POST','employee-payrolls/'+payroll_id+'/approve',{},204)
    domain['api']('POST','employee-payrolls/'+payroll_id+'/mark-paid',{},204)
    u,c=account('history',historical)
    before=snapshot();end(historical,end_body(historical,False));after=snapshot()
    preserved=[t for t in before if t not in ['Employees','EmploymentRecords','Users','SecurityAuditEvents']]
    check(all(before[t]==after[t] for t in preserved),'all Payroll, Leave, Attendance, calendars and related historical rows unchanged by offboarding')
    check(c.request('GET','/api/self/payrolls')['totalCount']==1,'retained authenticated employee reads only own final historical payroll')
    c.request('GET','/api/employee-payrolls/'+payroll_id+'/payslip');check(True,'retained account reads own historical payslip through D10 ownership policy')
    check(len(rows(f'SELECT * FROM EmployeeLeave WHERE EmployeeId={ident(historical)} AND Status=\'Pending\''))==1,'future Pending Leave retained without cancellation')
    current=admin.request('GET',f'/api/employees/{historical}/attendance-days/2030-01-07')
    check(not current['coveragePartitionAvailable'],'future calendar does not authorize expected work after employment ends')
    # SQL filtered unique constraint rejects concurrent current employment; no committed corruption.
    check(rows("SELECT is_unique,filter_definition FROM sys.indexes WHERE name='UX_EmploymentRecords_CurrentPerEmployee'")[0]['is_unique'],'database prohibits another current employment')
    columns=[r['name'] for r in rows("SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('EmploymentRecords') ORDER BY column_id")]
    names=','.join('['+x+']' for x in columns);values=','.join('NEWID()' if x=='EmploymentRecordId' else '['+x+']' for x in columns)
    rejection=sql('BEGIN TRAN; BEGIN TRY INSERT EmploymentRecords ('+names+') SELECT '+values+' FROM EmploymentRecords WHERE EmployeeId='+ident(EMP)+" AND IsCurrent=1; ROLLBACK; THROW 51000,'Invalid accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER() NOT IN (2601,2627) THROW; SELECT 'Rejected'; END CATCH;")
    check('Rejected' in rejection,'SQL rejects second current record; transaction rolled back without mutation')
    events=rows('SELECT * FROM SecurityAuditEvents WHERE ActorUserId IN ('+ident(actor)+','+ident(hr['userId'])+')')
    for action in ['OffboardingAccountDisabled','OffboardingAccountRetained','EmploymentEnded;AccountAccess=NoLinkedAccount','EmploymentEnded;AccountAccess=AlreadyDisabled','AccountEnabled','AccountDisabled']:
        check(any(x['Operation'].startswith(action) for x in events),'authenticated audit '+action)
    check(all(x.get('ActorUserId') for x in events),'offboarding actor attributed by server')
    swagger=Client().request('GET','/swagger/v1/swagger.json');paths=swagger['paths']
    check('/api/employees/{employeeId}/account-lifecycle' in paths,'Swagger safe account readiness route')
    props=swagger['components']['schemas']['EndEmploymentRequest']['properties']
    check(all(k in props for k in ['expectedEmploymentRecordId','disableLinkedAccount','expectedLinkedAccountVersion']) and 'actorUserId' not in props,'Swagger explicit decision and concurrency contracts')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        if pay:pay['cleanup']()
        if organization:sql('DELETE OrganizationProfiles WHERE OrganizationProfileId='+ident(organization))
        if historical:
            sql(f'DELETE AttendanceReviewActions WHERE EmployeeId={ident(historical)}; DELETE FinalizedAttendanceRevisions WHERE EmployeeId={ident(historical)}; DELETE AttendanceReviewCases WHERE EmployeeId={ident(historical)}; DELETE AttendanceEvents WHERE EmployeeId={ident(historical)};')
        cleanup()
        if domain:domain['cleanup_d8d']()
        check(snapshot()==baseline,'exact 85-table baseline restored after all D12 fixtures')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d12-live-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS:',len(results),'D12 live checks; baseline restored',flush=True)
