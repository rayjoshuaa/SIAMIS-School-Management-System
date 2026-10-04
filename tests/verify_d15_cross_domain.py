"""D15 composed A/B/C/E/H/J/K/L scenarios through real authenticated APIs and SQL.
Other required scenarios are freshly rerun in D12/D13/D14/security suites.
"""
import pathlib, datetime, os, subprocess, json, traceback, hashlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d14_live.py').read_text().split('\ntry:\n')[0])
PREFIX='D15-'+uuid.uuid4().hex[:8]
domain=None;pay=None;organization=None;employee_id=None

def review():return admin.request('GET',f'/api/employees/{employee_id}/attendance-days/2026-09-29/review')
def attendance_command(action,extra=None,status=201):
    r=review()
    return admin.request('POST',f'/api/employees/{employee_id}/attendance-days/2026-09-29/{action}',
        {'expectedVersion':r['version'],'expectedSourceFingerprint':r['sourceFingerprint'],'reason':'D15 explicit historical review'}|(extra or {}),status)
def attendance_event(date,d,time):
    return admin.request('POST',f'/api/employees/{employee_id}/attendance-events/manual',
        {'occurredAt':date+'T'+time+'+07:00','direction':d,'manualRequestKey':str(uuid.uuid4()),'reason':'D15 composed evidence'},201)

try:
    check(baseline==json.loads((directory/'d15-baseline.json').read_text()),'D15 exact baseline before composed fixtures')
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-admin';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode==0,'D15 explicit test administrator bootstrap')
    uid=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-admin'")[0]['Id'];fixtures['users'].append(uid)
    admin=Client();admin.login(PREFIX+'-admin');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    u=admin.request('GET','/api/admin/users/'+uid)
    admin.request('PUT','/api/admin/users/'+uid+'/roles',{'roles':['SystemAdmin','HRAdmin'],'version':u['version']})
    admin=Client();admin.login(PREFIX+'-admin',new_password)
    domain={'__file__':str(ROOT/'tests/verify_d8d_live.py')}
    exec((ROOT/'tests/verify_d8d_live.py').read_text().split('\ntry:\n')[0],domain)
    domain['PREFIX']=PREFIX+'-';domain['api']=lambda m,p,b=None,status=200:admin.request(m,'/api/'+p,b,status)
    employee_id=domain['employee']('COMPOSED',hire='2026-08-01',start=None)
    cal=domain['calendar']('COMPOSED',[('07:30','16:00')]);domain['assign'](employee_id,cal,start='2026-08-01',end='2031-12-31')
    domain['policy'](domain['annual'],'PAID',True,start='2026-01-01',end='2031-12-31',supportingDocumentPolicy='AlwaysRequired',documentTypeId=domain['doc'])
    domain['policy'](domain['personal'],'UNPAID',False,start='2026-01-01',end='2031-12-31')
    domain['entitlement'](employee_id,year=2026,minutes=60)
    check(admin.request('GET',f'/api/employees/{employee_id}/attendance-expected-work?date=2026-09-29')['readiness']=='Ready','A hire + explicit calendar resolves expected work')
    ownu,own=account('employee',['Employee'],employee_id)
    request=domain['request']('2026-09-29',a='14:00',b='16:00')
    l=domain['create'](employee_id,request,120,'A paid/unpaid exhaustion request')
    check(l['paidMinutes']==60 and l['unpaidMinutes']==60,'A D11 frozen mixed payment split reconciles 120 minutes')
    receipt=domain['receipt'](employee_id,l);doc=upload(admin,employee_id,fields={'leaveId':l['leaveId'],'leaveEvidenceId':receipt['id'],'documentTypeId':domain['doc']})
    contract=admin.request('POST',f'/api/employees/{employee_id}/contracts',{'contractNumber':PREFIX+'-contract','contractTypeId':rows("SELECT Id FROM ContractTypes WHERE Name='Permanent'")[0]['Id'],'startDate':'2026-08-01','contractStatus':'Active','documentId':doc['employeeDocumentId']},201)
    check(contract['documentId']==doc['employeeDocumentId'],'H same-owner contract points to immutable document version')
    domain['review'](employee_id,l,receipt);domain['command'](employee_id,l,'approve')
    facts={t:rows(f'SELECT * FROM {t}') for t in ['EmployeeLeave','EmployeeLeaveAllocations','EmployeeLeaveEvidence','EmployeeLeaveEvidenceEvents','EmployeeLeaveApprovalEvidence']}
    replaced=multipart(admin,direct(doc['employeeDocumentId'])+'/replace',payload=PDF+b'replacement',fields={'version':doc['version']})
    admin.request('POST',direct(replaced['employeeDocumentId'])+'/archive',{'version':replaced['version']})
    check(all(rows(f'SELECT * FROM {t}')==v for t,v in facts.items()),'H/J replacing and archiving actual supporting binary preserves frozen D8 facts')
    code,bytes_,_=raw(admin,'GET',direct(doc['employeeDocumentId'])+'/content')
    check(code==200 and hashlib.sha256(bytes_).hexdigest()==doc['contentSha256'],'H superseded history retains integrity-verifiable original bytes')
    attendance_event('2026-09-29','In','07:30:00');attendance_event('2026-09-29','Out','14:00:00')
    day=admin.request('GET',f'/api/employees/{employee_id}/attendance-days/2026-09-29')
    check(day['readiness']=='Ready' and day['paidLeaveCoveredMilliseconds']==3600000 and day['unpaidLeaveCoveredMilliseconds']==3600000 and day['unexplainedScheduledMilliseconds']==0,'A paid AND unpaid Leave cover attendance, without double coverage')
    finalized=attendance_command('finalize');first=finalized['latestHistoricalFinalizedRevision']
    check(first['revision']==1 and finalized['isCurrentlyValidated'],'A real hire/calendar/Leave/evidence/attendance finalization chain')
    preserved=rows('SELECT * FROM FinalizedAttendanceRevisions WHERE EmployeeId='+ident(employee_id))
    domain['cancel'](employee_id,l,True)
    stale=review();check(stale['isStale'] and stale['requiresReopen'] and not stale['isCurrentlyValidated'] and stale['changedSources'],'B cancellation produces structured stale state without implicit reopening')
    check(rows('SELECT * FROM FinalizedAttendanceRevisions WHERE EmployeeId='+ident(employee_id))==preserved,'B cancelled Leave never rewrites Revision 1')
    attendance_command('finalize',status=409)
    attendance_command('reopen',status=200)
    attendance_command('corrections',{'occurredAt':'2026-09-29T14:00:00.0000001+07:00','direction':'In','manualRequestKey':str(uuid.uuid4())})
    attendance_command('corrections',{'occurredAt':'2026-09-29T16:00:00+07:00','direction':'Out','manualRequestKey':str(uuid.uuid4())})
    refinalized=attendance_command('finalize')
    check(refinalized['latestHistoricalFinalizedRevision']['revision']==2 and refinalized['isCurrentlyValidated'] and rows('SELECT * FROM FinalizedAttendanceRevisions WHERE Id='+ident(first['id']))==preserved,'B explicit corrected Revision 2 preserves exact Revision 1')
    # A separate entirely unpaid leave does not need paid entitlement.
    unpaid=domain['create'](employee_id,domain['request']('2026-09-30',t=domain['personal'],a='14:00',b='16:00'),120,'A unpaid coverage')
    domain['command'](employee_id,unpaid,'approve')
    attendance_event('2026-09-30','In','07:40:00');attendance_event('2026-09-30','Out','14:00:00')
    day=admin.request('GET',f'/api/employees/{employee_id}/attendance-days/2026-09-30')
    check(day['isLateUnderCurrentPolicy'] and day['unpaidLeaveCoveredMilliseconds']==7200000 and day['unexplainedScheduledMilliseconds']==600000,'L late/unexplained/unpaid attendance evidence is present')
    pay={'__file__':str(ROOT/'tests/verify_d5a_live.py')};exec((ROOT/'tests/verify_d5a_live.py').read_text().split('baseline=snapshot()')[0],pay)
    pay['EMP']=employee_id;pay['PREFIX']=PREFIX+'-';pay['api']=domain['api'];pay['pit_opt_out']()
    ss=domain['api']('POST','statutory-schemes',{'code':'TH-SSO-33','name':PREFIX+' explicit synthetic opt-out','jurisdiction':'TH','schemeType':'SocialSecurity'},201);pay['fixtures']['schemes'].append(ss['statutorySchemeId'])
    en=domain['api']('POST',f'employees/{employee_id}/statutory-enrollments',{'statutorySchemeId':ss['statutorySchemeId'],'effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','applicability':'NotApplicable'},201);pay['fixtures']['enrollments'].append(en['employeeStatutoryEnrollmentId'])
    comp=domain['api']('POST',f'employees/{employee_id}/compensations',{'payTypeId':rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id'],'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-08-01','isCurrent':True},201);pay['fixtures']['compensations'].append(comp['compensationId'])
    organization=domain['api']('PUT','organization-profile',{'displayName':PREFIX+' synthetic employer'})['organizationProfileId']
    period=pay['period'](9);preview=pay['preview'](period);g=pay['generate'](period)
    check(g['status']=='Generated','K payroll generated through established pipeline')
    pid=g['payrollId'];detail=pay['detail'](pid)['payroll']
    check(detail['basicSalary']==30000 and detail['grossPay']==30000 and detail['totalDeductions']==0 and detail['netPay']==30000,'L attendance and Leave did not invent payroll deductions')
    check(preview['netPay']==detail['netPay'],'L preview/generation monetary parity')
    domain['api']('POST','employee-payrolls/'+pid+'/approve',{},204);domain['api']('POST','employee-payrolls/'+pid+'/mark-paid',{},204)
    before=snapshot();r=admin.request('GET',f'/api/employees/{employee_id}/account-lifecycle')
    ending={'expectedEmploymentRecordId':r['currentEmploymentRecordId'],'expectedLinkedAccountVersion':r['linkedAccountVersion'],'endDate':'2026-10-03','employmentStatusId':'40000000-0000-0000-0000-000000000005','disableLinkedAccount':True}
    admin.request('POST',f'/api/employees/{employee_id}/end-employment',ending)
    after=snapshot();history=[t for t in before if t not in ['Employees','EmploymentRecords','Users','SecurityAuditEvents']]
    check(all(before[t]==after[t] for t in history),'C/K actual nonempty Leave/Attendance/Paid payroll/payslip/document history preserved after offboarding')
    own.request('GET','/api/self/profile',status=401);check(True,'C old linked account session revoked')
    check(admin.request('GET',f'/api/employees/{employee_id}/attendance-expected-work?date=2026-10-04')['readiness']=='NotEmployed','C remaining calendar cannot grant work after inclusive employment end')
    domain['api']('POST',f'employees/{employee_id}/leave',domain['request']('2026-10-04',t=domain['personal']),409)
    check(True,'C leave creation outside ended employment rejected')
    admin.request('POST',f'/api/employees/{employee_id}/rehire',context|{'hireDate':'2026-10-04'},201)
    check(len(rows('SELECT * FROM EmploymentRecords WHERE EmployeeId='+ident(employee_id)))==2 and not rows('SELECT IsActive FROM Users WHERE Id='+ident(ownu['userId']))[0]['IsActive'],'E same employee rehire retains history and disabled account')
    after_rehire=snapshot();check(all(before[t]==after_rehire[t] for t in ['FinalizedAttendanceRevisions','EmployeePayrolls','EmployeePayrollLines','EmployeePayslips','EmployeeDocuments','EmployeeLeaveApprovalEvidence']),'E prior frozen domains unchanged after rehire')
    check(admin.request('GET',f'/api/employees/{employee_id}/attendance-expected-work?date=2026-10-04')['readiness']=='Ready','E rehire resolves explicit existing date-effective calendar')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        if pay:pay['cleanup']()
        if organization:sql('DELETE OrganizationProfiles WHERE OrganizationProfileId='+ident(organization))
        if employee_id:sql('DELETE EmployeeContracts WHERE EmployeeId='+ident(employee_id))
        for d in reversed(documents):
            found=rows('SELECT StorageKey FROM EmployeeDocuments WHERE EmployeeDocumentId='+ident(d));sql('DELETE EmployeeDocuments WHERE EmployeeDocumentId='+ident(d))
            if found:safe_path(found[0]['StorageKey']).unlink(missing_ok=True)
        if employee_id:sql(f'DELETE AttendanceReviewActions WHERE EmployeeId={ident(employee_id)}; DELETE FinalizedAttendanceRevisions WHERE EmployeeId={ident(employee_id)}; DELETE AttendanceReviewCases WHERE EmployeeId={ident(employee_id)}; DELETE AttendanceEvents WHERE EmployeeId={ident(employee_id)};')
        cleanup()
        if domain:domain['cleanup_d8d']()
        check(snapshot()==baseline,'D15 composed scenario exact SQL baseline restored')
        check({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in private.glob('*') if p.is_file()}==storage_before,'D15 composed scenario exact private storage restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d15-cross-domain-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2))
if error:raise SystemExit(error)
print('PASS:',len(results),'D15 composed cross-domain checks',flush=True)
