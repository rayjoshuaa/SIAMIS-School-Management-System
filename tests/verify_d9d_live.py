"""D9D calculated reads, real frozen Leave, exact clocks and coherent races. localhost/SIAMIS only."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
from concurrent.futures import ThreadPoolExecutor
import threading

OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d9d-live-results.json'
baseline=snapshot(); event_ids=[]; calendar_ids=[]; policy_ids=[]; leave_ids=[]; assignment_ids=[]; error=None
(OUT.parent/'d9d-baseline.json').write_text(json.dumps(baseline),encoding='utf-8')
paid=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-001'")[0]['Id'].lower()
unpaid=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-003'")[0]['Id'].lower()

def calculated(date):return api('GET',f'employees/{EMP}/attendance-days/{date}')
def event(date,d,t):
    r=api('POST',f'employees/{EMP}/attendance-events/manual',{'occurredAt':date+'T'+t+'+07:00','direction':d,'manualRequestKey':str(uuid.uuid4()),'reason':'D9D synthetic verification'},201)
    event_ids.append(r['attendanceEventId']);return r
def pair(date,a,b):event(date,'In',a);event(date,'Out',b)
def partition(r,label):
    check(r['coveragePartitionAvailable'],label+' partition available')
    check(r['scheduledMilliseconds']==sum(r[k] for k in ['presenceCoveredScheduledMilliseconds','approvedLeaveCoveredScheduledMilliseconds','unexplainedScheduledMilliseconds','coverageTruncationResidualMilliseconds']),label+' millisecond identity')
    check(r['coverageTruncationResidualMilliseconds']>=0,label+' residue nonnegative')
def create_leave(date,a=None,b=None,t=paid,approve=True):
    body={'leaveTypeId':t,'startDate':date,'endDate':date,'requestMode':'FullDay' if a is None else 'Timed','noticeCategory':'Foreseeable','reason':'D9D temporary Leave'}
    if a is not None:body|={'requestedStartTime':a,'requestedEndTime':b}
    r=api('POST',f'employees/{EMP}/leave',body,201);leave_ids.append(r['leaveId'])
    if approve:r=api('POST',f"employees/{EMP}/leave/{r['leaveId']}/approve",{})
    return r
def cancel(r):api('POST',f"employees/{EMP}/leave/{r['leaveId']}/cancel",{'expectedStatus':r['status'],'cancellationRemarks':'D9D verification cleanup'})
def override(date,kind,intervals=None):
    body={'date':date,'overrideType':kind}
    if intervals is not None:body['intervals']=intervals
    return api('POST',f"work-calendars/{calendar_ids[0]}/overrides",body,201)
def race(calls):
    barrier=threading.Barrier(len(calls))
    def run(f):barrier.wait();return f()
    with ThreadPoolExecutor(max_workers=len(calls)) as p:return list(p.map(run,calls))


def authoritative_event(date,d,t,source):
    eid=str(uuid.uuid4());event_ids.append(eid)
    sql(f"INSERT AttendanceEvents (AttendanceEventId,EmployeeId,OccurredAtUtc,BusinessDate,BusinessTimeZone,Direction,Source,SourceKey,ExternalEventId,ReceivedAtUtc,EmployeeWasInactive,EmploymentReadiness) VALUES ({ident(eid)},{ident(EMP)},CONVERT(datetime2(7),SWITCHOFFSET(CONVERT(datetimeoffset(7),'{date}T{t}+07:00'),'+00:00')),'{date}','Asia/Bangkok','{d}','{source}','D9D-SYNTHETIC','{eid}',SYSUTCDATETIME(),1,'Ready')")
    return {'attendanceEventId':eid}
def review(date):return api('GET',f'employees/{EMP}/attendance-days/{date}/review')
def token(date):
    r=review(date);return {'expectedVersion':r['version'],'expectedSourceFingerprint':r['sourceFingerprint'],'reason':'D9D synthetic reason'}
review_ids={'cases':set(),'actions':set(),'revisions':set()}
created_location_checked=False
def record_review(r):
    if r.get('reviewCase'):review_ids['cases'].add(r['reviewCase']['id'])
    for a in r.get('history',[]):review_ids['actions'].add(a['id'])
    if r.get('latestHistoricalFinalizedRevision'):review_ids['revisions'].add(r['latestHistoricalFinalizedRevision']['id'])
    return r
def command(date,action,extra=None,status=None,body=None):
    if status is None:status=200 if action in ['confirm-absence','reopen'] else 201
    global created_location_checked
    payload=(token(date) if body is None else body)|(extra or {})
    path=f'employees/{EMP}/attendance-days/{date}/{action}'
    if action=='finalize' and status==201 and not created_location_checked:
        req=urllib.request.Request(BASE+'/api/'+path,data=json.dumps(payload).encode(),method='POST',headers={'Content-Type':'application/json'})
        with urllib.request.urlopen(req,timeout=30) as response:
            check(response.status==201,'finalize HTTP Created response')
            r=record_review(json.loads(response.read()));location=response.headers['Location']
        check(location.endswith('/'+date+'/review'),'Created Location uses ISO business date')
        with urllib.request.urlopen(location,timeout=30) as response:check(response.status==200,'Created Location resolves to review GET')
        created_location_checked=True
        return r
    r=api('POST',path,payload,status)
    return record_review(r) if status in [200,201] else r
def finalize(date):
    r=command(date,'finalize');check(r['isCurrentlyValidated'] and not r['isStale'],'finalized current '+date)
    f=r['latestHistoricalFinalizedRevision'];c=f['snapshot']['calculation'];partition(c,'frozen '+date)
    check(f['actorUserId'] is None and c['clockInGraceMinutes']==5 and c['calculationContractVersion']=='D9C-v1','frozen policy/null actor '+date)
    return r
def correction(date,d,t,status=201,body=None):
    r=command(date,'corrections',{'occurredAt':date+'T'+t+'+07:00','direction':d,'manualRequestKey':str(uuid.uuid4())},status,body)
    if status==201:event_ids.append(r['history'][-1]['attendanceEventId'])
    return r
def decide(date,e,included=False):return command(date,'adjudications',{'attendanceEventId':e['attendanceEventId'],'included':included})
def raw_http(action,date,body):
    req=urllib.request.Request(BASE+f'/api/employees/{EMP}/attendance-days/{date}/{action}',data=json.dumps(body).encode(),method='POST',headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=30) as r:return r.status,record_review(json.loads(r.read()))
    except urllib.error.HTTPError as e:return e.code,json.loads(e.read())
def sql_reject(query,label):
    try:sql(query)
    except AssertionError as e:check('547' in str(e) or '2601' in str(e) or '2627' in str(e),label);return
    raise AssertionError('SQL accepted invalid '+label)
try:
    check(len(baseline)==77,'77 application tables after D9D')
    before=json.loads((OUT.parent/'d9d-before-migration.json').read_text())
    check(all(baseline[t]==v for t,v in before.items()),'migration preserves exact 74 original tables')
    check(all(not baseline[t] for t in ['AttendanceReviewCases','AttendanceReviewActions','FinalizedAttendanceRevisions']),'new tables have no seed data')
    schema=rows("SELECT t.name,c.name AS ColumnName,ty.name AS Type,c.scale FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.name IN ('AttendanceReviewCases','AttendanceReviewActions','FinalizedAttendanceRevisions')")
    check(sum(x['Type']=='datetime2' and x['scale']==7 for x in schema)==3,'SQL UTC-compatible datetime2(7) timestamps')
    fks=rows("SELECT delete_referential_action_desc AS Behavior FROM sys.foreign_keys WHERE parent_object_id IN (OBJECT_ID('AttendanceReviewCases'),OBJECT_ID('AttendanceReviewActions'),OBJECT_ID('FinalizedAttendanceRevisions'))")
    check(len(fks)==7 and all(x['Behavior']=='NO_ACTION' for x in fks),'seven SQL NoAction foreign keys')
    check(len(rows("SELECT name FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID('AttendanceReviewCases'),OBJECT_ID('AttendanceReviewActions'),OBJECT_ID('FinalizedAttendanceRevisions'))"))==5,'five SQL check constraints')
    api('GET',f'employees/{uuid.uuid4()}/attendance-days/2030-01-07/review',status=404);check(True,'unknown employee 404')
    c=api('POST','work-calendars',{'code':'D9D-VERIFY','name':'D9D synthetic continuous'},201);calendar_ids.append(c['id'])
    for d in range(7):api('POST',f"work-calendars/{c['id']}/weekly-intervals",{'dayOfWeek':d,'startTime':'07:30','endTime':'16:00'},201)
    a=api('POST',f'employees/{EMP}/work-calendar-assignments',{'workCalendarId':c['id'],'effectiveFrom':'2030-01-01','effectiveTo':'2030-12-31'},201);assignment_ids.append(a['id'])
    for t,version in [(paid,'PAID'),(unpaid,'UNPAID')]:
        p=api('POST','leave-policies',{'leaveTypeId':t,'version':'D9D-'+version,'effectiveFrom':'2030-01-01','effectiveTo':'2030-12-31','balanceTracked':False,'allowsSuddenRequest':True},201);policy_ids.append(p['id']);api('POST',f"leave-policies/{p['id']}/publish",{})

    # Clean, grace, precision, and split/non-working snapshots.
    for date,start,late in [('2030-01-07','07:30:00',False),('2030-01-08','07:15:00',False),('2030-01-09','07:35:00.0000000',False),('2030-01-10','07:35:00.0000001',True),('2030-01-11','07:30:00.0000001',False)]:
        pair(date,start,'16:00:00');r=finalize(date)
        check(r['reviewCase'] is None and r['latestHistoricalFinalizedRevision']['snapshot']['calculation']['isLateUnderCurrentPolicy']==late,'clean direct finalize/grace '+date)
    r=review('2030-01-11');check(r['calculation']['coverageTruncationResidualMilliseconds']==1 and r['calculation']['unexplainedScheduledMilliseconds']==0,'100ns residual not absence')
    override('2030-01-12','ExceptionalWorkingDay',[{'startTime':'07:30','endTime':'12:00'},{'startTime':'13:00','endTime':'16:00'}]);pair('2030-01-12','07:25:00','16:05:00');finalize('2030-01-12')
    override('2030-01-13','RestDay');pair('2030-01-13','08:00:00','09:00:00');r=finalize('2030-01-13');check(r['calculation']['scheduledMilliseconds']==0 and not r['isConfirmedAbsent'],'non-working presence no absence/payroll')
    # Missing evidence is corrected without editing the original raw row.
    for date,direction,time,missing in [('2030-01-14','In','07:29:00','Out'),('2030-01-15','Out','16:00:00','In')]:
        e=authoritative_event(date,direction,time,'Device' if direction=='In' else 'Imported');original=rows('SELECT * FROM AttendanceEvents WHERE AttendanceEventId='+ident(e['attendanceEventId']))
        command(date,'finalize',status=409);check(True,'unresolved missing clock blocked '+date)
        r=correction(date,missing,'16:00:00' if missing=='Out' else '07:30:00')
        check(r['calculation']['readiness']=='Ready' and r['reviewCase']['originalCalculation']['readiness']=='RequiresReview','correction resolves and preserves original finding '+date)
        check(rows('SELECT * FROM AttendanceEvents WHERE AttendanceEventId='+ident(e['attendanceEventId']))==original,'original evidence unchanged '+date)
        check(r['history'][-1]['origin']=='DevelopmentUnattributed' and r['history'][-1]['actorUserId'] is None,'correction attributable placeholder '+date)
        finalize(date)
    # Duplicate IN/OUT and Unknown are explicitly adjudicated; Included is reversible.
    for date,bad in [('2030-01-16','In'),('2030-01-17','Out'),('2030-01-18','Unknown')]:
        pair(date,'07:30:00','16:00:00');e=authoritative_event(date,bad,'10:00:00','Device')
        command(date,'finalize',status=409);r=decide(date,e)
        check(len(r['rawCalculation']['events'])==3 and len(r['calculation']['events'])==2,'excluded raw evidence retained '+date)
        r=decide(date,e,True);check(r['calculation']['readiness']=='RequiresReview','included decision reinstates source '+date)
        decide(date,e);finalize(date)
    # Potential absence needs confirmation, not fabricated Leave.
    date='2030-01-19';command(date,'finalize',status=409);r=command(date,'confirm-absence')
    check(r['isConfirmedAbsent'] and r['calculation']['potentialAbsence'],'explicit confirmed absence distinct from D9C')
    r=finalize(date);check(r['latestHistoricalFinalizedRevision']['snapshot']['isConfirmedAbsent'],'frozen confirmed absence')
    command('2030-01-20','confirm-absence',status=200)
    correction('2030-01-20','In','07:30:00');check(not review('2030-01-20')['isConfirmedAbsent'],'new sources invalidate absence confirmation')
    # Full paid/unpaid and partial frozen Leave. Cancellation remains independent.
    for date,t in [('2030-01-21',paid),('2030-01-22',unpaid)]:
        create_leave(date,t=t);r=finalize(date);check(r['calculation']['approvedLeaves'][0]['isPaid']==(t==paid),'frozen paid/unpaid coverage '+date)
    date='2030-01-23';pair(date,'07:30:00','14:00:00');l=create_leave(date,'14:00','16:00');r=finalize(date)
    old=r['latestHistoricalFinalizedRevision'];old_sql=rows('SELECT * FROM FinalizedAttendanceRevisions WHERE Id='+ident(old['id']))
    cancel(l);current=review(date)
    check(current['isStale'] and current['requiresReopen'] and not current['isCurrentlyValidated'],'cancelled contributing Leave makes revision stale')
    check(any(x['code']=='ApprovedLeaveCancelled' and l['leaveId'] in x['sourceIds'] for x in current['changedSources']),'structured cancelled Leave finding')
    before_reads=snapshot();review(date);api('GET',f'employees/{EMP}/attendance-days/{date}/history');check(snapshot()==before_reads,'stale reads have no writes')
    command(date,'finalize',status=409);r=command(date,'reopen');check(r['isReopened'] and not r['isCurrentlyValidated'],'explicit reopen needed')
    # Replace the short OUT with authorized correction by exclusion plus new evidence.
    out=r['rawCalculation']['events'][-1];decide(date,out);correction(date,'Out','16:00:00');r=finalize(date)
    hist=api('GET',f'employees/{EMP}/attendance-days/{date}/history')
    check(len(hist)==2 and hist[0]==old and r['latestHistoricalFinalizedRevision']['revision']==2,'revision 2 preserves immutable revision 1')
    check(rows('SELECT * FROM FinalizedAttendanceRevisions WHERE Id='+ident(old['id']))==old_sql,'SQL old snapshot byte-for-byte unchanged')
    # Schedule changes make a historical snapshot stale without rewriting it.
    date='2030-01-07';old=review(date)['latestHistoricalFinalizedRevision'];override(date,'ExceptionalWorkingDay',[{'startTime':'08:00','endTime':'16:00'}]);r=review(date)
    check(r['isStale'] and any(x['code']=='ExpectedWorkChanged' for x in r['changedSources']) and r['latestHistoricalFinalizedRevision']==old,'calendar change preserves historical schedule')
    # Overlap/mismatch/config conflicts cannot be force-finalized.
    pair('2030-01-24','07:30:00','16:00:00');create_leave('2030-01-24','14:00','16:00');command('2030-01-24','finalize',status=409);check(True,'presence Leave overlap blocks finalization')
    r=review('2030-01-24');decide('2030-01-24',r['rawCalculation']['events'][-1]);correction('2030-01-24','Out','14:00:00')
    r=finalize('2030-01-24');check(r['calculation']['presenceLeaveOverlapIntervals']==[] and len(r['rawCalculation']['events'])==3,'overlap resolved only through explicit corrected evidence, not precedence')
    create_leave('2030-01-25');override('2030-01-25','ExceptionalWorkingDay',[{'startTime':'08:00','endTime':'16:00'}]);command('2030-01-25','finalize',status=409);check(True,'frozen Leave schedule mismatch blocks finalization')
    command('2031-01-07','finalize',status=409);check(True,'missing assigned calendar blocks finalization')
    # Source/version protection and immutable finalized-day mutation guard.
    date='2030-01-26';stale=token(date);pair(date,'07:30:00','16:00:00');command(date,'finalize',body=stale,status=409);check(True,'stale screen evidence token rejected')
    r=finalize(date);correction(date,'Out','16:01:00',409);command(date,'reopen',extra={'reason':''},status=400);check(True,'finalized correction blocked and reason required')
    for field in ['actorUserId','scheduledMilliseconds','sourceFingerprint']:
        command('2030-01-27','confirm-absence',extra={field:None},status=400);check(True,'client forgery rejected '+field)
    foreign=event('2030-01-28','In','07:30:00');command('2030-01-27','adjudications',{'attendanceEventId':foreign['attendanceEventId'],'included':False},404);check(True,'wrong date event ownership 404')
    correction('2030-01-27','Unknown','07:30:00',400);correction('2030-01-27','In','07:30:00.12345678',400);check(True,'correction strict direction/precision')
    date='2030-01-26';old=review(date)['latestHistoricalFinalizedRevision'];event(date,'In','16:15:00');r=review(date)
    check(r['isStale'] and r['requiresReopen'] and any(x['code']=='AttendanceEvidenceChanged' for x in r['changedSources']) and r['latestHistoricalFinalizedRevision']==old,'new raw evidence makes immutable finalization stale')
    # Atomic failure leaves no case/action/event/revision side effects.
    fail_before=snapshot();command('2030-01-27','finalize',status=409);check(snapshot()==fail_before,'failed finalization has no partial writes')
    date='2030-01-29';body=token(date)|{'occurredAt':date+'T07:30:00+07:00','direction':'In','manualRequestKey':str(uuid.uuid4())}
    r=command(date,'corrections',body=body);event_ids.append(r['history'][-1]['attendanceEventId'])
    body=token(date)|{k:body[k] for k in ['occurredAt','direction','manualRequestKey']}
    fail_before=snapshot();command(date,'corrections',body=body,status=409);check(snapshot()==fail_before,'duplicate correction key rejected without partial writes')
    # Concurrent operations: every successful action serialized; stale competitor rejected.
    date='2030-02-01';pair(date,'07:30:00','16:00:00');b=token(date);rr=race([lambda:raw_http('finalize',date,b)]*2)
    check(sorted(x[0] for x in rr)==[201,409],'finalize/finalize one succeeds')
    b=token(date);rr=race([lambda:raw_http('reopen',date,b)]*2);check(sorted(x[0] for x in rr)==[200,409],'reopen/reopen one succeeds')
    date='2030-02-02';pair(date,'07:30:00','16:00:00');b=token(date)
    cb=b|{'occurredAt':date+'T16:01:00+07:00','direction':'Out','manualRequestKey':str(uuid.uuid4())}
    rr=race([lambda:raw_http('finalize',date,b),lambda:raw_http('corrections',date,cb)])
    if rr[1][0]==201:event_ids.append(rr[1][1]['history'][-1]['attendanceEventId'])
    check(sorted(x[0] for x in rr)==[201,409],'correction/finalize serialized no lost source')
    date='2030-02-03';pair(date,'07:30:00','16:00:00');r=review(date);e=r['rawCalculation']['events'][0];b=token(date)
    rr=race([lambda:raw_http('finalize',date,b),lambda:raw_http('adjudications',date,b|{'attendanceEventId':e['attendanceEventId'],'included':True})]);check(sorted(x[0] for x in rr)==[201,409],'adjudication/finalize serialized')
    date='2030-02-04';l=create_leave(date);b=token(date);rr=race([lambda:raw_http('finalize',date,b),lambda:cancel(l)])
    check(rr[0][0] in [201,409],'Leave cancellation/finalization coherent')
    if rr[0][0]==201:check(review(date)['isStale'],'concurrent cancellation detected after finalization')
    # SQL constraints and unique revision protections.
    rid=review('2030-01-11')['latestHistoricalFinalizedRevision']['id']
    sql_reject('UPDATE FinalizedAttendanceRevisions SET ScheduledMilliseconds=-1 WHERE Id='+ident(rid),'SQL negative duration rejected')
    sql_reject("UPDATE FinalizedAttendanceRevisions SET SnapshotJson='invalid' WHERE Id="+ident(rid),'SQL malformed snapshot rejected')
    sql_reject("UPDATE AttendanceReviewActions SET Action='Forged' WHERE EmployeeId="+ident(EMP),'SQL invalid action shape rejected')
    sql_reject("UPDATE AttendanceReviewActions SET BusinessDate='2029-01-01' WHERE EmployeeId="+ident(EMP),'SQL employee/date scope foreign keys enforced')
    sql_reject('UPDATE FinalizedAttendanceRevisions SET Revision=0 WHERE Id='+ident(rid),'SQL positive revision enforced')
    sql_reject('UPDATE FinalizedAttendanceRevisions SET CoverageTruncationResidualMilliseconds=99 WHERE Id='+ident(rid),'SQL coverage identity enforced')
    swagger=json.load(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json'));base='/api/employees/{employeeId}/attendance-days/{date}'
    for route,verb in [('review','get'),('history','get'),('corrections','post'),('adjudications','post'),('confirm-absence','post'),('finalize','post'),('reopen','post')]:check(verb in swagger['paths'][base+'/'+route] and '409' in swagger['paths'][base+'/'+route][verb]['responses'],'Swagger '+route)
    now=snapshot();check(all(now[t]==baseline[t] for t in baseline if t.startswith('Payroll') or t.startswith('EmployeePayroll') or t in ['Employees','EmploymentRecords','Attendance','EmployeeLeaveSandwichCases','EmployeeLeaveSandwichAllocations']),'Attendance has no Payroll/core/legacy/sandwich mutation')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        clauses=[]
        # Delete only recorded fixture identifiers, never a broad employee/date range.
        for table,group in [('AttendanceReviewActions','actions'),('FinalizedAttendanceRevisions','revisions'),('AttendanceReviewCases','cases')]:
            if review_ids[group]:clauses.append('DELETE '+table+' WHERE Id IN ('+','.join(map(ident,sorted(review_ids[group])))+')')
        if event_ids:clauses.append('DELETE AttendanceEvents WHERE AttendanceEventId IN ('+','.join(map(ident,event_ids))+')')
        if leave_ids:
            ids=','.join(map(ident,leave_ids));clauses += [f'DELETE EmployeeLeaveAllocations WHERE EmployeeLeaveId IN ({ids})',f'DELETE EmployeeLeave WHERE LeaveId IN ({ids})']
        if assignment_ids:clauses.append('DELETE EmployeeWorkCalendarAssignments WHERE Id IN ('+','.join(map(ident,assignment_ids))+')')
        if policy_ids:clauses.append('DELETE LeavePolicies WHERE Id IN ('+','.join(map(ident,policy_ids))+')')
        if calendar_ids:
            ids=','.join(map(ident,calendar_ids));clauses += [f'DELETE WorkCalendarOverrideIntervals WHERE WorkCalendarDateOverrideId IN (SELECT Id FROM WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids}))',f'DELETE WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendarWeeklyIntervals WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendars WHERE Id IN ({ids})']
        if clauses:sql('SET XACT_ABORT ON; BEGIN TRAN; '+';'.join(clauses)+';COMMIT;')
        after=snapshot();check(after==baseline,'exact 77-table baseline restored')
        check(len(after['Employees'])==1 and not json.loads(after['Employees'][0])['IsActive'],'TEST-EMP-001 remains inactive')
        OUT.write_text(json.dumps({'checks':len(results),'error':error,'results':results,'counts':{t:len(v) for t,v in after.items()}},indent=2),encoding='utf-8')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
if error:raise SystemExit(error)
print('PASS:',len(results),'D9D live checks; exact baseline restored.')
