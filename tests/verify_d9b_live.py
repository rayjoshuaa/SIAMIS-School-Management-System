"""D9B localhost/SIAMIS-only evidence, schedules, races and exact fixture cleanup."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
from concurrent.futures import ThreadPoolExecutor

OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d9b-live-results.json'
before=json.loads((OUT.parent/'d9b-before-migration.json').read_text())
baseline=snapshot(); error=None
event_ids=[]; calendar_ids=[]; assignment_ids=[]; employee_ids=[]

def expect(method,path,body=None,status=200,label=''):
    value=api(method,path,body,status);check(True,label or method+' '+path+' '+str(status));return value
def manual(direction='In',instant='2026-10-03T17:00:00.1234567Z',**changes):
    return {'occurredAt':instant,'direction':direction,'manualRequestKey':str(uuid.uuid4()),'reason':'D9B synthetic evidence'}|changes
def capture(body,employee=EMP):
    value=expect('POST',f'employees/{employee}/attendance-events/manual',body,201,'manual '+body['direction']);event_ids.append(value['attendanceEventId']);return value
def expected(date,employee=EMP): return expect('GET',f'employees/{employee}/attendance-expected-work?date={date}')
def reject_sql(statement,label,numbers=(547,)):
    values=','.join(map(str,numbers))
    output=sql(f"BEGIN TRAN; BEGIN TRY {statement}; ROLLBACK; THROW 51000,'Invalid data accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER() NOT IN ({values}) THROW; SELECT 'Rejected'; END CATCH;")
    check('Rejected' in output,label)
def send(body,employee=EMP):
    req=urllib.request.Request(BASE+f'/api/employees/{employee}/attendance-events/manual',data=json.dumps(body).encode(),method='POST',headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=30) as response: return response.status,json.loads(response.read())
    except urllib.error.HTTPError as ex: return ex.code,json.loads(ex.read())

try:
    check(len(baseline)==74 and not baseline['AttendanceEvents'],'single new empty table')
    check(all(baseline[t]==v for t,v in before.items()),'migration preserved exact previous 73-table rows/timestamps')
    check(len(rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20261003175821_AddAttendanceEvidenceFoundation'"))==1,'focused migration recorded')
    schema=rows("SELECT name,TYPE_NAME(user_type_id) AS TypeName,scale FROM sys.columns WHERE object_id=OBJECT_ID('AttendanceEvents')")
    check(len(schema)==16 and all(x['TypeName']=='datetime2' and x['scale']==7 for x in schema if x['name'] in ['OccurredAtUtc','ReceivedAtUtc']),'16 columns; exact datetime2(7) clocks')
    fks=rows("SELECT delete_referential_action_desc,is_disabled,is_not_trusted FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('AttendanceEvents')")
    check(len(fks)==1 and fks[0]['delete_referential_action_desc']=='NO_ACTION' and not fks[0]['is_disabled'] and not fks[0]['is_not_trusted'],'trusted NoAction ownership')
    checks=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('AttendanceEvents')")
    check(len(checks)==5 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in checks),'five enabled trusted checks')
    indexes=rows("SELECT name,is_unique,filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID('AttendanceEvents')")
    check(len(indexes)==4 and sum(x['is_unique'] and x.get('filter_definition') is not None for x in indexes)==2,'PK, lookup and two filtered replay indexes')
    check(expected('2026-10-05')['readiness']=='WorkCalendarNotConfigured','no calendar is unknown work, not zero-work readiness')
    check(expected('2026-09-28')['readiness']=='NotEmployed','date-effective employment precedes schedule resolution')
    expect('GET',f'employees/{uuid.uuid4()}/attendance-expected-work?date=2026-10-05',status=404)
    expect('GET',f'employees/{EMP}/attendance-expected-work',status=400)
    first_body=manual(); first=capture(first_body)
    check(first['occurredAtUtc']=='2026-10-03T17:00:00.1234567Z' and first['businessDate']=='2026-10-04','seven fractional digits and UTC boundary -> Bangkok')
    check(first['employeeWasInactive'] and first['intakeAnomalies']==['EmployeeInactiveAtReceipt'] and first['employmentReadiness']=='Ready','inactive/open employee evidence preserved with receipt anomaly')
    check(first['source']=='ManualAuthorized' and first['actorId'] is None and first['receivedAtUtc'].endswith('Z'),'server manual source, null actor, UTC receipt')
    check(first['originalSourceTimestamp']==first_body['occurredAt'],'original explicit source timestamp preserved')
    replay=expect('POST',f'employees/{EMP}/attendance-events/manual',first_body,status=200)
    check(replay==first,'identical replay returns original full receipt without rewriting timestamps/anomalies')
    equivalent=expect('POST',f'employees/{EMP}/attendance-events/manual',first_body|{'occurredAt':'2026-10-04T00:00:00.1234567+07:00'},status=200)
    check(equivalent==first,'same normalized instant with another offset is replay')
    for change in [{'direction':'Out'},{'occurredAt':'2026-10-03T17:00:01Z'},{'reason':'conflicting reason'}]:
        expect('POST',f'employees/{EMP}/attendance-events/manual',first_body|change,status=409)
    capture(manual('Out','2026-10-04T01:02:03.9876543+07:00'))
    unknown=capture(manual('Unknown'));check(unknown['direction']=='Unknown','unknown retained without alternation')
    before_employment=capture(manual('In','2026-09-28T01:00:00Z'))
    check(before_employment['employmentReadiness']=='NotEmployed' and 'NotEmployed' in before_employment['intakeAnomalies'],'no-employment receipt preserved and flagged')
    precision=rows('SELECT DATEPART(NANOSECOND,OccurredAtUtc) AS Fraction FROM AttendanceEvents WHERE AttendanceEventId='+ident(first['attendanceEventId']))
    check(precision[0]['Fraction']==123456700,'SQL preserves all 100ns digits')
    for changes in [{'reason':''},{'reason':' '},{'reason':'X'*2001},{'manualRequestKey':None},{'manualRequestKey':str(uuid.UUID(int=0))},{'direction':'Bogus'},{'direction':0},{'occurredAt':'2026-10-04T00:00:00'},{'occurredAt':'2026-10-04T00:00:00.12345678Z'},{'source':'Device'},{'actorId':str(uuid.uuid4())},{'userId':str(uuid.uuid4())},{'reviewerId':str(uuid.uuid4())},{'receivedAtUtc':'2026-10-04T00:00:00Z'},{'businessDate':'2026-10-04'},{'externalEventId':'forged'}]:
        expect('POST',f'employees/{EMP}/attendance-events/manual',manual()|changes,status=400)
    expect('POST',f'employees/{uuid.uuid4()}/attendance-events/manual',manual(),status=404)
    eid=str(uuid.uuid4());employee_ids.append(eid)
    sql(f"INSERT Employees (EmployeeId,EmployeeNumber,FirstName,LastName,IsActive,CreatedAt,UpdatedAt) VALUES ({ident(eid)},'D9B-VERIFY-NO-EMPLOYMENT','Synthetic','D9B',1,SYSUTCDATETIME(),SYSUTCDATETIME())")
    no_emp=capture(manual(),eid);check(no_emp['employmentReadiness']=='NotEmployed' and not no_emp['employeeWasInactive'],'employee with no records accepts evidence without repairing employment')
    reject_sql('DELETE Employees WHERE EmployeeId='+ident(eid),'NoAction prevents deletion of employee with evidence')
    expect('POST',f'employees/{eid}/attendance-events/manual',first_body,status=409)
    expect('GET',f"employees/{eid}/attendance-events/{first['attendanceEventId']}",status=404)
    for verb in ['PUT','DELETE']:expect(verb,f"employees/{EMP}/attendance-events/{first['attendanceEventId']}",first_body if verb=='PUT' else None,status=405)
    expect('GET',f"employees/{EMP}/attendance-events/{first['attendanceEventId']}")
    page=expect('GET',f'employees/{EMP}/attendance-events?fromDate=2026-10-04&toDate=2026-10-04&page=1&pageSize=2')
    check(len(page['items'])==2 and page['totalCount']==3,'database date filtering and paging')
    expect('GET',f'employees/{EMP}/attendance-events?pageSize=101',status=400)
    expect('GET',f'employees/{EMP}/attendance-events?fromDate=2026-10-05&toDate=2026-10-04',status=400)
    race_body=manual()
    with ThreadPoolExecutor(max_workers=8) as pool: outcomes=list(pool.map(lambda _:send(race_body),range(8)))
    event_ids += list(set(value['attendanceEventId'] for code,value in outcomes if code in [200,201]))
    check(sorted(code for code,_ in outcomes)==[200]*7+[201] and len(set(value['attendanceEventId'] for _,value in outcomes))==1,'eight same-key concurrent receipts: one201 seven200 one row')
    conflict_key=str(uuid.uuid4()); conflict_bodies=[manual(manualRequestKey=conflict_key),manual('Out',manualRequestKey=conflict_key)]
    with ThreadPoolExecutor(max_workers=2) as pool: conflicting=list(pool.map(send,conflict_bodies))
    event_ids += [v['attendanceEventId'] for code,v in conflicting if code==201]
    check(sorted(code for code,_ in conflicting)==[201,409],'concurrent conflicting payload one acceptance one409')
    c=expect('POST','work-calendars',{'code':'D9B-VERIFY-CALENDAR','name':'D9B synthetic','isDefault':True},201);calendar_ids.append(c['id'])
    check(expected('2026-10-05')['readiness']=='WorkCalendarNotConfigured','default calendar never supplies fallback')
    for start,end in [('13:00','16:00'),('08:00','12:00')]:expect('POST',f"work-calendars/{c['id']}/weekly-intervals",{'dayOfWeek':1,'startTime':start,'endTime':end},201)
    ass=expect('POST',f'employees/{EMP}/work-calendar-assignments',{'workCalendarId':c['id'],'effectiveFrom':'2026-10-01','effectiveTo':'2026-10-31'},201);assignment_ids.append(ass['id'])
    work=expected('2026-10-05');check(work['readiness']=='Ready' and not work['employeeIsActive'] and [x['startTime'] for x in work['intervals']]==['08:00:00','13:00:00'],'ordered intervals preserve lunch gap for inactive employee')
    check(work['employment']['employmentStart']=='2026-09-29' and work['assignmentId']==ass['id'] and work['businessTimeZone']=='Asia/Bangkok','context uses StartDate fallback and explicit timezone')
    check(expected('2026-10-04')['scheduleKind']=='WeeklyNonWorking','weekly nonworking valid zero schedule')
    for date,kind in [('2026-10-06','PublicHoliday'),('2026-10-07','SchoolHoliday'),('2026-10-08','RestDay')]:
        expect('POST',f"work-calendars/{c['id']}/overrides",{'date':date,'overrideType':kind},201)
        value=expected(date);check(value['scheduleKind']==kind and value['intervals']==[] and value['readiness']=='Ready',kind+' empty schedule')
    expect('POST',f"work-calendars/{c['id']}/overrides",{'date':'2026-10-12','overrideType':'ExceptionalWorkingDay','intervals':[{'startTime':'10:00','endTime':'11:30'}]},201)
    exceptional=expected('2026-10-12');check(exceptional['scheduleKind']=='ExceptionalWorkingDay' and len(exceptional['intervals'])==1 and exceptional['intervals'][0]['startTime']=='10:00:00','exceptional replaces weekly intervals')
    for date in ['2026-10-01','2026-10-31']:check(expected(date)['assignmentId']==ass['id'],'inclusive effective '+date)
    check(expected('2026-11-01')['readiness']=='WorkCalendarNotConfigured','calendar effective gap no fallback')
    expect('PUT',f"work-calendars/{c['id']}",{'code':c['code'],'name':c['name'],'isActive':False,'isDefault':False})
    check(expected('2026-10-05')['readiness']=='Ready','historical assignment ignores current calendar inactive flag')
    ambiguous=str(uuid.uuid4());assignment_ids.append(ambiguous)
    sql(f"INSERT EmployeeWorkCalendarAssignments VALUES ({ident(ambiguous)},{ident(EMP)},{ident(c['id'])},'2026-10-05','2026-10-05',SYSUTCDATETIME(),SYSUTCDATETIME())")
    check(expected('2026-10-05')['readiness']=='ConfigurationConflict','ambiguous assignment explicit readiness')
    sql('DELETE EmployeeWorkCalendarAssignments WHERE Id='+ident(ambiguous));assignment_ids.remove(ambiguous)
    # A legacy summary remains readable without becoming evidence; only this recorded fixture is removed.
    legacy=str(uuid.uuid4());status_id=rows("SELECT Id FROM AttendanceStatuses WHERE Code='ATT-001'")[0]['Id']
    sql(f"INSERT Attendance (AttendanceId,EmployeeId,AttendanceDate,AttendanceStatusId,Remarks) VALUES ({ident(legacy)},{ident(EMP)},'2026-10-05',{ident(status_id)},'D9B legacy read fixture')")
    try:
        check(len(api('GET',f'employees/{EMP}/attendance'))==1,'legacy collection GET unchanged')
        check(api('GET',f'employees/{EMP}/attendance/{legacy}')['attendanceStatusName']=='Present','legacy detail retains current status behavior')
        old={'attendanceDate':'2026-10-05','attendanceStatusId':status_id}
        expect('POST',f'employees/{EMP}/attendance',old,410)
        expect('PUT',f'employees/{EMP}/attendance/{legacy}',old,410)
        expect('DELETE',f'employees/{EMP}/attendance/{legacy}',status=410)
    finally:sql('DELETE Attendance WHERE AttendanceId='+ident(legacy))
    event_where=' WHERE AttendanceEventId='+ident(first['attendanceEventId'])
    for statement,label in [("Direction='Bad'",'invalid direction'),("Direction='in'",'case-sensitive direction'),("Source='SystemDerived'",'invalid source'),("Reason=' '",'empty manual reason'),('ManualRequestKey=NULL','missing manual key'),("SourceKey='forged'",'manual cannot claim external source'),("ActorId='00000000-0000-0000-0000-000000000001'",'manual actor null'),("EmploymentReadiness='Absent'",'no absence readiness'),("BusinessTimeZone='UTC'",'fixed business timezone')]:
        reject_sql('UPDATE AttendanceEvents SET '+statement+event_where,label+' SQL rejected')
    duplicate=str(uuid.uuid4())
    reject_sql(f"INSERT AttendanceEvents SELECT {ident(duplicate)},EmployeeId,OccurredAtUtc,BusinessDate,BusinessTimeZone,Direction,Source,SourceKey,ExternalEventId,ManualRequestKey,OriginalSourceTimestamp,ReceivedAtUtc,Reason,ActorId,EmployeeWasInactive,EmploymentReadiness FROM AttendanceEvents"+event_where,'SQL manual unique',(2601,2627))
    for source in ['Device','Imported']:
        external_id=str(uuid.uuid4());event_ids.append(external_id)
        sql(f"INSERT AttendanceEvents SELECT {ident(external_id)},EmployeeId,OccurredAtUtc,BusinessDate,BusinessTimeZone,Direction,'{source}','D9B-SOURCE-{source}','D9B-EVENT',NULL,OriginalSourceTimestamp,ReceivedAtUtc,NULL,NULL,EmployeeWasInactive,EmploymentReadiness FROM AttendanceEvents"+event_where)
        reject_sql(f"INSERT AttendanceEvents SELECT {ident(str(uuid.uuid4()))},{ident(eid)},OccurredAtUtc,BusinessDate,BusinessTimeZone,Direction,Source,SourceKey,ExternalEventId,ManualRequestKey,OriginalSourceTimestamp,ReceivedAtUtc,Reason,ActorId,EmployeeWasInactive,EmploymentReadiness FROM AttendanceEvents WHERE AttendanceEventId={ident(external_id)}",'external replay identity across employees '+source,(2601,2627))
        reject_sql('UPDATE AttendanceEvents SET ExternalEventId=NULL WHERE AttendanceEventId='+ident(external_id),'external required identity '+source)
    swagger=json.load(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json'))
    paths=swagger['paths'];prefix='/api/employees/{employeeId}'
    check(all(prefix+p in paths for p in ['/attendance-events','/attendance-events/{eventId}','/attendance-events/manual','/attendance-expected-work']),'four D9B Swagger routes')
    contract=swagger['components']['schemas']['ManualAttendanceEventRequest']['properties']
    check(set(contract)=={'occurredAt','direction','manualRequestKey','reason'},'Swagger has no forgeable source/actor/derived fields')
    check(all('410' in paths[prefix+path][verb]['responses'] for path,verb in [('/attendance','post'),('/attendance/{attendanceId}','put'),('/attendance/{attendanceId}','delete')]),'Swagger documents all retired writes')
    check(not any('AttendanceDay' in x['name'] for x in rows('SELECT name FROM sys.tables')),'no speculative daily/finalization tables')
    now=snapshot();check(all(now[t]==baseline[t] for t in baseline if t.startswith('EmployeeLeave') or t=='LeavePolicies' or t.startswith('Payroll') or t.startswith('EmployeePayroll')),'no Leave or Payroll writes from foundation')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        statements=[]
        if event_ids: statements.append('DELETE AttendanceEvents WHERE AttendanceEventId IN ('+','.join(map(ident,set(event_ids)))+')')
        if assignment_ids: statements.append('DELETE EmployeeWorkCalendarAssignments WHERE Id IN ('+','.join(map(ident,assignment_ids))+')')
        if calendar_ids:
            ids=','.join(map(ident,calendar_ids))
            statements += [f'DELETE WorkCalendarOverrideIntervals WHERE WorkCalendarDateOverrideId IN (SELECT Id FROM WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids}))',f'DELETE WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendarWeeklyIntervals WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendars WHERE Id IN ({ids})']
        if employee_ids: statements.append('DELETE Employees WHERE EmployeeId IN ('+','.join(map(ident,employee_ids))+')')
        if statements:sql('SET XACT_ABORT ON; BEGIN TRAN; '+';'.join(statements)+';COMMIT;')
        after=snapshot();check(after==baseline,'exact 74-table baseline restored including employee fields/timestamps')
        check(len(after['Employees'])==1 and not json.loads(after['Employees'][0])['IsActive'] and len(after['EmploymentRecords'])==1,'original inactive employee/open employment unchanged')
        OUT.write_text(json.dumps({'checks':len(results),'error':error,'results':results,'counts':{t:len(v) for t,v in after.items()}},indent=2),encoding='utf-8')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
if error:raise SystemExit(error)
print('PASS:',len(results),'D9B live assertions; exact baseline restored.')
