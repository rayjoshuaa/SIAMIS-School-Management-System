"""D10 Production pipeline verification against ONLY localhost/SIAMIS.
Simulates an HTTPS browser through an explicitly trusted loopback TLS terminator;
the local backend transport is HTTP. No production database or TLS installation.
"""
import pathlib, urllib.request, urllib.error, json, os, subprocess, traceback, uuid
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
BASE='http://localhost:5156'
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')};error=None
leave={'__file__':str(ROOT/'tests/verify_d8d_live.py')}
exec((ROOT/'tests/verify_d8d_live.py').read_text().split('\ntry:\n')[0],leave)
leave['PREFIX']=PREFIX+'-';attendance_employee=None

class ProxyClient(Client):
    def request(self,method,path,body=None,status=200,csrf=True,headers=None,forward=True):
        if method not in ['GET','HEAD','OPTIONS'] and csrf:
            self.token=self.request('GET','/api/auth/csrf')['token']
        hdr={'Content-Type':'application/json',**(headers or {})}
        if forward:hdr['X-Forwarded-Proto']='https'
        # A real browser sends these Secure cookies over HTTPS to the proxy.
        # Python sees backend HTTP, so explicitly relay the framework-issued cookies.
        if self.jar:hdr['Cookie']='; '.join(c.name+'='+c.value for c in self.jar)
        if self.token and csrf:hdr['X-CSRF-TOKEN']=self.token
        req=urllib.request.Request(BASE+path,data=None if body is None else json.dumps(body).encode(),method=method,headers=hdr)
        try:
            with self.opener.open(req,timeout=40) as r:code,data,self.last_headers=r.status,r.read(),r.headers
        except urllib.error.HTTPError as e:code,data,self.last_headers=e.code,e.read(),e.headers
        if code!=status:raise AssertionError(f'{method} {path}: expected {status}, got {code}; {data.decode()[:500]}')
        return json.loads(data) if data and 'json' in self.last_headers.get('Content-Type','') else None

try:
    anonymous=ProxyClient()
    anonymous.request('GET','/health',status=400,forward=False);check(True,'Production rejects unencrypted requests')
    anonymous.request('GET','/health');check(True,'only explicitly trusted proxy establishes HTTPS')
    anonymous.request('GET','/api/employees',status=401);check(True,'Production anonymous HR denied')
    anonymous.request('GET','/health',headers={'Origin':'https://untrusted.example.invalid'})
    check('Access-Control-Allow-Origin' not in anonymous.last_headers,'default CORS denies unconfigured origins')
    anonymous.request('POST','/api/auth/login',{'userName':'missing','password':'invalid'},400,csrf=False);check(True,'Production login CSRF required')
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-production';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    if p.returncode:raise AssertionError('Production verification account bootstrap failed')
    uid=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-production'")[0]['Id'];fixtures['users'].append(uid)
    admin=ProxyClient();admin.login(PREFIX+'-production');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    check(all(c.secure and any(k.lower()=='httponly' for k in c._rest) and any(k.lower()=='samesite' and v.lower()=='strict' for k,v in c._rest.items()) for c in admin.jar),'Production session and CSRF cookies Secure/HttpOnly/Strict')
    admin.request('GET','/swagger/v1/swagger.json',status=404);check(True,'Production Swagger disabled')
    admin.request('POST','/api/admin/users',{},400,csrf=False);check(True,'authenticated mutation without CSRF rejected')
    # Existing D8 helpers, authorized by real Identity cookies, retain their financial contracts.
    leave['api']=lambda method,path,body=None,status=200:admin.request(method,'/api/'+path,body,status)
    employee,calendar=leave['new']('PRODUCTION',minutes=None);attendance_employee=employee
    # Production has no configured email provider. Establish isolated credentials through the explicit
    # Development test delivery boundary; all authorization/domain assertions below use Production.
    production_base=BASE;BASE='http://localhost:5155'
    dev_admin=Client();dev_admin.login(PREFIX+'-production',new_password)
    BASE=production_base
    for suffix,roles in [('employee',['Employee']),('hr',['HRAdmin']),('management',['Management'])]:
        BASE='http://localhost:5155'
        try:u=dev_admin.request('POST','/api/admin/users',{'userName':PREFIX+'-'+suffix,'temporaryPassword':password,'employeeId':employee if suffix=='employee' else None,'roles':roles},201)
        finally:BASE=production_base
        fixtures['users'].append(u['userId'])
        c=ProxyClient();c.login(u['userName']);c.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
        if suffix=='employee':own=c
        elif suffix=='hr':hr=c;hrid=u['userId']
        else:management=c
    leave['api']=lambda method,path,body=None,status=200:hr.request(method,'/api/'+path,body,status)
    leave['policy'](leave['annual'],'SANDWICH',False,sandwichParticipation=True,sandwichEquivalentDayMinutes=300)
    left,right=leave['pair'](employee);case=leave['cases'](employee)[0]
    own.request('POST',f"/api/employees/{employee}/leave-sandwich-cases/{case['id']}/review",{'expectedStatus':'ReviewPending','outcome':'ReasonAccepted','reason':'Forged privilege'},403)
    reviewed=leave['sandwich_review'](employee,case,outcome='ReasonAccepted')
    check(reviewed['state']=='ReasonAccepted','HR sandwich review Production-capable')
    for l in [left,right]:leave['command'](employee,l,'approve')
    check(rows('SELECT ActorId FROM EmployeeLeaveSandwichEvents WHERE CaseId='+ident(case['id']))[-1]['ActorId'].lower()==hrid,'sandwich event stores authenticated HR user')
    check(rows('SELECT Id FROM SecurityAuditEvents WHERE ActorUserId='+ident(hrid)+" AND ResourceType='EmployeeLeave'")!=[],'Leave approval audit stores authenticated HR user')
    leave['policy'](leave['sick'],'EVIDENCE',False,supportingDocumentPolicy='AlwaysRequired',documentTypeId=leave['doc'])
    request=leave['create'](employee,leave['request']('2030-01-08',t=leave['sick']),420,'Production required evidence')
    receipt=leave['receipt'](employee,request)
    own.request('GET',f"/api/employees/{employee}/leave/{request['leaveId']}/evidence",status=403)
    management.request('GET',f"/api/employees/{employee}/leave/{request['leaveId']}/evidence",status=403)
    own_detail=own.request('GET',f"/api/employees/{employee}/leave/{request['leaveId']}")
    check(own_detail['evidence'] is None and 'SYNTHETIC-' not in json.dumps(own_detail),'ordinary own Leave read hides confidential receipts')
    check(all(r['evidence'] is None for r in own.request('GET',f'/api/employees/{employee}/leave')),'ordinary Leave list hides confidential receipts')
    leave['review'](employee,request,receipt)
    leave['command'](employee,request,'approve')
    check(all(r['ActorId'].lower()==hrid for r in rows('SELECT ActorId FROM EmployeeLeaveEvidenceEvents WHERE EvidenceId='+ident(receipt['id']))),'evidence record/review stores authenticated HR user')
    leave['receipt'](employee,request,status=400,actorId=uid);check(True,'evidence forged actor rejected')
    leave['sandwich_review'](employee,case,status=400,actorId=uid);check(True,'sandwich forged actor rejected')
    # Review/correction/adjudication/finalization/reopening, preserving revisions and optimistic tokens.
    date='2030-01-09';base=f'/api/employees/{employee}/attendance-days/{date}'
    def command(action,extra=None,status=201):
        current=hr.request('GET',base+'/review')
        body={'expectedVersion':current['version'],'expectedSourceFingerprint':current['sourceFingerprint'],'reason':'D10 Production verified actor'}|(extra or {})
        return hr.request('POST',base+'/'+action,body,status,headers={'X-UserId':uid,'X-ActorUserId':uid})
    own.request('POST',base+'/corrections',{},403);management.request('POST',base+'/finalize',{},403)
    check(True,'Employee and Management cannot mutate Attendance')
    command('corrections',{'occurredAt':date+'T08:00:00+07:00','direction':'In','manualRequestKey':str(uuid.uuid4())})
    r=command('corrections',{'occurredAt':date+'T16:00:00+07:00','direction':'Out','manualRequestKey':str(uuid.uuid4())})
    eid=r['history'][-1]['attendanceEventId']
    command('adjudications',{'attendanceEventId':eid,'included':True})
    r=command('finalize');revision=r['latestHistoricalFinalizedRevision']
    check(revision['actorUserId']==hrid and r['isCurrentlyValidated'],'Production finalization stores HR UserId')
    command('reopen',status=200);r=command('finalize')
    check(r['latestHistoricalFinalizedRevision']['revision']==2 and len(hr.request('GET',base+'/history'))==2,'Production reopen/refinalize preserves both revisions')
    actions=rows('SELECT ActorUserId,Origin FROM AttendanceReviewActions WHERE EmployeeId='+ident(employee))
    check(len(actions)>=6 and all(x['ActorUserId'].lower()==hrid and x['Origin']=='Authenticated' for x in actions),'correction/adjudication/finalize/reopen actor authority ignores forged headers')
    command('reopen',{'actorUserId':uid},400);check(True,'Attendance forged actor body rejected')
    check(all(r['ActorId'].lower()==hrid for r in rows('SELECT ActorId FROM AttendanceEvents WHERE EmployeeId='+ident(employee))),'correction evidence stores same authenticated HR actor')
    # Safe management status APIs omit reasons/evidence/salary.
    overview=management.request('GET','/api/hr/leave-status')
    check('reason' not in json.dumps(overview).lower() and 'evidence' not in json.dumps(overview).lower(),'Management overview minimizes confidential Leave data')
    own.request('GET',f'/api/employees/{uuid.uuid4()}/leave',status=403);check(True,'Production wrong-parent IDOR denied')
    current=snapshot()
    check(all(current[t]==baseline[t] for t in baseline if t.startswith('Payroll') or t.startswith('EmployeePayroll')),'security workflow does not mutate Payroll')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        # Delete only recorded fixture employee-owned records; no original employee is touched.
        if attendance_employee:
            eid=ident(attendance_employee)
            sql(f'DELETE AttendanceReviewActions WHERE EmployeeId={eid}; DELETE FinalizedAttendanceRevisions WHERE EmployeeId={eid}; DELETE AttendanceReviewCases WHERE EmployeeId={eid}; DELETE AttendanceEvents WHERE EmployeeId={eid};')
        cleanup();leave['cleanup_d8d']();check(snapshot()==baseline,'Production fixtures restore exact 85-table baseline')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d10-production-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS:',len(results),'D10 Production/security assertions; exact baseline restored',flush=True)
