"""D11 synthetic authenticated localhost/SIAMIS verification; exact baseline cleanup."""
import pathlib,json,subprocess,os,traceback,uuid,concurrent.futures
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')};error=None
ns={'__file__':str(ROOT/'tests/verify_d8d_live.py')}
exec((ROOT/'tests/verify_d8d_live.py').read_text().split('\ntry:\n')[0],ns)
ns['PREFIX']='D11-'+uuid.uuid4().hex[:6]+'-';attendance=[]
try:
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-admin';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    if p.returncode:raise AssertionError('Temporary D11 administrator bootstrap failed')
    admin=Client();admin.login(PREFIX+'-admin');uid=admin.request('GET','/api/auth/me')['userId'];fixtures['users'].append(uid)
    admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    ns['api']=lambda method,path,body=None,status=200:admin.request(method,'/api/'+path,body,status)
    ns['policy'](ns['annual'],'TRACKED',True,end='2031-12-31',sandwichParticipation=True,sandwichEquivalentDayMinutes=300)
    ns['policy'](ns['sick'],'PAID-UNTRACKED',False,end='2031-12-31')
    ns['policy'](ns['personal'],'UNPAID-UNTRACKED',False,end='2031-12-31')
    ns['policy'](ns['other'],'UNPAID-TRACKED',True,end='2031-12-31')
    def fixture(label,minutes=None,t=None):
        e=ns['employee'](label[:18]);c=ns['calendar'](label,[('08:00','16:00')]);ns['assign'](e,c,end='2031-12-31')
        ent=ns['entitlement'](e,t or ns['annual'],minutes=minutes) if minutes is not None else None
        return e,c,ent
    def create(e,start='2030-01-07',end=None,t=None):
        return ns['api']('POST',f'employees/{e}/leave',ns['request'](start,end=end,t=t or ns['annual']),201)
    def totals(l,paid,unpaid,label):
        check(l['calculation']['version']==2 and l['paidMinutes']==paid and l['unpaidMinutes']==unpaid and l['chargeableMinutes']==paid+unpaid,label)
        check(sum(a['paidMinutes'] for a in l['allocations'])==paid and sum(a['unpaidMinutes'] for a in l['allocations'])==unpaid,label+' yearly allocation parity')
    for label,budget,end,paid,unpaid in [('SUFFICIENT',2400,'2030-01-08',960,0),('EXACT',960,'2030-01-08',960,0),('PARTIAL',960,'2030-01-10',960,960),('ZERO',0,'2030-01-10',0,1920),('PARTIAL-DAY',180,'2030-01-07',180,300)]:
        e,c,en=fixture(label,budget);l=create(e,end=end);totals(l,paid,unpaid,label)
        b=ns['balance'](e);check(b['pendingMinutes']==paid and b['availableMinutes']==budget-paid,label+' reserves only paid minutes')
        approved=ns['command'](e,l,'approve');check(ns['balance'](e)['usedMinutes']==paid and ns['balance'](e)['pendingMinutes']==0,label+' approval once')
        if label=='PARTIAL-DAY':
            segments=l['calculation']['dates'][0]['paymentIntervals']
            check(segments[0]['endTime']=='11:00:00' and segments[0]['isPaid'] and not segments[1]['isPaid'],'exact 3-hour paid/5-hour unpaid split')
            day=ns['api']('GET',f'employees/{e}/attendance-days/2030-01-07')
            check(day['approvedLeaveCoveredScheduledMilliseconds']==480*60000 and day['paidLeaveCoveredMilliseconds']==180*60000 and day['unpaidLeaveCoveredMilliseconds']==300*60000 and day['unexplainedScheduledMilliseconds']==0,'mixed Approved Leave covers all authorized absence')
            r=ns['api']('GET',f'employees/{e}/attendance-days/2030-01-07/review')
            body={'reason':'D11 synthetic finalization','expectedVersion':r['version'],'expectedSourceFingerprint':r['sourceFingerprint']}
            r=ns['api']('POST',f'employees/{e}/attendance-days/2030-01-07/finalize',body,201);attendance.append(e)
            old=rows('SELECT * FROM FinalizedAttendanceRevisions WHERE EmployeeId='+ident(e))
            ns['api']('POST',f"employees/{e}/leave-entitlements/{en['id']}/adjustments",{'adjustmentMinutes':60,'reason':'D11 later entitlement change'},201)
            totals(ns['api']('GET',f"employees/{e}/leave/{l['leaveId']}"),paid,unpaid,'frozen allocation after entitlement adjustment')
            check(rows('SELECT * FROM FinalizedAttendanceRevisions WHERE EmployeeId='+ident(e))==old,'historical finalized Attendance remains immutable')
            ns['cancel'](e,approved,True)
            stale=ns['api']('GET',f'employees/{e}/attendance-days/2030-01-07/review')
            check(stale['isStale'] and stale['requiresReopen'] and rows('SELECT * FROM FinalizedAttendanceRevisions WHERE EmployeeId='+ident(e))==old,'Leave cancellation preserves historical Attendance and detects stale')
            check(ns['balance'](e)['availableMinutes']==budget+60,'mixed cancellation restores paid portion only')
        else:
            ns['cancel'](e,approved,True);check(ns['balance'](e)['availableMinutes']==budget,label+' cancellation releases exactly paid usage')
        ns['command'](e,l,'cancel',{'expectedStatus':'Approved','cancellationRemarks':'Repeat'},409);check(True,label+' repeated cancellation rejected')
    e,c,en=fixture('MISSING');ns['api']('POST',f'employees/{e}/leave',ns['request'](),409);check(True,'missing paid entitlement remains conflict')
    for label,t,tracked,budget,paid in [('PAID-UNTRACKED',ns['sick'],False,None,480),('UNPAID-UNTRACKED',ns['personal'],False,None,0),('UNPAID-TRACKED',ns['other'],True,None,0),('UNPAID-TRACKED-WITH-ENTITLEMENT',ns['other'],True,60,0)]:
        e,c,en=fixture(label,budget,t);l=create(e,t=t);totals(l,paid,480-paid,label);ns['command'](e,l,'approve')
        if budget is not None:check(ns['balance'](e,t)['availableMinutes']==budget and ns['balance'](e,t)['usedMinutes']==0,'unpaid tracked does not consume or invent quota')
    e,c,en=fixture('REJECT',180);l=create(e);ns['command'](e,l,'reject');check(ns['balance'](e)['availableMinutes']==180,'rejection releases paid reservation only')
    ns['command'](e,l,'reject',status=409);check(ns['balance'](e)['availableMinutes']==180,'repeated rejection cannot credit entitlement')
    e,c,en=fixture('VARIED',600)
    ns['api']('POST',f'work-calendars/{c}/overrides',{'date':'2030-01-08','overrideType':'ExceptionalWorkingDay','intervals':[{'startTime':'10:00','endTime':'14:00'}]},201)
    l=create(e,end='2030-01-09');totals(l,600,600,'different day lengths follow minutes')
    check([sum((int(s['endTime'][:2])*60+int(s['endTime'][3:5]))-(int(s['startTime'][:2])*60+int(s['startTime'][3:5])) for s in d['paymentIntervals'] if s['isPaid']) for d in l['calculation']['dates']]==[480,120,0],'chronological variable-duration date allocation')
    e,c,en=fixture('REST',480);l=create(e,'2030-01-11','2030-01-14');totals(l,480,480,'weekend stays nonchargeable')
    check(all(d['paymentIntervals']==[] and d['chargeableMinutes']==0 for d in l['calculation']['dates'] if d['date'] in ['2030-01-12','2030-01-13']),'nonworking dates have no fabricated paid/unpaid minutes')
    e,c,en=fixture('CROSS-YEAR',120);ns['entitlement'](e,year=2031,minutes=600)
    l=create(e,'2030-12-31','2031-01-02');totals(l,720,720,'cross-year independent exhaustion')
    check([a['paidMinutes'] for a in l['allocations']]==[120,600],'each year funds its own dates')
    e,c,en=fixture('RACE',480)
    with concurrent.futures.ThreadPoolExecutor(2) as pool:raced=list(pool.map(lambda date:create(e,date),['2030-01-07','2030-01-08']))
    check(sorted(l['paidMinutes'] for l in raced)==[0,480] and ns['balance'](e)['availableMinutes']==0,'two simultaneous valid requests cannot pay twice from same entitlement')
    # V1 synthetic history retains old accounting and serialized bytes, without backfilling segmentation.
    e,c,en=fixture('V1',960);l=create(e);legacy=l['calculation'];legacy['version']=1
    for d in legacy['dates']:d.pop('paymentIntervals',None)
    for a in legacy['allocations']:a.pop('paidMinutes',None);a.pop('unpaidMinutes',None)
    legacy_json=json.dumps(legacy).replace("'","''")
    sql('UPDATE EmployeeLeave SET CalculationSnapshotVersion=1,CalculationSnapshotJson=N\''+legacy_json+'\' WHERE LeaveId='+ident(l['leaveId'])+'; UPDATE EmployeeLeaveAllocations SET PaidMinutes=NULL,UnpaidMinutes=NULL WHERE EmployeeLeaveId='+ident(l['leaveId']))
    legacy_rows=rows('SELECT CalculationSnapshotJson FROM EmployeeLeave WHERE LeaveId='+ident(l['leaveId']))
    old=ns['api']('GET',f"employees/{e}/leave/{l['leaveId']}");check(old['calculation']['version']==1 and old['paidMinutes'] is None,'V1 historical interpretation retained, no fabricated totals')
    ns['command'](e,l,'approve');check(ns['balance'](e)['usedMinutes']==480 and rows('SELECT CalculationSnapshotJson FROM EmployeeLeave WHERE LeaveId='+ident(l['leaveId']))==legacy_rows,'V1 historical accounting and snapshot bytes unchanged')
    new=create(e,'2030-01-08');totals(new,480,0,'V2 coexists with V1 reservation')
    # Deliberate corruption affects only recorded fixtures and is restored after failed transition.
    e,c,en=fixture('INTEGRITY',180);l=create(e);saved=rows('SELECT CalculationSnapshotJson FROM EmployeeLeave WHERE LeaveId='+ident(l['leaveId']))[0]['CalculationSnapshotJson']
    sql("UPDATE EmployeeLeave SET CalculationSnapshotJson=JSON_MODIFY(CalculationSnapshotJson,'$.dates[0].paymentIntervals[0].endTime','11:01:00') WHERE LeaveId="+ident(l['leaveId']))
    ns['command'](e,l,'approve',status=409);check(True,'V2 tampered interval segmentation rejected by lifecycle integrity')
    sql("UPDATE EmployeeLeave SET CalculationSnapshotJson=N'"+saved.replace("'","''")+"' WHERE LeaveId="+ident(l['leaveId']))
    ns['constraint']('UPDATE EmployeeLeaveAllocations SET PaidMinutes=-1 WHERE EmployeeLeaveId='+ident(l['leaveId']),'SQL negative paid minutes rejected')
    ns['constraint']('UPDATE EmployeeLeaveAllocations SET UnpaidMinutes=NULL WHERE EmployeeLeaveId='+ident(l['leaveId']),'SQL one-null classification rejected')
    ns['constraint']('UPDATE EmployeeLeaveAllocations SET PaidMinutes=0 WHERE EmployeeLeaveId='+ident(l['leaveId']),'SQL paid/unpaid total mismatch rejected')
    check(True,'live SQL V2 constraints reject invalid allocation combinations')
    # Existing sandwich cap shares the same remaining availability, without altering potential/unabsorbed facts.
    e,c=ns['new']('SANDWICH',500);left,right=ns['pair'](e);case=ns['cases'](e)[0]
    reviewed=ns['sandwich_review'](e,case,outcome='ReasonNotAccepted')
    check(left['paidMinutes']+right['paidMinutes']==500 and reviewed['appliedSandwichDebitMinutes']==0 and reviewed['sandwichDebitMinutes']==600,'normal exhaustion and capped sandwich share availability without double consumption')
    ns['command'](e,left,'approve');ns['command'](e,right,'approve');check(ns['balance'](e)['availableMinutes']==0,'mixed boundaries preserve sandwich approval lifecycle')
    # Authenticated self-service: own create only; cannot approve or expose confidential evidence.
    e,c,en=fixture('SELF',0)
    u=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-employee','temporaryPassword':password,'roles':['Employee'],'employeeId':e},201);fixtures['users'].append(u['userId'])
    own=Client();own.login(u['userName']);own.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    l=own.request('POST',f'/api/employees/{e}/leave',ns['request'](),201);totals(l,0,480,'self-service zero entitlement valid exhaustion')
    own.request('POST',f"/api/employees/{e}/leave/{l['leaveId']}/approve",{},403);own.request('POST',f'/api/employees/{EMP}/leave',ns['request'](),403)
    own.request('GET',f"/api/employees/{e}/leave/{l['leaveId']}/evidence",status=403);check(True,'D10 own ownership/review/confidential-evidence boundary preserved')
    own.request('POST',f'/api/employees/{e}/leave',ns['request']('2030-01-08')|{'paidMinutes':480,'actorUserId':uid},400);check(True,'caller cannot choose allocation or actor')
    current=snapshot();check(all(current[t]==baseline[t] for t in baseline if t.startswith('Payroll') or t.startswith('EmployeePayroll')),'no Payroll transaction or configuration changed')
    swagger=Client().request('GET','/swagger/v1/swagger.json');check('paidMinutes' in swagger['components']['schemas']['EmployeeLeaveDto']['properties'] and 'unpaidMinutes' in swagger['components']['schemas']['EmployeeLeaveDto']['properties'],'Swagger additive paid/unpaid DTO metadata')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        for e in attendance:sql('DELETE AttendanceReviewActions WHERE EmployeeId='+ident(e)+'; DELETE FinalizedAttendanceRevisions WHERE EmployeeId='+ident(e)+'; DELETE AttendanceReviewCases WHERE EmployeeId='+ident(e))
        cleanup();ns['cleanup_d8d']();check(snapshot()==baseline,'D11 fixtures cleaned; exact 85-table baseline restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d11-live-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results,'counts':{t:len(v) for t,v in snapshot().items()}},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS:',len(results),'D11 live assertions; exact baseline restored',flush=True)
