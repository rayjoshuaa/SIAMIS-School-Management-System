"""Focused D10 payroll ownership, lockout, administration races, forgery and schema verification."""
import pathlib, os, json, subprocess, concurrent.futures, traceback, uuid
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')};error=None
payroll_ns={'__file__':str(ROOT/'tests/verify_d5a_live.py')}
exec((ROOT/'tests/verify_d5a_live.py').read_text().split('baseline=snapshot()')[0],payroll_ns)
events=[];organization_id=None
try:
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-advanced';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    if p.returncode:raise AssertionError('Advanced bootstrap failed')
    admin=Client();admin.login(PREFIX+'-advanced');uid=admin.request('GET','/api/auth/me')['userId'];fixtures['users'].append(uid)
    admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    # Reject an existing second bootstrap without adding another user.
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode!=0 and len(rows('SELECT Id FROM Users'))==1,'first-admin bootstrap cannot rerun once a SystemAdmin exists')
    clients=[];employees=[];accounts=[]
    for suffix in ['A','B']:
        e=admin.request('POST','/api/employees',{'employeeNumber':PREFIX+'-'+suffix,'firstName':'Security','lastName':suffix,'departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001','hireDate':'2026-09-29'},201)
        employees.append(e['employeeId']);fixtures['employees'].append(e['employeeId'])
        u=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-'+suffix,'temporaryPassword':password,'employeeId':e['employeeId'],'email':PREFIX+'-'+suffix+'@example.invalid','roles':['Employee']},201)
        fixtures['users'].append(u['userId']);accounts.append(u)
        c=Client();c.login(u['userName']);c.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204);clients.append(c)
    payroll_ns['api']=lambda method,path,body=None,status=200:admin.request(method,'/api/'+path,body,status)
    payroll_ns['PREFIX']=PREFIX+'-';payroll_ns['EMP']=employees[0]
    payroll_ns['pit_opt_out']()
    pit_scheme=payroll_ns['fixtures']['schemes'][0]
    enrollment=admin.request('POST',f'/api/employees/{employees[1]}/statutory-enrollments',{'statutorySchemeId':pit_scheme,'effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','applicability':'NotApplicable'},201)
    payroll_ns['fixtures']['enrollments'].append(enrollment['employeeStatutoryEnrollmentId'])
    sso=admin.request('POST','/api/statutory-schemes',{'code':'TH-SSO-33','name':PREFIX+' Synthetic explicit opt-out','jurisdiction':'TH','schemeType':'SocialSecurity'},201)
    payroll_ns['fixtures']['schemes'].append(sso['statutorySchemeId'])
    for employee in employees:
        en=admin.request('POST',f'/api/employees/{employee}/statutory-enrollments',{'statutorySchemeId':sso['statutorySchemeId'],'effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','applicability':'NotApplicable'},201)
        payroll_ns['fixtures']['enrollments'].append(en['employeeStatutoryEnrollmentId'])
    paytype=rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id']
    for employee in employees:
        c=admin.request('POST',f'/api/employees/{employee}/compensations',{'payTypeId':paytype,'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-29','isCurrent':True,'remarks':'D10 temporary ownership fixture'},201)
        payroll_ns['fixtures']['compensations'].append(c['compensationId'])
    hr=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-hr','temporaryPassword':password,'roles':['HRAdmin']},201)
    fixtures['users'].append(hr['userId']);hr_client=Client();hr_client.login(hr['userName'])
    hr_client.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    check(hr_client.request('GET','/api/employees/'+employees[0])['currentCompensation'] is None,'HR-only employee detail does not expose salary')
    check(admin.request('GET','/api/employees/'+employees[0])['currentCompensation']['basicSalary']==30000,'explicit Payroll capability can read employee salary')
    period=payroll_ns['period'](10)
    organization=admin.request('PUT','/api/organization-profile',{'displayName':PREFIX+' Synthetic Employer','addressLine1':'Synthetic verification only'})
    organization_id=organization['organizationProfileId']
    generated=admin.request('POST',f'/api/payroll-periods/{period}/generate',{'employeeIds':employees})['results']
    check(len(generated)==2 and all(r['status']=='Generated' for r in generated),'temporary payroll generation uses unchanged calculator: '+json.dumps(generated))
    payroll_ids=[next(r['payrollId'] for r in generated if r['employeeId']==employee) for employee in employees]
    for i,payroll in enumerate(payroll_ids):
        clients[i].request('GET','/api/employee-payrolls/'+payroll,status=403);check(True,'Calculated payroll not available to employee '+str(i))
        admin.request('POST',f'/api/employee-payrolls/{payroll}/approve',status=204)
        clients[i].request('GET','/api/employee-payrolls/'+payroll)
        clients[1-i].request('GET','/api/employee-payrolls/'+payroll,status=403);check(True,'Approved payroll own-only '+str(i))
        clients[i].request('GET',f'/api/employee-payrolls/{payroll}/payslip')
        clients[1-i].request('GET',f'/api/employee-payrolls/{payroll}/payslip',status=403);check(True,'frozen payslip own-only '+str(i))
        clients[i].request('POST',f'/api/employee-payrolls/{payroll}/mark-paid',status=403);check(True,'employee cannot mutate Payroll '+str(i))
        own=clients[i].request('GET','/api/self/payrolls?employeeId='+employees[1-i])
        check(own['totalCount']==1 and own['items'][0]['payroll']['employeeId']==employees[i],'self-service ignores client employee identity '+str(i))
    admin.request('POST',f'/api/employee-payrolls/{payroll_ids[0]}/mark-paid',status=204)
    clients[0].request('GET','/api/employee-payrolls/'+payroll_ids[0]);check(True,'Paid own payroll remains accessible')
    # Forged actor headers never supply authority.
    e=admin.request('POST',f'/api/employees/{employees[0]}/attendance-events/manual',{'occurredAt':'2026-09-30T07:30:00+07:00','direction':'In','manualRequestKey':str(uuid.uuid4()),'reason':'D10 actor header rejection fixture'},201,headers={'X-UserId':accounts[1]['userId'],'X-ActorUserId':accounts[1]['userId']})
    event=e;events.append(event['attendanceEventId'])
    check(event['actorId']==uid,'signed authenticated actor wins over forged headers')
    admin.request('POST',f'/api/employees/{employees[0]}/attendance-events/manual',{'occurredAt':'2026-09-30T07:30:00+07:00','direction':'In','manualRequestKey':str(uuid.uuid4()),'reason':'D10 invalid actor','actorId':accounts[1]['userId']},400);check(True,'actor body forgery rejected')
    admin.request('PUT','/api/admin/users/'+accounts[0]['userId']+'/roles',{'roles':['Employee'],'version':'forged','actorUserId':uid},400);check(True,'security administration rejects forged actor field')
    # Own account's lifecycle is separate from Employee.IsActive.
    sql('UPDATE Employees SET IsActive=0 WHERE EmployeeId='+ident(employees[0]))
    check(clients[0].request('GET','/api/self/profile')['isActive'] is False,'inactive employee does not implicitly disable login')
    # Parallel role writes using the same administration version: exactly one succeeds.
    target=admin.request('GET','/api/admin/users/'+accounts[1]['userId'])
    def race_role(role):
        c=Client();c.jar=admin.jar;c.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(c.jar))
        try:c.request('PUT','/api/admin/users/'+target['userId']+'/roles',{'roles':['Employee',role],'version':target['version']});return 200
        except AssertionError as exc:
            if 'got 409' in str(exc):return 409
            raise
    with concurrent.futures.ThreadPoolExecutor(2) as pool:codes=list(pool.map(race_role,['HRAdmin','PayrollAdmin']))
    check(sorted(codes)==[200,409],'parallel role updates produce one winner and one stale conflict')
    clients[1].request('GET','/api/auth/me',status=401);check(True,'role changes revoke existing sessions')
    # Lockout and rate limiting are exercised after all other logins.
    failed=Client()
    for _ in range(5):failed.request('POST','/api/auth/login',{'userName':accounts[0]['userName'],'password':'invalid'},401)
    failed.request('POST','/api/auth/login',{'userName':accounts[0]['userName'],'password':new_password},401)
    check(rows('SELECT LockoutEnd FROM Users WHERE Id='+ident(accounts[0]['userId']))[0].get('LockoutEnd') is not None,'framework failed-login lockout persists')
    clients[0].request('GET','/api/auth/me',status=401);check(True,'locked account session no longer authorizes requests')
    limited=False
    for _ in range(25):
        try:failed.request('POST','/api/auth/login',{'userName':'missing','password':'invalid'},401)
        except AssertionError as exc:
            if 'got 429' in str(exc):limited=True;break
            raise
    check(limited,'login IP rate limit returns 429')
    checks=rows("SELECT name,is_unique,filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID('Users') AND is_unique=1")
    check(any(r['name']=='IX_Users_EmployeeId' for r in checks),'SQL unique Employee linkage exists')
    check(any(r['name']=='EmailIndex' for r in checks),'SQL unique provided email exists')
    check(rows("SELECT delete_referential_action_desc FROM sys.foreign_keys WHERE name='FK_Users_Employees_EmployeeId'")[0]['delete_referential_action_desc']=='NO_ACTION','SQL employee relationship NoAction')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        if events:sql('DELETE AttendanceEvents WHERE AttendanceEventId IN ('+','.join(map(ident,events))+')')
        payroll_ns['cleanup']()
        if organization_id:sql('DELETE OrganizationProfiles WHERE OrganizationProfileId='+ident(organization_id))
        cleanup();check(snapshot()==baseline,'advanced Payroll/security fixtures exactly cleaned')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d10-advanced-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS:',len(results),'D10 advanced security assertions; exact baseline restored',flush=True)
