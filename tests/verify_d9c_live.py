"""D9C calculated reads, real frozen Leave, exact clocks and coherent races. localhost/SIAMIS only."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
from concurrent.futures import ThreadPoolExecutor
import threading

OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d9c-live-results.json'
baseline=snapshot(); event_ids=[]; calendar_ids=[]; policy_ids=[]; leave_ids=[]; assignment_ids=[]; error=None
(OUT.parent/'d9c-baseline.json').write_text(json.dumps(baseline),encoding='utf-8')
paid=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-001'")[0]['Id'].lower()
unpaid=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-003'")[0]['Id'].lower()

def day(date):return api('GET',f'employees/{EMP}/attendance-days/{date}')
def event(date,d,t):
    r=api('POST',f'employees/{EMP}/attendance-events/manual',{'occurredAt':date+'T'+t+'+07:00','direction':d,'manualRequestKey':str(uuid.uuid4()),'reason':'D9C synthetic verification'},201)
    event_ids.append(r['attendanceEventId']);return r
def pair(date,a,b):event(date,'In',a);event(date,'Out',b)
def partition(r,label):
    check(r['coveragePartitionAvailable'],label+' partition available')
    check(r['scheduledMilliseconds']==sum(r[k] for k in ['presenceCoveredScheduledMilliseconds','approvedLeaveCoveredScheduledMilliseconds','unexplainedScheduledMilliseconds','coverageTruncationResidualMilliseconds']),label+' millisecond identity')
    check(r['coverageTruncationResidualMilliseconds']>=0,label+' residue nonnegative')
def create_leave(date,a=None,b=None,t=paid,approve=True):
    body={'leaveTypeId':t,'startDate':date,'endDate':date,'requestMode':'FullDay' if a is None else 'Timed','noticeCategory':'Foreseeable','reason':'D9C temporary Leave'}
    if a is not None:body|={'requestedStartTime':a,'requestedEndTime':b}
    r=api('POST',f'employees/{EMP}/leave',body,201);leave_ids.append(r['leaveId'])
    if approve:r=api('POST',f"employees/{EMP}/leave/{r['leaveId']}/approve",{})
    return r
def cancel(r):api('POST',f"employees/{EMP}/leave/{r['leaveId']}/cancel",{'expectedStatus':r['status'],'cancellationRemarks':'D9C verification cleanup'})
def override(date,kind,intervals=None):
    body={'date':date,'overrideType':kind}
    if intervals is not None:body['intervals']=intervals
    return api('POST',f"work-calendars/{calendar_ids[0]}/overrides",body,201)
def race(calls):
    barrier=threading.Barrier(len(calls))
    def run(f):barrier.wait();return f()
    with ThreadPoolExecutor(max_workers=len(calls)) as p:return list(p.map(run,calls))

try:
    check(len(baseline)==74 and len(baseline['Employees'])==1 and not baseline['AttendanceEvents'],'D9B baseline 74 tables, one employee, zero events')
    check(day('2030-01-07')['readiness']=='WorkCalendarNotConfigured','no default-calendar fallback')
    check(day('2026-09-28')['readiness']=='NotEmployed','effective employment precedes calculation')
    api('GET',f'employees/{uuid.uuid4()}/attendance-days/2030-01-07',status=404);check(True,'unknown employee 404')
    for d in ['invalid','0001-01-01']:api('GET',f'employees/{EMP}/attendance-days/{d}',status=400);check(True,'invalid/boundary date 400 '+d)
    c=api('POST','work-calendars',{'code':'D9C-VERIFY','name':'D9C synthetic continuous'},201);calendar_ids.append(c['id'])
    for d in range(7):api('POST',f"work-calendars/{c['id']}/weekly-intervals",{'dayOfWeek':d,'startTime':'07:30','endTime':'16:00'},201)
    a=api('POST',f'employees/{EMP}/work-calendar-assignments',{'workCalendarId':c['id'],'effectiveFrom':'2030-01-01','effectiveTo':'2030-12-31'},201);assignment_ids.append(a['id'])
    for t,version in [(paid,'PAID'),(unpaid,'UNPAID')]:
        p=api('POST','leave-policies',{'leaveTypeId':t,'version':'D9C-'+version,'effectiveFrom':'2030-01-01','effectiveTo':'2030-12-31','balanceTracked':False,'allowsSuddenRequest':True},201);policy_ids.append(p['id']);api('POST',f"leave-policies/{p['id']}/publish",{})
    pair('2030-01-07','07:30:00','16:00:00');r=day('2030-01-07');partition(r,'normal')
    check(r['readiness']=='Ready' and r['observedPresenceMilliseconds']==30600000 and not r['isLateUnderCurrentPolicy'] and r['coverageTruncationResidualMilliseconds']==0,'normal complete coverage')
    pair('2030-01-08','07:15:00','16:20:00');r=day('2030-01-08');partition(r,'early/late')
    check(r['observedPresenceMilliseconds']==32700000 and r['presenceCoveredScheduledMilliseconds']==30600000,'outside schedule diagnostic only')
    for d,t,late in [('2030-01-09','07:34:59.999',False),('2030-01-10','07:35:00.0000000',False),('2030-01-11','07:35:00.0000001',True)]:
        pair(d,t,'16:00:00');r=day(d);partition(r,t)
        check(r['isLateUnderCurrentPolicy']==late,t+' exact grace classification')
        if late:check(r['rawStartVarianceTicks']==3000000001 and r['rawStartVarianceMilliseconds']==300000 and r['coverageTruncationResidualMilliseconds']==1,'100ns beyond grace raw precision/residue')
    pair('2030-01-12','07:30:00.0000001','16:00:00');r=day('2030-01-12');partition(r,'100ns arrival')
    check(r['rawStartVarianceTicks']==1 and r['unexplainedScheduledMilliseconds']==0 and r['coverageTruncationResidualMilliseconds']==1 and r['readiness']=='Ready','residue not absence/undertime/review')
    r=day('2030-01-13');partition(r,'no evidence');check(r['potentialAbsence'] and r['readiness']=='RequiresReview' and r['unexplainedScheduledMilliseconds']==30600000,'potential absence never finalized')
    for d,t in [('2030-01-14',paid),('2030-01-15',unpaid)]:
        leave=create_leave(d,t=t);r=day(d);partition(r,'full Leave '+d)
        check(r['readiness']=='Ready' and not r['potentialAbsence'] and r['unexplainedScheduledMilliseconds']==0 and r['approvedLeaveCoveredScheduledMilliseconds']==30600000,'full Approved Leave covers no clocks')
        check(r['approvedLeaves'][0]['leaveId']==leave['leaveId'] and r['approvedLeaves'][0]['snapshotVersion']==1 and r['approvedLeaves'][0]['observedStatus']=='Approved' and r['approvedLeaves'][0]['isPaid']==(t==paid),'frozen Leave provenance/classification')
    pair('2030-01-16','07:30:00.0000001','15:30:00');create_leave('2030-01-16','15:30','16:00');r=day('2030-01-16');partition(r,'presence Leave residue')
    check(r['paidLeaveCoveredMilliseconds']==1800000 and r['presenceCoveredScheduledMilliseconds']==28799999 and r['unexplainedScheduledMilliseconds']==0 and r['coverageTruncationResidualMilliseconds']==1,'approved precision example in SQL/API')
    pair('2030-01-17','09:00:00','16:00:00');create_leave('2030-01-17','07:30','09:00');r=day('2030-01-17');partition(r,'morning Leave')
    check(r['expectedArrivalUtc']=='2030-01-17T02:00:00Z' and r['rawStartVarianceTicks']==0 and not r['isLateUnderCurrentPolicy'],'morning Leave shifts required arrival')
    pair('2030-01-22','08:55:00','16:00:00');create_leave('2030-01-22','07:30','09:00');r=day('2030-01-22')
    check(r['expectedArrivalUtc']=='2030-01-22T02:00:00Z' and r['isLateUnderCurrentPolicy']==False and r['readiness']=='RequiresReview' and not r['coveragePartitionAvailable'],'early arrival during morning Leave not late; overlap retains review')
    pair('2030-01-18','07:30:00','16:00:00');create_leave('2030-01-18','14:00','16:00');r=day('2030-01-18')
    check(r['readiness']=='RequiresReview' and len(r['presenceLeaveOverlapIntervals'])==1 and r['presenceCoveredScheduledMilliseconds'] is None and r['coverageTruncationResidualMilliseconds'] is None,'overlap preserves sources without precedence or double totals')
    pair('2030-01-19','09:00:00','15:30:00');create_leave('2030-01-19','07:30','09:00');create_leave('2030-01-19','15:30','16:00',unpaid);r=day('2030-01-19');partition(r,'multiple disjoint Leave')
    check(len(r['approvedLeaves'])==2 and r['paidLeaveCoveredMilliseconds']==5400000 and r['unpaidLeaveCoveredMilliseconds']==1800000,'multiple Approved paid/unpaid intervals')
    pending=create_leave('2030-01-20',approve=False);r=day('2030-01-20');check(r['approvedLeaves']==[] and r['potentialAbsence'],'Pending Leave not coverage');cancel(pending)
    cancelled=create_leave('2030-01-21');cancel(cancelled);check(day('2030-01-21')['approvedLeaves']==[],'Cancelled Leave not coverage')
    for d,ds in [('2030-02-01',['In']),('2030-02-02',['Out']),('2030-02-03',['In','In','Out']),('2030-02-04',['Out','Out']),('2030-02-05',['Unknown'])]:
        for i,direction in enumerate(ds):event(d,direction,f'{8+i:02}:00:00')
        r=day(d);check(r['readiness']=='RequiresReview' and not r['coveragePartitionAvailable'] and len(r['events'])==len(ds) and r['observedPresenceIntervals']==[],'ambiguous evidence retained '+str(ds))
    event('2030-02-06','In','08:00:00');event('2030-02-06','Out','08:00:00');r=day('2030-02-06');check(any(f['code']=='ExactTimestampConflict' for f in r['findings']),'same instant not ordered by GUID')
    event('2030-02-07','Out','16:00:00');event('2030-02-07','In','07:30:00');r=day('2030-02-07');partition(r,'out-of-order ingestion');check(r['readiness']=='Ready','occurrence ordering independent of receipt')
    for d,kind in [('2030-02-08','PublicHoliday'),('2030-02-09','SchoolHoliday'),('2030-02-10','RestDay')]:
        override(d,kind);r=day(d);partition(r,kind);check(r['readiness']=='Ready' and r['scheduledMilliseconds']==0 and not r['potentialAbsence'],kind+' no missing work')
    pair('2030-02-10','08:00:00','09:00:00');r=day('2030-02-10');check(r['observedPresenceMilliseconds']==3600000 and r['presenceCoveredScheduledMilliseconds']==0,'holiday presence no overtime inference')
    split=[{'startTime':'07:30','endTime':'12:00'},{'startTime':'13:00','endTime':'16:00'}]
    override('2030-02-11','ExceptionalWorkingDay',split);pair('2030-02-11','07:25:00','16:05:00');r=day('2030-02-11');partition(r,'split lunch');check(r['scheduledMilliseconds']==27000000 and r['observedPresenceMilliseconds']==31200000,'split unscheduled lunch excluded')
    override('2030-02-12','ExceptionalWorkingDay',split)
    for direction,time in [('Out','15:59:59.9999999'),('In','13:00:00.0000001'),('Out','11:59:59.9999999'),('In','07:30:00.0000001')]:event('2030-02-12',direction,time)
    r=day('2030-02-12');partition(r,'multiple residual partitions');check(len(r['unexplainedScheduledIntervals'])==4 and r['coverageTruncationResidualMilliseconds']==1 and r['unexplainedScheduledMilliseconds']==0,'aggregate truncation across multiple partitions')
    for direction,time in [('In','07:30:00'),('Out','10:00:00'),('In','11:00:00'),('Out','15:30:00')]:event('2030-02-13',direction,time)
    r=day('2030-02-13');partition(r,'internal/tail');check(len(r['unexplainedScheduledIntervals'])==2 and r['unexplainedScheduledMilliseconds']==5400000,'midday and tail gaps not one undertime label')
    frozen=create_leave('2030-02-14','14:00','16:00');override('2030-02-14','ExceptionalWorkingDay',[{'startTime':'08:00','endTime':'15:00'}]);r=day('2030-02-14')
    check(any(f['code']=='LeaveScheduleMismatch' for f in r['findings']) and r['unexplainedScheduledMilliseconds'] is None and r['approvedLeaves'][0]['date']['scheduledIntervals'][0]['startTime']=='07:30:00' and r['expectedWork']['intervals'][0]['startTime']=='08:00:00','frozen/current mismatch preserved no precedence')
    # Corrupt legacy Approved evidence is diagnostic, not guessed duration coverage.
    bad_id=str(uuid.uuid4());leave_ids.append(bad_id)
    sql(f"INSERT EmployeeLeave (LeaveId,EmployeeId,LeaveTypeId,StartDate,EndDate,Days,Status) VALUES ({ident(bad_id)},{ident(EMP)},{ident(paid)},'2030-02-15','2030-02-15',1,'Approved')")
    r=day('2030-02-15');check(any(f['code']=='LeaveSnapshotInvalid' for f in r['findings']) and not r['coveragePartitionAvailable'],'invalid legacy Approved snapshot no fallback')
    sql('DELETE EmployeeLeave WHERE LeaveId='+ident(bad_id))
    before_reads=snapshot()
    for d in ['2030-01-07','2030-01-12','2030-01-16','2030-02-14']:day(d)
    check(snapshot()==before_reads,'calculated reads/residue leave every database row/timestamp unchanged')
    values=race([lambda:day('2030-02-16')]*6+[lambda:event('2030-02-16','In','07:30:00')])
    for r in values[:6]:check(len(r['events']) in [0,1] and r['coveragePartitionAvailable']==(len(r['events'])==0),'event intake/read coherent evidence and calculation')
    full=create_leave('2030-02-17')
    values=race([lambda:day('2030-02-17')]*6+[lambda:cancel(full)])
    for r in values[:6]:check((len(r['approvedLeaves'])==1 and r['unexplainedScheduledMilliseconds']==0) or (len(r['approvedLeaves'])==0 and r['potentialAbsence']),'Leave cancellation/read coherent lifecycle')
    swagger=json.load(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json'));path='/api/employees/{employeeId}/attendance-days/{date}'
    check(set(swagger['paths'][path])=={'get'} and set(swagger['paths'][path]['get']['responses'])=={'200','400','404','409'},'Swagger focused GET purpose/status codes')
    check('coverageTruncationResidualMilliseconds' in swagger['components']['schemas']['AttendanceDayDto']['properties'],'Swagger residual metadata')
    now=snapshot();check(all(now[t]==baseline[t] for t in baseline if t.startswith('Payroll') or t.startswith('EmployeePayroll') or t=='Attendance' or t=='Employees' or t=='EmploymentRecords'),'no Payroll/legacy/employee core mutations')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        clauses=[]
        if event_ids:clauses.append('DELETE AttendanceEvents WHERE AttendanceEventId IN ('+','.join(map(ident,event_ids))+')')
        if leave_ids:
            ids=','.join(map(ident,leave_ids));clauses += [f'DELETE EmployeeLeaveAllocations WHERE EmployeeLeaveId IN ({ids})',f'DELETE EmployeeLeave WHERE LeaveId IN ({ids})']
        if assignment_ids:clauses.append('DELETE EmployeeWorkCalendarAssignments WHERE Id IN ('+','.join(map(ident,assignment_ids))+')')
        if policy_ids:clauses.append('DELETE LeavePolicies WHERE Id IN ('+','.join(map(ident,policy_ids))+')')
        if calendar_ids:
            ids=','.join(map(ident,calendar_ids));clauses += [f'DELETE WorkCalendarOverrideIntervals WHERE WorkCalendarDateOverrideId IN (SELECT Id FROM WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids}))',f'DELETE WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendarWeeklyIntervals WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendars WHERE Id IN ({ids})']
        if clauses:sql('SET XACT_ABORT ON; BEGIN TRAN; '+';'.join(clauses)+';COMMIT;')
        after=snapshot();check(after==baseline,'exact 74-table baseline restored')
        check(len(after['Employees'])==1 and not json.loads(after['Employees'][0])['IsActive'],'TEST-EMP-001 remains inactive')
        OUT.write_text(json.dumps({'checks':len(results),'error':error,'results':results,'counts':{t:len(v) for t,v in after.items()}},indent=2),encoding='utf-8')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
if error:raise SystemExit(error)
print('PASS:',len(results),'D9C live checks; exact baseline restored.')
