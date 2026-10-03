"""D8C synthetic leave API/SQL/concurrency fixtures. Local Development localhost/SIAMIS only.
Every fixture is removed; original application rows and timestamps are compared exactly.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
from concurrent.futures import ThreadPoolExecutor
import threading

PREFIX='D8C-VERIFY-'
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-live-results.json'
baseline=snapshot(); employee_ids=[]; calendar_ids=[]; policy_ids=[]; error=None
annual=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-001'")[0]['Id'].lower()
sick=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-002'")[0]['Id'].lower()
personal=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-003'")[0]['Id'].lower()
other=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-010'")[0]['Id'].lower()
doc=rows("SELECT Id FROM DocumentTypes WHERE Code='DOC-011'")[0]['Id'].lower()
original_paid={r['Id'].lower():r['IsPaid'] for r in rows('SELECT Id,IsPaid FROM LeaveTypes')}

def expect(method,path,body,status,label):
    r=api(method,path,body,status);check(True,label);return r

def employee(suffix,hire='2026-10-01',start='2030-01-01'):
    r=api('POST','employees',{'employeeNumber':PREFIX+suffix,'firstName':'Synthetic','lastName':'D8C','departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001','hireDate':hire,'startDate':start},201)
    employee_ids.append(r['employeeId']);return r['employeeId']

def calendar(suffix,intervals):
    r=api('POST','work-calendars',{'code':PREFIX+suffix,'name':PREFIX+suffix},201);calendar_ids.append(r['id'])
    for day in range(1,6):
        for a,b in intervals:api('POST',f"work-calendars/{r['id']}/weekly-intervals",{'dayOfWeek':day,'startTime':a,'endTime':b},201)
    return r['id']

def assign(e,c,start='2030-01-01',end=None):
    return api('POST',f'employees/{e}/work-calendar-assignments',{'workCalendarId':c,'effectiveFrom':start,'effectiveTo':end},201)

def policy(t,suffix,tracked,start='2030-01-01',end='2030-12-31',publish=True,**kw):
    body={'leaveTypeId':t,'version':PREFIX+suffix,'effectiveFrom':start,'effectiveTo':end,'balanceTracked':tracked,'allowsSuddenRequest':True}|kw
    p=api('POST','leave-policies',body,201);policy_ids.append(p['id'])
    if publish:p=api('POST',f"leave-policies/{p['id']}/publish")
    return p,body

def entitlement(e,t=annual,year=2030,minutes=10000):
    return api('POST',f'employees/{e}/leave-entitlements',{'leaveTypeId':t,'leaveYear':year,'entitledMinutes':minutes},201)

def balance(e,t=annual,year=2030):return next(x for x in api('GET',f'employees/{e}/leave-balances?leaveYear={year}') if x['leaveTypeId']==t)

def request(date='2030-01-07',end=None,t=annual,a=None,b=None,**kw):
    r={'leaveTypeId':t,'startDate':date,'endDate':end or date,'requestMode':'FullDay' if a is None else 'Timed','noticeCategory':'Foreseeable'}|kw
    if a is not None:r|={'requestedStartTime':a,'requestedEndTime':b}
    return r

def create(e,r,minutes,label):
    x=expect('POST',f'employees/{e}/leave',r,201,label)
    check(x['chargeableMinutes']==minutes and x['chargeableHours']==minutes/60 and x['status']=='Pending',label+' minute/hour/server Pending facts')
    check(x['requestedAt'].endswith('Z') and x['calculation']['businessTimeZone']=='Asia/Bangkok',label+' UTC request and Bangkok evidence')
    check(sum(a['chargeableMinutes'] for a in x['allocations'])==minutes,label+' relational allocation response')
    return x

def command(e,l,kind,body=None,status=200):
    if body is None:body={'expectedStatus':'Pending'} if kind=='cancel' else {}
    return api('POST',f"employees/{e}/leave/{l['leaveId']}/{kind}",body,status)
def cancel(e,l,approved=False):return command(e,l,'cancel',{'expectedStatus':'Approved','cancellationRemarks':'Synthetic approved cancellation'} if approved else {'expectedStatus':'Pending'})
def frozen(l):return rows('SELECT CalculationSnapshotJson,CalculationSnapshotVersion,ChargeableMinutes,RequestedAt FROM EmployeeLeave WHERE LeaveId='+ident(l['leaveId']))[0],rows('SELECT * FROM EmployeeLeaveAllocations WHERE EmployeeLeaveId='+ident(l['leaveId']))
def complete(l):return rows('SELECT * FROM EmployeeLeave WHERE LeaveId='+ident(l['leaveId'])),rows('SELECT * FROM EmployeeLeaveAllocations WHERE EmployeeLeaveId='+ident(l['leaveId']))

def raw(method,path,body):
    req=urllib.request.Request(BASE+'/api/'+path,data=json.dumps(body).encode(),method=method,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=40) as r:return r.status,json.loads(r.read())
    except urllib.error.HTTPError as e:return e.code,json.loads(e.read())

def race(calls):
    barrier=threading.Barrier(len(calls))
    def call(args):barrier.wait();return raw(*args)
    with ThreadPoolExecutor(max_workers=len(calls)) as pool:return list(pool.map(call,calls))

def constraint(query,label,number=547):
    result=sql("BEGIN TRAN; BEGIN TRY "+query+"; ROLLBACK; THROW 51000,'Invalid accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER()<>"+str(number)+" THROW; SELECT 'Rejected'; END CATCH;")
    check('Rejected' in result,label)

try:
    check(len(baseline)==66 and not baseline['EmployeeLeave'] and not baseline['EmployeeLeaveAllocations'],'66-table empty leave baseline')
    pre=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-before-migration.json').read_text(encoding='utf-8'))
    check(all(baseline[t]==v for t,v in pre.items()),'migration preserves every original application row/timestamp')
    check(rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20261003100244_AddLeaveRequestCalculationLifecycle'")!=[],'D8C migration recorded')
    checks=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name IN ('CK_EmployeeLeave_Authoritative','CK_EmployeeLeave_Lifecycle','CK_EmployeeLeave_Status','CK_LeaveAllocation_Year','CK_LeaveAllocation_Minutes')")
    check(len(checks)==5 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in checks),'five new checks enabled/trusted')
    check(rows("SELECT delete_referential_action_desc FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('EmployeeLeaveAllocations')")[0]['delete_referential_action_desc']=='NO_ACTION','allocation FK NoAction')
    check(rows("SELECT is_unique FROM sys.indexes WHERE name='IX_EmployeeLeaveAllocations_EmployeeLeaveId_LeaveYear'")[0]['is_unique'],'unique leave/year allocation SQL index')
    check(api('GET',f'employees/{EMP}/leave')==[] and api('GET',f'employees/{EMP}/attendance')==[],'original Employee leave/Attendance read remains empty')
    expect('GET',f'employees/{uuid.uuid4()}/leave',None,404,'unknown employee GET')
    e=employee('MAIN');c=calendar('SPLIT',[('08:00','12:00'),('13:00','16:00')]);c2=calendar('SHORT',[('10:00','12:00')])
    p1,b1=policy(annual,'ANNUAL1',True,end='2030-06-30')
    p2,b2=policy(annual,'ANNUAL2',True,start='2030-07-01',end='2031-12-31',foreseeableNoticeHours=72)
    expect('POST',f'employees/{e}/leave',request(),409,'missing explicit calendar does not fallback')
    a1=assign(e,c,end='2030-06-30');a2=assign(e,c2,start='2030-07-01')
    missing=expect('POST',f'employees/{e}/leave',request(),409,'missing entitlement rejected')
    check('not configured' in missing['detail'],'missing entitlement message')
    ent=entitlement(e,minutes=0)
    zero=expect('POST',f'employees/{e}/leave',request(),409,'zero entitlement insufficient')
    check('Insufficient' in zero['detail'] and balance(e)['entitledMinutes']==0,'configured zero differs from missing')
    api('POST',f"employees/{e}/leave-entitlements/{ent['id']}/adjustments",{'adjustmentMinutes':10000,'reason':PREFIX+' synthetic allowance'},201)
    check(balance(e)['adjustedEntitledMinutes']==10000,'append-only positive adjustment included')
    full=create(e,request(),420,'full split day');before=frozen(full)
    check(full['isPaid'] is True and full['calculation']['isPaid'] is True,'paid fact stored in snapshot and detail')
    unchanged=complete(full)
    for body in [{},{'expectedStatus':None},{'expectedStatus':''},{'expectedStatus':'Rejected'},{'expectedStatus':'Cancelled'},{'expectedStatus':'Other'},{'expectedStatus':0},{'expectedStatus':1},{'expectedStatus':'Pending','status':'Cancelled'}]:
        expect('POST',f"employees/{e}/leave/{full['leaveId']}/cancel",body,400,'invalid/missing cancellation precondition rejected '+str(body))
        check(complete(full)==unchanged,'invalid cancellation cannot alter header or allocation '+str(body))
    command(e,full,'cancel',{'expectedStatus':'Approved','cancellationRemarks':'Wrong source state'},409)
    check(complete(full)==unchanged and balance(e)['pendingMinutes']==420,'Approved precondition on Pending fails without writes')
    check(balance(e)['pendingMinutes']==420 and balance(e)['availableMinutes']==9580,'Pending reserves exact minutes')
    expect('POST',f'employees/{e}/leave',request(a='09:00',b='11:00'),409,'Pending full day overlap')
    approved=command(e,full,'approve',{'reviewRemarks':'Synthetic review'})
    check(approved['status']=='Approved' and approved['reviewedAt'].endswith('Z') and approved['supportingDocumentRequired']==False,'approved timestamp and document metadata')
    check(balance(e)['usedMinutes']==420 and balance(e)['pendingMinutes']==0,'approval Pending to Used')
    unchanged=complete(full)
    command(e,full,'cancel',{'expectedStatus':'Pending','cancellationRemarks':'Stale Pending intent'},409)
    check(complete(full)==unchanged and balance(e)['usedMinutes']==420,'stale Pending cancellation after approval returns 409 without any writes')
    expect('POST',f'employees/{e}/leave',request(a='09:00',b='11:00'),409,'Approved overlap')
    command(e,full,'approve',status=409);command(e,full,'reject',status=409);check(True,'repeated/invalid approval rejected')
    command(e,full,'cancel',{'expectedStatus':'Approved'},status=400);check(True,'Approved cancellation reason mandatory')
    cancelled=cancel(e,full,True)
    check(cancelled['reviewedAt']==approved['reviewedAt'] and cancelled['cancelledAt'].endswith('Z') and frozen(full)==before,'Approved cancellation retains review, snapshot and allocation evidence')
    check(balance(e)['usedMinutes']==0 and balance(e)['availableMinutes']==10000,'Approved cancellation releases usage')
    command(e,full,'cancel',status=409);command(e,full,'approve',status=409);check(True,'Cancelled terminal')
    sql('UPDATE LeaveTypes SET IsPaid=0 WHERE Id='+ident(annual))
    historical=api('GET',f"employees/{e}/leave/{full['leaveId']}")
    historical_list=api('GET',f'employees/{e}/leave')
    historical_history=api('GET',f'leave-requests?employeeId={e}&leaveTypeId={annual}&pageSize=100')['items']
    check(historical['isPaid'] is True and historical['calculation']['isPaid'] is True and next(x for x in historical_list if x['leaveId']==full['leaveId'])['isPaid'] is True and next(x for x in historical_history if x['leaveId']==full['leaveId'])['isPaid'] is True,'Paid historical classification survives live LeaveType change in detail/list/history')
    newly_unpaid=create(e,request(),420,'new request uses current Unpaid classification')
    check(newly_unpaid['isPaid'] is False and newly_unpaid['calculation']['isPaid'] is False and frozen(full)==before,'new Unpaid classification leaves prior Paid evidence unchanged')
    cancel(e,newly_unpaid);sql('UPDATE LeaveTypes SET IsPaid=1 WHERE Id='+ident(annual))
    again=create(e,request(),420,'Cancelled does not block');cancel(e,again)
    rejected=create(e,request(),420,'same date reusable');rBefore=frozen(rejected);command(e,rejected,'reject',{'reviewRemarks':'Synthetic rejection'})
    check(balance(e)['pendingMinutes']==0 and frozen(rejected)==rBefore,'rejection releases and retains evidence')
    command(e,rejected,'cancel',status=409);command(e,rejected,'approve',status=409);check(True,'Rejected terminal')
    again=create(e,request(),420,'Rejected does not block');cancel(e,again)
    first=create(e,request(a='09:00',b='11:00'),120,'hourly morning');second=create(e,request(a='13:00',b='15:00'),120,'disjoint hourly afternoon')
    expect('POST',f'employees/{e}/leave',request(a='10:00',b='14:00'),409,'exact charged overlap across lunch')
    cancel(e,first);cancel(e,second)
    lunch=create(e,request(a='11:00',b='14:00'),120,'cross lunch precise');cancel(e,lunch)
    for body,label in [(request(a='12:00',b='13:00'),'lunch only'),(request('2030-01-13'),'nonworking Sunday'),(request(a='11:00',b='10:00'),'inverted time'),(request(a='09:00:01',b='11:00'),'subminute time'),(request(requestedStartTime='09:00'),'FullDay with time'),(request('2030-01-08',end='2030-01-07'),'inverted dates'),(request(requestMode='Other'),'invalid mode'),(request(noticeCategory='Other'),'invalid notice')]:
        expect('POST',f'employees/{e}/leave',body,400,label+' rejected')
    for field,value in [('status','Approved'),('chargeableMinutes',1),('days',1),('calculationSnapshotJson','{}'),('requestedAt','2030-01-01'),('reviewedAt','2030-01-01'),('cancelledAt','2030-01-01'),('reviewerId',e),('availableMinutes',123),('allocations',[])]:
        expect('POST',f'employees/{e}/leave',request(**{field:value}),400,'forged '+field+' rejected')
    expect('POST',f'employees/{e}/leave',request(t=str(uuid.uuid4())),404,'unknown leave type')
    expect('PUT',f"employees/{e}/leave/{full['leaveId']}",request(),405,'legacy PUT removed')
    expect('DELETE',f"employees/{e}/leave/{full['leaveId']}",None,405,'legacy DELETE removed')
    expect('POST',f"employees/{EMP}/leave/{full['leaveId']}/approve",{},404,'lifecycle ownership check')
    for date,kind in [('2030-01-14','PublicHoliday'),('2030-01-15','SchoolHoliday'),('2030-01-16','RestDay')]:
        api('POST',f'work-calendars/{c}/overrides',{'date':date,'overrideType':kind},201)
        expect('POST',f'employees/{e}/leave',request(date),400,kind+' zero charge')
    api('POST',f'work-calendars/{c}/overrides',{'date':'2030-01-21','overrideType':'ExceptionalWorkingDay','intervals':[{'startTime':'10:00','endTime':'12:15'}]},201)
    x=create(e,request('2030-01-21'),135,'exceptional replacement');check(x['calculation']['dates'][0]['scheduleSource']=='ExceptionalWorkingDay','override evidence recorded');cancel(e,x)
    x=create(e,request('2030-01-07',end='2030-01-09'),1260,'multi-day full');cancel(e,x)
    x=create(e,request('2030-01-07',end='2030-01-09',a='14:00',b='11:00'),720,'multi-day timed boundary intersection');cancel(e,x)
    x=create(e,request('2030-06-28',end='2030-07-01',a='14:00',b='11:00'),180,'calendar and policy change across request')
    check(len({d['workCalendarId'] for d in x['calculation']['dates']})==2 and len({d['policy']['id'] for d in x['calculation']['dates']})==2 and x['calculation']['effectiveNoticeHours']==72,'both calendars/policies and MAX notice frozen');cancel(e,x)
    # Deliberate corruption fixtures are isolated to this synthetic employee/configuration and restored immediately.
    ambiguous=str(uuid.uuid4());sql(f"INSERT EmployeeWorkCalendarAssignments VALUES ('{ambiguous}','{e}','{c2}','2030-01-07','2030-01-07',SYSUTCDATETIME(),SYSUTCDATETIME())")
    expect('POST',f'employees/{e}/leave',request(),409,'ambiguous assignment rejected');sql('DELETE EmployeeWorkCalendarAssignments WHERE Id='+ident(ambiguous))
    job=rows('SELECT EmploymentRecordId FROM EmploymentRecords WHERE EmployeeId='+ident(e))[0]['EmploymentRecordId']
    sql(f"UPDATE EmploymentRecords SET StartDate='2030-01-08' WHERE EmploymentRecordId='{job}'")
    expect('POST',f'employees/{e}/leave',request(),409,'before employment rejected');sql(f"UPDATE EmploymentRecords SET StartDate='2030-01-01' WHERE EmploymentRecordId='{job}'")
    sql(f"UPDATE EmploymentRecords SET IsCurrent=0,EndDate='2030-01-06' WHERE EmploymentRecordId='{job}'")
    expect('POST',f'employees/{e}/leave',request(),409,'after employment rejected');sql(f"UPDATE EmploymentRecords SET EndDate=NULL,IsCurrent=1 WHERE EmploymentRecordId='{job}'")
    # Separate D1 history fixture exercises an actual gap followed by adjacent coverage via supported workflow.
    gap=employee('HISTORY',hire='2026-09-01',start=None);assign(gap,c);entitlement(gap)
    api('POST',f'employees/{gap}/end-employment',{'endDate':'2026-09-07','employmentStatusId':'40000000-0000-0000-0000-000000000005'})
    rehire={'hireDate':'2026-09-09','departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001'}
    api('POST',f'employees/{gap}/rehire',rehire,201)
    # D1 commands prohibit future-dated mutations. Move only these disposable history fixtures to the synthetic calculation year.
    sql("UPDATE EmploymentRecords SET HireDate=DATEADD(day,DATEDIFF(day,'2026-09-01','2030-01-01'),HireDate),EndDate=DATEADD(day,DATEDIFF(day,'2026-09-01','2030-01-01'),EndDate) WHERE EmployeeId="+ident(gap))
    expect('POST',f'employees/{gap}/leave',request(end='2030-01-09'),409,'actual D1 employment gap rejected')
    gapjob=rows('SELECT EmploymentRecordId FROM EmploymentRecords WHERE EmployeeId='+ident(gap)+' AND IsCurrent=1')[0]['EmploymentRecordId']
    sql(f"UPDATE EmploymentRecords SET HireDate='2030-01-08',StartDate=NULL WHERE EmploymentRecordId='{gapjob}'")
    continuous=create(gap,request(end='2030-01-09'),1260,'adjacent D1 employment allowed');check(len({d['employmentRecordId'] for d in continuous['calculation']['dates']})==2,'D1 history preserved in leave evidence');cancel(gap,continuous)
    sql(f"UPDATE EmploymentRecords SET HireDate='2030-01-07' WHERE EmploymentRecordId='{gapjob}'")
    expect('POST',f'employees/{gap}/leave',request(),409,'ambiguous employment rejected')
    sql(f"UPDATE EmploymentRecords SET HireDate='2030-01-08' WHERE EmploymentRecordId='{gapjob}'")
    # Published policies only and no fabricated entitlement for nontracked leave.
    draft,draftbody=policy(personal,'PERSONAL-DRAFT',False,publish=False)
    expect('POST',f'employees/{e}/leave',request(t=personal),409,'Draft ignored')
    api('POST',f"leave-policies/{draft['id']}/publish")
    untracked=create(e,request(t=personal),420,'nontracked no entitlement')
    command(e,untracked,'approve');unpaid_evidence=frozen(untracked)
    sql('UPDATE LeaveTypes SET IsPaid=1 WHERE Id='+ident(personal))
    old_unpaid=api('GET',f"employees/{e}/leave/{untracked['leaveId']}")
    unpaid_history=api('GET',f'leave-requests?employeeId={e}&leaveTypeId={personal}&status=Approved')['items']
    check(old_unpaid['isPaid'] is False and old_unpaid['status']=='Approved' and old_unpaid['chargeableMinutes']==420 and old_unpaid['calculation']['isPaid'] is False and unpaid_history[0]['isPaid'] is False,'Approved Unpaid chargeable minutes remain historical HR fact after live classification change')
    prospective_paid=create(e,request('2030-01-08',t=personal),420,'new paid classification same scheduled minutes')
    check(prospective_paid['isPaid'] is True and frozen(untracked)==unpaid_evidence,'classification change affects new request only')
    cancel(e,prospective_paid);cancel(e,untracked,True);sql('UPDATE LeaveTypes SET IsPaid=0 WHERE Id='+ident(personal))
    check(rows('SELECT COUNT(*) AS Count FROM Attendance')[0]['Count']==0 and rows('SELECT COUNT(*) AS Count FROM EmployeePayrolls')[0]['Count']==0 and rows('SELECT COUNT(*) AS Count FROM EmployeePayrollLines')[0]['Count']==0,'Paid/Unpaid facts create no Attendance or payroll money')
    check(balance(e,personal)['entitledMinutes'] is None and balance(e,personal)['availableMinutes'] is None,'nontracked view no invented unlimited entitlement')
    expect('POST',f'employees/{e}/leave',request(t=other),409,'missing Published policy')
    ps,bs=policy(sick,'SICK1',False,end='2030-06-30',foreseeableNoticeHours=1000000,supportingDocumentPolicy='Conditional',documentTypeId=doc,certificateAfterConsecutiveDays=1,certificateOnMondayWorkingDate=True,certificateOnFridayWorkingDate=True,sandwichParticipation=True)
    ps2,bs2=policy(sick,'SICK2',True,start='2030-07-01',end=None,allowsSuddenRequest=False)
    expect('POST',f'employees/{e}/leave',request(t=sick),400,'insufficient foreseeable notice')
    expect('POST',f'employees/{e}/leave',request(t=sick,noticeCategory='SuddenIllness',reason=' '),400,'Sudden reason required')
    sudden=create(e,request(t=sick,noticeCategory='SuddenIllness',reason='Synthetic sudden illness'),420,'Sudden bypass and certificate Monday')
    check(sudden['supportingDocumentRequired'] and sudden['certificateRequirementReasons']==['MondayWorkingDate'],'certificate metadata without document storage')
    command(e,sudden,'approve');check(True,'approval does not block for missing medical file');cancel(e,sudden,True)
    cert=create(e,request('2030-01-11',end='2030-01-14',t=sick,noticeCategory='SuddenIllness',reason='Synthetic weekend sequence'),420,'holiday Monday zero no trigger')
    check(cert['certificateRequirementReasons']==['FridayWorkingDate'],'working Friday trigger and nonworking Monday excluded');cancel(e,cert)
    cert=create(e,request('2030-01-25',end='2030-01-28',t=sick,noticeCategory='SuddenIllness',reason='Synthetic weekend sequence'),840,'certificate across weekend')
    check(cert['calculation']['longestConsecutiveQualifyingDays']==2 and set(cert['certificateRequirementReasons'])=={'ConsecutiveDaysThreshold','MondayWorkingDate','FridayWorkingDate'},'current-request exclusive certificate threshold; weekend not charged');cancel(e,cert)
    expect('POST',f'employees/{e}/leave',request('2030-06-28',end='2030-07-01',t=sick),409,'mixed BalanceTracked revisions require separate requests')
    expect('POST',f'employees/{e}/leave',request('2030-07-01',t=sick,noticeCategory='SuddenIllness',reason='Synthetic reason'),400,'Sudden forbidden by published revision')
    entitlement(e,year=2031,minutes=1000)
    cross=create(e,request('2030-12-31',end='2031-01-01'),240,'cross year exact allocation')
    check(cross['allocations']==[{'leaveYear':2030,'chargeableMinutes':120},{'leaveYear':2031,'chargeableMinutes':120}], 'cross-year independent relational years')
    check(balance(e,year=2031)['pendingMinutes']==120,'2031 reservation independent');cancel(e,cross)
    # Frozen evidence survives a legitimate calendar change and an entitlement adjustment.
    frozenLeave=create(e,request('2030-02-04'),420,'frozen calendar before change');stored=frozen(frozenLeave)
    api('POST',f'work-calendars/{c}/overrides',{'date':'2030-02-04','overrideType':'PublicHoliday'},201)
    api('PUT',f'work-calendars/{c}',{'code':PREFIX+'SPLIT','name':'Changed synthetic calendar'})
    api('POST',f"employees/{e}/leave-entitlements/{ent['id']}/adjustments",{'adjustmentMinutes':1,'reason':'Synthetic later adjustment'},201)
    approved=command(e,frozenLeave,'approve');check(approved['chargeableMinutes']==420 and frozen(frozenLeave)==stored,'approval no current configuration recalculation');cancel(e,frozenLeave,True)
    # Corrupted allocation and snapshot fail approval without partial transition.
    broken=create(e,request('2030-02-05'),420,'integrity fixture');brokenBefore=frozen(broken)
    sql('UPDATE EmployeeLeaveAllocations SET ChargeableMinutes=ChargeableMinutes+1 WHERE EmployeeLeaveId='+ident(broken['leaveId']))
    command(e,broken,'approve',status=409);check(True,'approval rejects corrupted allocation')
    sql('UPDATE EmployeeLeaveAllocations SET ChargeableMinutes=ChargeableMinutes-1 WHERE EmployeeLeaveId='+ident(broken['leaveId']))
    sql("UPDATE EmployeeLeave SET CalculationSnapshotJson=JSON_MODIFY(CalculationSnapshotJson,'$.chargeableMinutes',421) WHERE LeaveId="+ident(broken['leaveId']))
    command(e,broken,'approve',status=409);check(True,'approval rejects corrupted snapshot')
    original_json=brokenBefore[0]['CalculationSnapshotJson'].replace("'","''")
    sql("UPDATE EmployeeLeave SET CalculationSnapshotJson=N'"+original_json+"' WHERE LeaveId="+ident(broken['leaveId']))
    check(frozen(broken)==brokenBefore and api('GET',f"employees/{e}/leave/{broken['leaveId']}")['status']=='Pending','failed approvals preserve complete stored request')
    cancel(e,broken)
    pending=create(e,request('2030-03-04'),420,'read-model Pending fixture')
    detail=api('GET',f"employees/{e}/leave/{pending['leaveId']}")
    check(detail['calculation'] is not None and detail['chargeableHours']==7 and detail['allocations'][0]['chargeableMinutes']==420,'typed detail summary no client JSON reconstruction')
    history=api('GET',f'leave-requests?employeeId={e}&leaveTypeId={annual}&status=Pending&fromDate=2030-03-01&toDate=2030-03-31&page=1&pageSize=1')
    check(history['totalCount']==1 and history['items'][0]['leaveId']==pending['leaveId'],'history combined filters and pagination')
    queue=api('GET',f'leave-requests/pending?employeeId={e}')
    check(queue['totalCount']==1 and queue['items'][0]['employeeNumber']==PREFIX+'MAIN' and queue['items'][0]['chargeableMinutes']==420 and queue['items'][0]['noticeCategory']=='Foreseeable','Pending-only review queue employee and notice metadata')
    allhistory=api('GET',f'leave-requests?employeeId={e}&pageSize=100')
    check(allhistory==api('GET',f'leave-requests?employeeId={e}&pageSize=100'),'history deterministic ordering')
    for path in ['leave-requests?page=0','leave-requests?pageSize=101','leave-requests?page=2147483647&pageSize=100','leave-requests?status=Invalid','leave-requests/pending?status=Approved',f'employees/{e}/leave-balances?leaveYear=0']:
        expect('GET',path,None,400,'invalid read query '+path)
    cancel(e,pending)
    # Reservation overspend, interval overlap, review/cancel and adjustment/create races.
    ce=employee('CONCURRENT');assign(ce,c);ceEnt=entitlement(ce,minutes=420)
    raced=race([('POST',f'employees/{ce}/leave',request('2030-04-01')),('POST',f'employees/{ce}/leave',request('2030-04-02'))])
    check(sorted(x[0] for x in raced)==[201,409],'concurrent distinct dates cannot overspend last entitlement')
    winner=next(x[1] for x in raced if x[0]==201);check(balance(ce)['pendingMinutes']==420 and balance(ce)['availableMinutes']==0,'SQL-backed race retains one reservation')
    expect('POST',f"employees/{ce}/leave-entitlements/{ceEnt['id']}/adjustments",{'adjustmentMinutes':-1,'reason':'Synthetic reduce below reservation'},409,'adjustment cannot reduce below Pending')
    command(ce,winner,'approve')
    expect('POST',f"employees/{ce}/leave-entitlements/{ceEnt['id']}/adjustments",{'adjustmentMinutes':-1,'reason':'Synthetic reduce below Used'},409,'adjustment cannot reduce below Approved')
    cancel(ce,winner,True)
    raced=race([('POST',f'employees/{ce}/leave',request('2030-04-03',a='09:00',b='11:00'))]*2)
    check(sorted(x[0] for x in raced)==[201,409],'concurrent exact interval overlap prevented')
    winner=next(x[1] for x in raced if x[0]==201);cancel(ce,winner)
    for i in range(3):
        l=create(ce,request('2030-04-04'),420,'review/cancel race fixture '+str(i));ev=frozen(l)
        raced=race([('POST',f"employees/{ce}/leave/{l['leaveId']}/approve",{}),('POST',f"employees/{ce}/leave/{l['leaveId']}/cancel",{'expectedStatus':'Pending','cancellationRemarks':'Synthetic stale Pending intent with reason'})])
        check(sorted(code for code,_ in raced)==[200,409],'review/cancel race exactly one success and one 409 '+str(i))
        state=api('GET',f"employees/{ce}/leave/{l['leaveId']}");check(frozen(l)==ev,'race preserves frozen evidence '+str(i))
        if state['status']=='Approved':cancel(ce,l,True)
    raced=race([('POST',f'employees/{ce}/leave',request('2030-04-05')),('POST',f"employees/{ce}/leave-entitlements/{ceEnt['id']}/adjustments",{'adjustmentMinutes':-1,'reason':'Synthetic concurrent reduction'})])
    check(sum(code==201 for code,_ in raced)==1 and sorted(code for code,_ in raced)==[201,409],'entitlement adjustment/create coherent reservation race')
    check(balance(ce)['availableMinutes']>=0,'no negative balance after concurrent adjustment')
    for code,value in raced:
        if code==201 and 'leaveId' in value:cancel(ce,value)
    # Coherent reads during calendar/policy mutation: frozen version cannot mix interval states.
    configrace=race([('POST',f'employees/{e}/leave',request('2030-05-06')),('POST',f'work-calendars/{c}/overrides',{'date':'2030-05-06','overrideType':'ExceptionalWorkingDay','intervals':[{'startTime':'10:00','endTime':'11:00'}]})])
    check(all(code==201 for code,_ in configrace),'calendar writer and calculation serialize successfully')
    result=configrace[0][1];day=result['calculation']['dates'][0]
    check((result['chargeableMinutes']==420 and day['scheduleSource']=='Weekly') or (result['chargeableMinutes']==60 and day['scheduleSource']=='ExceptionalWorkingDay'),'calculation sees whole old or whole new calendar state')
    cancel(e,result)
    # Validate actual SQL checks against an existing frozen request, always rolled back.
    for query,label,num in [
        ("UPDATE EmployeeLeave SET Status='Other' WHERE LeaveId="+ident(full['leaveId']),'SQL invalid lifecycle status',547),
        ("UPDATE EmployeeLeave SET ChargeableMinutes=0 WHERE LeaveId="+ident(full['leaveId']),'SQL zero charge rejected',547),
        ("UPDATE EmployeeLeave SET RequestedAt=NULL WHERE LeaveId="+ident(full['leaveId']),'SQL missing request time',547),
        ("UPDATE EmployeeLeave SET CalculationSnapshotVersion=NULL WHERE LeaveId="+ident(full['leaveId']),'SQL snapshot version required',547),
        ("UPDATE EmployeeLeaveAllocations SET ChargeableMinutes=0 WHERE EmployeeLeaveId="+ident(full['leaveId']),'SQL positive allocation',547),
        ("UPDATE EmployeeLeaveAllocations SET LeaveYear=0 WHERE EmployeeLeaveId="+ident(full['leaveId']),'SQL valid allocation year',547),
        ("INSERT EmployeeLeaveAllocations SELECT NEWID(),EmployeeLeaveId,LeaveYear,ChargeableMinutes FROM EmployeeLeaveAllocations WHERE EmployeeLeaveId="+ident(full['leaveId']),'SQL unique allocation per leave/year',2601),
        ("DELETE EmployeeLeave WHERE LeaveId="+ident(full['leaveId']),'SQL allocation prevents hard deletion',547)]:constraint(query,label,num)
    with urllib.request.urlopen(BASE+'/swagger/v1/swagger.json') as r:swagger=json.load(r)
    path='/api/employees/{employeeId}/leave/{leaveId}'
    check(set(swagger['paths'][path])=={'get'},'Swagger legacy mutation route no PUT/DELETE')
    routes=[('/api/employees/{employeeId}/leave','post')]+[(path+'/'+verb,'post') for verb in ['approve','reject','cancel']]+[('/api/leave-requests','get'),('/api/leave-requests/pending','get'),('/api/employees/{employeeId}/leave-balances','get')]
    check(all(verb in swagger['paths'][p] and 'responses' in swagger['paths'][p][verb] for p,verb in routes),'all seven workflow/read actions documented')
    schema=swagger['components']['schemas']['EmployeeLeaveRequest']['properties']
    check('status' not in schema and 'chargeableMinutes' not in schema and 'requestMode' in schema and 'noticeCategory' in schema,'Swagger strict authoritative request contract')
    cancel_schema=swagger['components']['schemas']['LeaveCancellationRequest']
    check('expectedStatus' in cancel_schema['required'],'Swagger cancellation precondition required')
    status_schema=swagger['components']['schemas']['LeaveCancellationExpectedStatus']
    check(status_schema['type']=='string' and status_schema['enum']==['Pending','Approved'],'Swagger cancellation typed source states only')
    check(all('StorageKey' not in json.dumps(x) and 'storageKey' not in json.dumps(x) for x in [detail,queue,allhistory]),'no medical storage key in operational models')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        statements=[]
        if employee_ids:
            ids=','.join(map(ident,employee_ids))
            statements += [f'DELETE EmployeeLeaveAllocations WHERE EmployeeLeaveId IN (SELECT LeaveId FROM EmployeeLeave WHERE EmployeeId IN ({ids}))',f'DELETE EmployeeLeave WHERE EmployeeId IN ({ids})',f'DELETE EmployeeLeaveEntitlementAdjustments WHERE EmployeeLeaveEntitlementId IN (SELECT Id FROM EmployeeLeaveEntitlements WHERE EmployeeId IN ({ids}))',f'DELETE EmployeeLeaveEntitlements WHERE EmployeeId IN ({ids})',f'DELETE EmployeeWorkCalendarAssignments WHERE EmployeeId IN ({ids})',f'DELETE EmploymentRecords WHERE EmployeeId IN ({ids})',f'DELETE Employees WHERE EmployeeId IN ({ids})']
        if policy_ids:statements.append('DELETE LeavePolicies WHERE Id IN ('+','.join(map(ident,policy_ids))+')')
        if calendar_ids:
            ids=','.join(map(ident,calendar_ids));statements += [f'DELETE WorkCalendarOverrideIntervals WHERE WorkCalendarDateOverrideId IN (SELECT Id FROM WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids}))',f'DELETE WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendarWeeklyIntervals WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendars WHERE Id IN ({ids})']
        statements += ['UPDATE LeaveTypes SET IsPaid='+('1' if original_paid[t] else '0')+' WHERE Id='+ident(t) for t in [annual,personal]]
        if statements:sql('SET XACT_ABORT ON; BEGIN TRAN; '+';'.join(statements)+';COMMIT;')
        after=snapshot();check(after==baseline,'Exact 66-table post-migration baseline restored including every original timestamp')
        pre=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-before-migration.json').read_text(encoding='utf-8'))
        check(all(after[t]==v for t,v in pre.items()) and after['EmployeeLeaveAllocations']==[],'Exact pre-migration data preserved and new allocation table empty')
        check(rows('SELECT IsActive FROM Employees WHERE EmployeeId='+ident(EMP))[0]['IsActive']==False,'TEST-EMP-001 remains inactive without core modification')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':len(results),'results':results,'error':error,'counts':{t:len(v) for t,v in snapshot().items()}},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} D8C live assertions; exact baseline restored.',flush=True)
