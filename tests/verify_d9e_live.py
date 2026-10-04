"""D9E read/query fixtures. Reuse D9D mutation helpers for setup only; localhost/SIAMIS only."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d9d_live.py').read_text().split("try:\n    check(len(baseline)==77")[0])
import datetime,time
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d9e-live-results.json';employee_ids=[];performance={}
def overview(date,query=''):return api('GET','attendance/days/'+date+query)
def report(date):
    data=overview(date);return next(r for r in data['rows']['items'] if r['employeeId'].lower()==EMP.lower())
def history(a,b,employee=EMP):return api('GET',f'employees/{employee}/attendance-history?from={a}&to={b}')
def summary(a,b):return api('GET',f'employees/{EMP}/attendance-summary?from={a}&to={b}')
def compare(date):
    r=report(date);d=review(date)
    check(r['live']['scheduledMilliseconds']==d['calculation']['scheduledMilliseconds'] and r['live']['isLate']==d['calculation']['isLateUnderCurrentPolicy'] and r['isStale']==d['isStale'] and r['isCurrentlyValidated']==d['isCurrentlyValidated'],'D9D calculation/validity parity '+date)
    return r
def measure(label,path):
    log=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d9e-api.log';offset=log.stat().st_size
    r=api('GET',path);time.sleep(.1)
    with log.open('rb') as f:f.seek(offset);text=f.read().decode('utf-8',errors='replace')
    count=text.count('Executed DbCommand');performance[label]=count
    check(count>0 and count<=20,'bounded bulk SQL command count '+label+' = '+str(count))
    return r
def fixture_employee(start='2030-01-01',end=None):
    eid=str(uuid.uuid4());rid=str(uuid.uuid4());employee_ids.append(eid)
    dept='10000000-0000-0000-0000-000000000004';desig='20000000-0000-0000-0000-000000000006'
    sql(f"INSERT Employees (EmployeeId,EmployeeNumber,FirstName,LastName,IsActive,CreatedAt,UpdatedAt) VALUES ({ident(eid)},'D9E-'+LEFT(REPLACE(CONVERT(varchar(36),{ident(eid)}),'-',''),16),'Temporary','Reporting',0,SYSUTCDATETIME(),SYSUTCDATETIME()); INSERT EmploymentRecords (EmploymentRecordId,EmployeeId,DepartmentId,DesignationId,EmploymentTypeId,EmploymentStatusId,HireDate,StartDate,EndDate,IsCurrent) VALUES ({ident(rid)},{ident(eid)},{ident(dept)},{ident(desig)},'30000000-0000-0000-0000-000000000001','40000000-0000-0000-0000-000000000001','{start}',NULL,"+("NULL" if end is None else "'"+end+"'")+", "+('1' if end is None else '0')+")")
    return eid
try:
    check(snapshot()==json.loads((OUT.parent/'d9e-baseline.json').read_text()),'exact inspected D9E baseline')
    check(overview('2026-09-28')['populationCount']==0,'daily excludes before hire')
    r=overview('2030-01-07');check(r['populationCount']==1 and r['counts']['configurationRequired']==1 and r['rows']['items'][0]['workState']=='ConfigurationRequired','inactive historically employed staff included with missing calendar structured')
    check(history('2026-09-27','2026-09-30')['dates'][0]['businessDate']=='2026-09-29','history inclusive effective hire boundary')
    check(summary('2030-01-07','2030-01-07')['configurationRequiredDays']==1 and not summary('2030-01-07','2030-01-07')['isComplete'],'missing calendar makes official summary incomplete')
    c=api('POST','work-calendars',{'code':'D9D-VERIFY','name':'D9D synthetic continuous'},201);calendar_ids.append(c['id'])
    for d in range(7):api('POST',f"work-calendars/{c['id']}/weekly-intervals",{'dayOfWeek':d,'startTime':'07:30','endTime':'16:00'},201)
    a=api('POST',f'employees/{EMP}/work-calendar-assignments',{'workCalendarId':c['id'],'effectiveFrom':'2030-01-01','effectiveTo':'2030-12-31'},201);assignment_ids.append(a['id'])
    for t,version in [(paid,'PAID'),(unpaid,'UNPAID')]:
        p=api('POST','leave-policies',{'leaveTypeId':t,'version':'D9D-'+version,'effectiveFrom':'2030-01-01','effectiveTo':'2030-12-31','balanceTracked':False,'allowsSuddenRequest':True},201);policy_ids.append(p['id']);api('POST',f"leave-policies/{p['id']}/publish",{})


    a=api('POST',f'employees/{EMP}/work-calendar-assignments',{'workCalendarId':calendar_ids[0],'effectiveFrom':'2026-09-29','effectiveTo':'2026-10-04'},201);assignment_ids.append(a['id'])
    pair('2026-09-30','07:30:00','16:00:00');q=api('GET','attendance/review-queue?from=2026-09-29&to=2026-10-03')
    check(q['counts']['readyToFinalize']>=1 and any(r['businessDate']=='2026-09-30' and r['recordState']=='UnfinalizedPastDay' for r in q['rows']['items']),'past ready/unfinalized attention queue without persistent case')
    check(not rows('SELECT Id FROM AttendanceReviewCases'),'queue does not create review cases')
    dates=[]
    for date,start,late in [('2030-01-07','07:30:00',False),('2030-01-08','07:15:00',False),('2030-01-09','07:35:00.0000000',False),('2030-01-10','07:35:00.0000001',True),('2030-01-11','07:30:00.0000001',False)]:
        dates.append(date);pair(date,start,'16:00:00');finalize(date);r=compare(date)
        check(r['timingState']==('Late' if late else 'OnTime') and r['official']['isLate']==late and r['currentPresence']=='Unknown','factual timing/unknown presence '+date)
    check(report('2030-01-11')['official']['coverageTruncationResidualMilliseconds']==1 and report('2030-01-11')['official']['unexplainedScheduledMilliseconds']==0,'snapshot residual not absence')
    for date,t in [('2030-01-12',paid),('2030-01-13',unpaid)]:
        create_leave(date,t=t);finalize(date);r=compare(date);check(r['live']['leaveExtent']=='Full' and r['live']['leaveState']==('Paid' if t==paid else 'Unpaid'),'Paid/Unpaid full coverage '+date)
    pair('2030-01-14','09:00:00','15:30:00');create_leave('2030-01-14','07:30','09:00');create_leave('2030-01-14','15:30','16:00',unpaid);finalize('2030-01-14');r=compare('2030-01-14')
    check(r['official']['leaveExtent']=='Partial' and r['official']['leaveState']=='Mixed' and r['official']['paidLeaveCoveredMilliseconds']==5400000 and r['official']['unpaidLeaveCoveredMilliseconds']==1800000,'mixed partial Leave, exact durations not arbitrary days')
    r=report('2030-01-15');check(r['live']['potentialAbsence'] and not r['live']['isConfirmedAbsent'] and r['official'] is None,'provisional no-evidence finding not confirmed absence')
    command('2030-01-16','confirm-absence');finalize('2030-01-16');r=compare('2030-01-16');check(r['official']['isConfirmedAbsent'] and not r['requiresReview'],'only D9D confirmation becomes official absence')
    event('2030-01-17','In','07:30:00');r=report('2030-01-17');check(r['requiresReview'] and r['timingState']=='Unknown' and r['currentPresence']=='Unknown','missing OUT no invented timing/current-inside')
    correction('2030-01-17','Out','16:00:00');finalize('2030-01-17');compare('2030-01-17')
    event('2030-01-18','Out','16:00:00');check(any(f['code']=='MissingClockIn' for f in report('2030-01-18')['findings']),'missing IN structured')
    pair('2030-01-19','07:30:00','16:00:00');e=event('2030-01-19','In','08:00:00');check(any(f['code']=='DuplicateDirection' for f in report('2030-01-19')['findings']),'duplicate direction structured');decide('2030-01-19',e);finalize('2030-01-19');compare('2030-01-19')
    event('2030-01-20','Unknown','09:00:00');check(any(f['code']=='UnknownDirection' for f in report('2030-01-20')['findings']),'Unknown direction structured')
    pair('2030-01-21','07:30:00','14:00:00');l=create_leave('2030-01-21','14:00','16:00');finalize('2030-01-21');frozen=review('2030-01-21')['latestHistoricalFinalizedRevision'];cancel(l);r=compare('2030-01-21')
    check(r['isStale'] and r['requiresReopen'] and r['official'] is None and any(x['code']=='ApprovedLeaveCancelled' for x in r['changedSources']),'cancelled Leave historical/stale excluded from official')
    check(summary('2030-01-21','2030-01-21')['scheduledMilliseconds']==0 and summary('2030-01-21','2030-01-21')['staleFinalizedDays']==1,'stale date contributes no official totals')
    check(review('2030-01-21')['latestHistoricalFinalizedRevision']==frozen,'historical snapshot preserved during reporting')
    pair('2030-01-22','07:30:00','16:00:00');finalize('2030-01-22');command('2030-01-22','reopen');r=compare('2030-01-22');check(r['recordState']=='Reopened' and r['official'] is None,'reopened not official')
    finalize('2030-01-22');r=compare('2030-01-22');check(r['latestHistoricalRevision']==2 and summary('2030-01-22','2030-01-22')['scheduledMilliseconds']==30600000,'revision 2 counted exactly once')
    for date,kind in [('2030-01-23','RestDay'),('2030-01-24','PublicHoliday'),('2030-01-25','SchoolHoliday')]:
        override(date,kind);r=report(date);check(r['workState']=='NotScheduled' and not r['live']['potentialAbsence'],kind+' calendar reused');finalize(date)
    pair('2030-01-23','08:00:00','09:00:00');check(report('2030-01-23')['isStale'],'nonworking activity change surfaces staleness, no overtime')
    override('2030-01-26','ExceptionalWorkingDay',[{'startTime':'08:00','endTime':'12:00'},{'startTime':'13:00','endTime':'15:00'}]);pair('2030-01-26','08:00:00','15:00:00');finalize('2030-01-26');check(report('2030-01-26')['official']['scheduledMilliseconds']==21600000,'exception replaces weekly schedule')
    final=review('2030-01-07')['latestHistoricalFinalizedRevision'];override('2030-01-07','ExceptionalWorkingDay',[{'startTime':'08:00','endTime':'16:00'}]);r=report('2030-01-07');check(r['isStale'] and r['official'] is None and review('2030-01-07')['latestHistoricalFinalizedRevision']==final,'calendar change never rewrites official snapshot')
    h=history('2030-01-07','2030-01-26');check(len(h['dates'])==20 and [r['businessDate'] for r in h['dates']]==sorted(set(r['businessDate'] for r in h['dates'])),'history one ordered row per effective date')
    s=summary('2030-01-07','2030-01-26');official=[r['official'] for r in h['dates'] if r['official'] is not None]
    for field in ['scheduledMilliseconds','presenceCoveredScheduledMilliseconds','approvedLeaveCoveredScheduledMilliseconds','paidLeaveCoveredMilliseconds','unpaidLeaveCoveredMilliseconds','unexplainedScheduledMilliseconds','coverageTruncationResidualMilliseconds']:
        check(s[field]==sum(r[field] or 0 for r in official),'official summary exact frozen aggregation '+field)
    check(s['lateDays']==1 and s['confirmedAbsenceDays']==1 and s['partialLeaveDates']==1 and s['staleFinalizedDays']==3 and not s['isComplete'],'official counts and completeness mixed states')
    check(s['scheduledMilliseconds']==s['presenceCoveredScheduledMilliseconds']+s['approvedLeaveCoveredScheduledMilliseconds']+s['unexplainedScheduledMilliseconds']+s['coverageTruncationResidualMilliseconds'],'wide aggregate coverage identity')
    check(overview('2030-01-10','?late=true')['rows']['totalCount']==1 and overview('2030-01-10','?late=false')['rows']['totalCount']==0,'typed Late filter')
    check(overview('2030-01-21','?isStale=true')['rows']['totalCount']==1 and overview('2030-01-21','?recordState=Finalized')['rows']['totalCount']==0,'typed validity filters')
    q=api('GET','attendance/review-queue?from=2030-01-07&to=2030-01-26&isStale=true&pageSize=1');check(q['rows']['totalCount']==3 and len(q['rows']['items'])==1 and q['counts']['stale']==3,'attention pagination counts cover filtered set, not page')
    q2=api('GET','attendance/review-queue?from=2030-01-07&to=2030-01-26&isStale=true&pageSize=1&page=2');check(q['rows']['items'][0]['businessDate']<q2['rows']['items'][0]['businessDate'],'queue stable date sort/page')
    # Same batched command count for a single date, more staff and many dates.
    measure('one-employee-one-date','attendance/days/2030-01-08')
    closed=fixture_employee('2030-01-08','2030-01-10');future=fixture_employee('2031-01-01');missing=fixture_employee()
    for _ in range(8):fixture_employee()
    many=measure('eleven-employees-one-date','attendance/days/2030-01-08?pageSize=100')
    check(many['populationCount']==11 and many['counts']['configurationRequired']==10,'one bad employee cannot break overview')
    check(performance['one-employee-one-date']==performance['eleven-employees-one-date'],'SQL command count independent of employee count')
    measure('eleven-employees-twenty-dates','attendance/review-queue?from=2030-01-07&to=2030-01-26')
    check(performance['eleven-employees-twenty-dates']==performance['one-employee-one-date'],'SQL command count independent of employee/date product')
    check([r['businessDate'] for r in history('2030-01-07','2030-01-11',closed)['dates']]==['2030-01-08','2030-01-09','2030-01-10'],'closed employment inclusive endpoints, omit outside dates')
    original=rows("SELECT Id,SnapshotJson FROM FinalizedAttendanceRevisions WHERE EmployeeId="+ident(EMP)+" AND BusinessDate='2030-01-08'")[0]
    try:
        sql("UPDATE FinalizedAttendanceRevisions SET SnapshotJson='{}' WHERE Id="+ident(original['Id']))
        bad=overview('2030-01-08');row=next(r for r in bad['rows']['items'] if r['employeeId'].lower()==EMP)
        check(row['recordState']=='SnapshotInvalid' and row['official'] is None and row['requiresReview'] and bad['populationCount']==11,'one invalid snapshot excludes official facts without failing dashboard')
    finally:sql("UPDATE FinalizedAttendanceRevisions SET SnapshotJson=N'"+original['SnapshotJson'].replace("'","''")+"' WHERE Id="+ident(original['Id']))
    check(history('2030-01-01','2030-01-31',future)['dates']==[],'future hire no fabricated history')
    check(overview('2030-01-08','?departmentId=10000000-0000-0000-0000-000000000001')['populationCount']==0,'effective department filter')
    first=overview('2030-01-08','?pageSize=2');second=overview('2030-01-08','?pageSize=2&page=2');check(first['rows']['totalCount']==11 and len(set(r['employeeId'] for r in first['rows']['items']+second['rows']['items']))==4,'daily pagination no duplicate staff')
    now=api('GET','attendance/today');expected=(datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(hours=7)).date().isoformat();check(now['businessDate']==expected and now['businessTimeZone']=='Asia/Bangkok','today uses business timezone')
    # Strict bounds and query errors.
    for path in ['attendance/review-queue','attendance/review-queue?from=2030-01-01&to=2030-02-01',f'employees/{EMP}/attendance-history?from=2030-01-01&to=2031-01-02',f'employees/{EMP}/attendance-summary?from=2030-01-02&to=2030-01-01','attendance/days/0001-01-01','attendance/days/2030-01-08?pageSize=101','attendance/days/2030-01-08?page=2147483647','attendance/days/2030-01-08?recordState=Invalid','attendance/days/2030-01-08?late=invalid','attendance/days/2030-01-08?departmentId=not-a-guid']:
        api('GET',path,status=400);check(True,'invalid query 400 '+path)
    api('GET',f'employees/{uuid.uuid4()}/attendance-history?from=2030-01-01&to=2030-01-02',status=404);check(True,'unknown employee history 404')
    for verb in ['POST','PUT','PATCH','DELETE']:api(verb,'attendance/days/2030-01-08',{},405);check(True,'GET-only '+verb+' 405')
    text=json.dumps(history('2030-01-07','2030-01-26')).lower()
    for field in ['reason','salary','bankaccount','documenttypeid','requiredDocumentTypeIds'.lower(),'supportingdocumentpolicy','actoruserid','sourcekey','externalEventId'.lower(),'snapShotJson'.lower()]:check('"'+field+'"' not in text,'privacy field omitted '+field)
    before_reads=snapshot()
    for date in ['2030-01-08','2030-01-21','2030-01-23']:overview(date)
    history('2030-01-07','2030-01-26');summary('2030-01-07','2030-01-26');api('GET','attendance/review-queue?from=2030-01-07&to=2030-01-26')
    check(snapshot()==before_reads,'all D9E reports are read-only including stale views')
    swagger=json.load(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json'))
    for path in ['/api/attendance/days/{date}','/api/attendance/today','/api/attendance/review-queue','/api/employees/{employeeId}/attendance-history','/api/employees/{employeeId}/attendance-summary']:check(set(swagger['paths'][path])=={'get'} and '200' in swagger['paths'][path]['get']['responses'],'Swagger GET-only '+path)
    check('official' in swagger['components']['schemas']['AttendanceReportRow']['properties'],'Swagger separate nullable official facts')
    now=snapshot();check(all(now[t]==baseline[t] for t in baseline if t.startswith('Payroll') or t.startswith('EmployeePayroll') or t=='Attendance' or t.startswith('EmployeeLeaveSandwich')),'no Payroll/legacy/sandwich mutations')
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
        if employee_ids:
            ids=','.join(map(ident,employee_ids));clauses += [f'DELETE EmploymentRecords WHERE EmployeeId IN ({ids})',f'DELETE Employees WHERE EmployeeId IN ({ids})']
        if clauses:sql('SET XACT_ABORT ON; BEGIN TRAN; '+';'.join(clauses)+';COMMIT;')
        after=snapshot();check(after==baseline,'exact 77-table baseline restored')
        check(len(after['Employees'])==1 and not json.loads(after['Employees'][0])['IsActive'],'TEST-EMP-001 remains inactive')
        OUT.write_text(json.dumps({'performance':performance,'checks':len(results),'error':error,'results':results,'counts':{t:len(v) for t,v in after.items()}},indent=2),encoding='utf-8')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
if error:raise SystemExit(error)
print('PASS:',len(results),'D9E live checks; exact baseline restored.')
