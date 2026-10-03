"""D8B synthetic live fixtures, SQL constraints and exact baseline cleanup. localhost/SIAMIS only."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
from concurrent.futures import ThreadPoolExecutor

PREFIX='D8B-VERIFY-'
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-live-results.json'
baseline=snapshot()
calendar_ids=[]; policy_ids=[]; entitlement_ids=[]; assignment_ids=[]; employee_ids=[]; leave_ids=[]
error=None
def verify_status(method,path,body,status,label):
    value=api(method,path,body,status);check(True,label);return value
def resolve(date,employee=EMP,status=200): return api('GET',f'employees/{employee}/work-calendar?date={date}',status=status)
def constraint(query,label,number=547):
    result=sql("BEGIN TRAN; BEGIN TRY "+query+"; ROLLBACK; THROW 51000,'Invalid accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER()<>"+str(number)+" THROW; SELECT 'Rejected'; END CATCH;")
    check('Rejected' in result,label)
try:
    check(len(baseline)==65 and len(baseline['Employees'])==1 and not baseline['EmployeeLeave'],'65 tables and original empty leave baseline')
    check(rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20261003091135_AddLeaveCalendarPolicyEntitlementFoundation'")!=[],'D8B migration recorded')
    tables=['WorkCalendars','WorkCalendarWeeklyIntervals','WorkCalendarDateOverrides','WorkCalendarOverrideIntervals','EmployeeWorkCalendarAssignments','LeavePolicies','EmployeeLeaveEntitlements','EmployeeLeaveEntitlementAdjustments']
    fks=rows("SELECT name,delete_referential_action_desc FROM sys.foreign_keys WHERE OBJECT_NAME(parent_object_id) IN ("+','.join("'"+x+"'" for x in tables)+")")
    check(len(fks)==10 and all(x['delete_referential_action_desc']=='NO_ACTION' for x in fks),'10 new FKs all NoAction')
    constraints=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE OBJECT_NAME(parent_object_id) IN ("+','.join("'"+x+"'" for x in tables+['EmployeeLeave'])+")")
    check(len(constraints)==16 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in constraints),'16 foundation/preparation checks enabled and trusted')
    indexes=rows("SELECT name,is_unique,filter_definition FROM sys.indexes WHERE OBJECT_NAME(object_id)='WorkCalendars' AND name='IX_WorkCalendars_IsDefault'")
    check(indexes[0]['is_unique'] and 'IsActive' in indexes[0]['filter_definition'],'live default filtered uniqueness')
    check(api('GET',f'employees/{EMP}/leave')==[],'legacy leave list remains empty')
    check(api('GET',f'employees/{EMP}/attendance')==[],'Attendance remains empty')
    check(api('GET',f'employees/{EMP}/leave-entitlements')==[],'missing entitlement is empty, not unlimited/zero')
    verify_status('GET',f'employees/{EMP}/work-calendar?date=2026-10-12',None,409,'missing calendar explicit error')
    a=verify_status('POST','work-calendars',{'code':PREFIX+'A','name':PREFIX+'A','isDefault':True},201,'create default calendar');calendar_ids.append(a['id'])
    check(a['createdAt'].endswith('Z'),'calendar server timestamp UTC')
    verify_status('POST','work-calendars',{'code':PREFIX+'B','name':PREFIX+'B','isDefault':True},409,'second default rejected')
    b=api('POST','work-calendars',{'code':PREFIX+'B','name':PREFIX+'B'},201);calendar_ids.append(b['id']);check(True,'multiple named calendars')
    verify_status('POST','work-calendars',{'code':PREFIX+'A','name':'duplicate'},409,'duplicate calendar code rejected')
    verify_status('POST','work-calendars',{'code':'invalid','name':'invalid','isDefault':True,'isActive':False},400,'inactive default rejected')
    verify_status('POST','work-calendars',{'code':'invalid','name':'invalid','createdAt':'2020-01-01'},400,'caller calendar timestamp rejected')
    verify_status('GET',f'employees/{EMP}/work-calendar?date=2026-10-12',None,409,'default never supplies resolution fallback')
    weekly=f"work-calendars/{a['id']}/weekly-intervals"
    for start,end in [('08:00','12:00'),('13:00','16:00')]:verify_status('POST',weekly,{'dayOfWeek':1,'startTime':start,'endTime':end},201,'split Monday interval '+start)
    for body,label in [({'dayOfWeek':1,'startTime':'11:00','endTime':'13:30'},'weekly overlap'),({'dayOfWeek':1,'startTime':'12:00','endTime':'12:00'},'zero interval'),({'dayOfWeek':1,'startTime':'17:00','endTime':'08:00'},'overnight interval'),({'dayOfWeek':9,'startTime':'08:00','endTime':'09:00'},'invalid weekday'),({'dayOfWeek':1,'startTime':'08:00:01','endTime':'09:00'},'subminute interval')]:
        verify_status('POST',weekly,body,409 if label=='weekly overlap' else 400,label+' rejected')
    check(len(api('GET',weekly))==2,'two intervals returned')
    path=f'employees/{EMP}/work-calendar-assignments'
    ass=verify_status('POST',path,{'workCalendarId':a['id'],'effectiveFrom':'2026-10-01','effectiveTo':'2026-10-31'},201,'explicit assignment created');assignment_ids.append(ass['id'])
    check(resolve('2026-10-12')['scheduledMinutes']==420,'7-hour split day, lunch excluded, no 8-hour assumption')
    verify_status('POST',path,{'workCalendarId':b['id'],'effectiveFrom':'2026-10-31','effectiveTo':'2026-11-01'},409,'inclusive assignment boundary overlap rejected')
    future=verify_status('POST',path,{'workCalendarId':b['id'],'effectiveFrom':'2027-01-01'},201,'future assignment accepted');assignment_ids.append(future['id'])
    check(resolve('2027-01-01')['workCalendarId']==b['id'],'future assignment resolves')
    verify_status('GET',f'employees/{EMP}/work-calendar?date=2026-12-01',None,409,'assignment gap has no fallback')
    for date in ['2026-10-01','2026-10-31']:check(resolve(date)['assignmentId']==ass['id'],'inclusive assignment boundary '+date)
    verify_status('POST',path,{'workCalendarId':str(uuid.uuid4()),'effectiveFrom':'2028-01-01'},400,'unknown calendar rejected')
    verify_status('POST',path,{'workCalendarId':b['id'],'effectiveFrom':'2028-02-01','effectiveTo':'2028-01-01'},400,'inverted assignment dates rejected')
    api('PUT',f"work-calendars/{a['id']}",{'code':a['code'],'name':a['name'],'isDefault':False})
    api('PUT',f"work-calendars/{b['id']}",{'code':b['code'],'name':b['name'],'isDefault':True})
    check(resolve('2026-10-12')['workCalendarId']==a['id'] and len(api('GET',path))==2,'changing default neither reassigns nor reinterprets')
    override=f"work-calendars/{a['id']}/overrides"
    for date,kind in [('2026-10-19','PublicHoliday'),('2026-10-20','SchoolHoliday'),('2026-10-21','RestDay')]:
        verify_status('POST',override,{'date':date,'overrideType':kind},201,kind+' override created');check(resolve(date)['scheduledMinutes']==0,kind+' has empty schedule')
    verify_status('POST',override,{'date':'2026-10-19','overrideType':'RestDay'},409,'duplicate dated override rejected')
    verify_status('POST',override,{'date':'2026-10-26','overrideType':'ExceptionalWorkingDay','intervals':[{'startTime':'10:00','endTime':'12:15'}]},201,'exceptional override created')
    check(resolve('2026-10-26')['scheduledMinutes']==135 and len(resolve('2026-10-26')['intervals'])==1,'exceptional hours replace 420-minute weekly schedule')
    for body,label in [({'date':'2026-10-27','overrideType':'ExceptionalWorkingDay'},'missing replacement'),({'date':'2026-10-27','overrideType':'RestDay','intervals':[{'startTime':'08:00','endTime':'09:00'}]},'nonworking hours'),({'date':'2026-10-27','overrideType':'Unknown'},'unknown category'),({'date':'2026-10-27','overrideType':'ExceptionalWorkingDay','intervals':[{'startTime':'08:00','endTime':'10:00'},{'startTime':'09:00','endTime':'11:00'}]},'replacement overlap')]:verify_status('POST',override,body,400,label+' rejected')
    check(len(api('GET',override))==4,'override list contains exactly four dates')
    verify_status('POST',override,{'date':'2026-10-27','overrideType':'ExceptionalWorkingDay','intervals':[None]},400,'null replacement interval rejected')
    # Deliberate integrity fixture demonstrates rejection rather than arbitrary selection. Remove immediately.
    ambiguous=str(uuid.uuid4());assignment_ids.append(ambiguous)
    sql(f"INSERT EmployeeWorkCalendarAssignments VALUES ('{ambiguous}','{EMP}','{b['id']}','2026-10-12','2026-10-12',SYSUTCDATETIME(),SYSUTCDATETIME())")
    verify_status('GET',f'employees/{EMP}/work-calendar?date=2026-10-12',None,409,'ambiguous assignment detected')
    sql('DELETE EmployeeWorkCalendarAssignments WHERE Id='+ident(ambiguous));assignment_ids.remove(ambiguous)
    sick=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-002'")[0]['Id'].lower();annual=rows("SELECT Id FROM LeaveTypes WHERE Code='LEV-001'")[0]['Id'].lower();doc=rows("SELECT Id FROM DocumentTypes WHERE Code='DOC-011'")[0]['Id'].lower()
    policy={'leaveTypeId':sick,'version':PREFIX+'1','effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','balanceTracked':True,'foreseeableNoticeHours':24,'allowsSuddenRequest':True,'supportingDocumentPolicy':'Conditional','documentTypeId':doc,'certificateAfterConsecutiveDays':2,'certificateOnMondayWorkingDate':True,'certificateOnFridayWorkingDate':True,'sandwichParticipation':True}
    p=verify_status('POST','leave-policies',policy,201,'draft typed policy created');policy_ids.append(p['id'])
    check(p['status']=='Draft' and p['publishedAt'] is None,'publication server controlled')
    for key in ['balanceTracked','foreseeableNoticeHours','allowsSuddenRequest','supportingDocumentPolicy','documentTypeId','certificateAfterConsecutiveDays','certificateOnMondayWorkingDate','certificateOnFridayWorkingDate','sandwichParticipation']:check(p[key]==policy[key],'typed policy roundtrip '+key)
    verify_status('POST','leave-policies',policy,409,'duplicate policy version rejected')
    for change,label in [({'documentTypeId':str(uuid.uuid4())},'unknown supporting document'),({'foreseeableNoticeHours':-1},'negative notice'),({'certificateAfterConsecutiveDays':-1},'negative certificate'),({'status':'Published'},'forged policy status'),({'supportingDocumentPolicy':'None'},'inconsistent document policy')]:verify_status('POST','leave-policies',dict(policy,version=PREFIX+label,**change),400,label+' rejected')
    api('PUT',f"leave-policies/{p['id']}",policy);check(True,'draft update supported')
    pub=verify_status('POST',f"leave-policies/{p['id']}/publish",None,200,'policy published');check(pub['publishedAt'].endswith('Z'),'publication timestamp UTC')
    verify_status('PUT',f"leave-policies/{p['id']}",dict(policy,foreseeableNoticeHours=48),409,'published policy immutable')
    verify_status('POST',f"leave-policies/{p['id']}/publish",None,409,'published cannot republish')
    overlapping=api('POST','leave-policies',dict(policy,version=PREFIX+'2'),201);policy_ids.append(overlapping['id'])
    verify_status('POST',f"leave-policies/{overlapping['id']}/publish",None,409,'overlapping publication rejected')
    nextp=api('POST','leave-policies',dict(policy,version=PREFIX+'3',effectiveFrom='2027-01-01',effectiveTo=None),201);policy_ids.append(nextp['id']);api('POST',f"leave-policies/{nextp['id']}/publish")
    for date in ['2026-01-01','2026-12-31']:check(api('GET',f'leave-policies/resolve?leaveTypeId={sick}&date={date}')['id']==p['id'],'inclusive policy boundary '+date)
    check(api('GET',f'leave-policies/resolve?leaveTypeId={sick}&date=2027-01-01')['id']==nextp['id'],'adjacent policy successor resolves')
    verify_status('GET',f'leave-policies/resolve?leaveTypeId={sick}&date=2025-12-31',None,409,'missing policy fails clearly')
    check(len(api('GET',f'leave-policies?leaveTypeId={sick}'))==3,'policy filter includes revisions')
    entpath=f'employees/{EMP}/leave-entitlements'
    e=verify_status('POST',entpath,{'leaveTypeId':sick,'leaveYear':2026,'entitledMinutes':135},201,'minute entitlement created');entitlement_ids.append(e['id'])
    check(e['adjustedEntitledMinutes']==135,'initial entitlement exact minute precision')
    verify_status('POST',entpath,{'leaveTypeId':sick,'leaveYear':2026,'entitledMinutes':0},409,'unique employee/type/year')
    zero=api('POST',entpath,{'leaveTypeId':annual,'leaveYear':2026,'entitledMinutes':0},201);entitlement_ids.append(zero['id']);check(zero['adjustedEntitledMinutes']==0,'configured zero entitlement distinct from missing')
    for change,label in [({'entitledMinutes':-1},'negative entitlement'),({'entitledMinutes':0.5},'fractional minutes'),({'leaveYear':0},'invalid year'),({'usedMinutes':1},'caller used total'),({'availableMinutes':100},'caller available total')]:verify_status('POST',entpath,dict(leaveTypeId=annual,leaveYear=2027,entitledMinutes=0,**{} )|change,400,label+' rejected')
    adj=f"{entpath}/{e['id']}/adjustments"
    for minutes in [120,-15]:verify_status('POST',adj,{'adjustmentMinutes':minutes,'reason':PREFIX+' signed adjustment'},201,'signed adjustment '+str(minutes))
    check(next(x for x in api('GET',entpath) if x['id']==e['id'])['adjustedEntitledMinutes']==240,'derived adjusted entitlement equals base plus history')
    verify_status('POST',adj,{'adjustmentMinutes':1,'reason':' '},400,'adjustment reason required')
    verify_status('POST',adj,{'adjustmentMinutes':0,'reason':'zero'},400,'zero adjustment rejected')
    verify_status('POST',adj,{'adjustmentMinutes':-241,'reason':'negative'},400,'negative adjusted entitlement rejected')
    verify_status('POST',adj,{'adjustmentMinutes':1,'reason':'forged','createdAt':'2020-01-01'},400,'adjustment timestamp cannot be forged')
    with ThreadPoolExecutor(max_workers=4) as pool: concurrent=list(pool.map(lambda _:api('POST',adj,{'adjustmentMinutes':1,'reason':PREFIX+' concurrent'},201),range(4)))
    after=next(x for x in api('GET',entpath) if x['id']==e['id'])
    check(after['adjustedEntitledMinutes']==244 and after['adjustmentMinutes']==109,'four concurrent adjustments lose no updates')
    history=api('GET',adj);check(len(history)==6 and all(x['createdAt'].endswith('Z') for x in history),'append-only history and server UTC')
    verify_status('PUT',adj,{'reason':'rewrite'},405,'no adjustment edit endpoint')
    verify_status('DELETE',adj,None,405,'no adjustment delete endpoint')
    check(rows('SELECT SUM(CAST(AdjustmentMinutes AS bigint)) AS Total FROM EmployeeLeaveEntitlementAdjustments WHERE EmployeeLeaveEntitlementId='+ident(e['id']))[0]['Total']==109,'SQL adjustment persistence')
    constraint(f"UPDATE EmployeeLeaveEntitlements SET EntitledMinutes=-1 WHERE Id='{e['id']}'",'SQL negative entitlement rejected')
    constraint(f"UPDATE WorkCalendarWeeklyIntervals SET EndTime=StartTime WHERE WorkCalendarId='{a['id']}'",'SQL invalid interval rejected')
    constraint(f"UPDATE LeavePolicies SET SupportingDocumentPolicy='Invalid' WHERE Id='{p['id']}'",'SQL invalid document policy rejected')
    constraint(f"UPDATE EmployeeLeaveEntitlementAdjustments SET Reason='' WHERE EmployeeLeaveEntitlementId='{e['id']}'",'SQL blank adjustment reason rejected')
    constraint(f"UPDATE WorkCalendars SET IsDefault=1 WHERE Id='{a['id']}'",'SQL duplicate active default rejected',2601)
    # D1/legacy leave regression uses a separate synthetic employee, never changes TEST-EMP-001.
    emp=api('POST','employees',{'employeeNumber':PREFIX+'EMP','firstName':'Synthetic','lastName':'D8B','departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001','hireDate':'2026-09-01'},201)
    eid=emp['employeeId'];employee_ids.append(eid);check(True,'synthetic D1 employee created')
    verify_status('POST',f'employees/{eid}/employment-changes',{'effectiveDate':'2026-09-15'},201,'D1 effective context change')
    verify_status('POST',f'employees/{eid}/end-employment',{'endDate':'2026-09-30','employmentStatusId':'40000000-0000-0000-0000-000000000005'},200,'D1 employment end')
    verify_status('POST',f'employees/{eid}/rehire',{'hireDate':'2026-10-01','departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001'},201,'D1 rehire')
    check(len(api('GET',f'employees/{eid}/employment-history'))==3,'D1 preserves historical intervals')
    l=api('POST',f'employees/{eid}/leave',{'leaveTypeId':sick,'startDate':'2026-10-12','endDate':'2026-10-13'},201);leave_ids.append(l['leaveId'])
    check(l['days']==2 and l['status']=='Pending','legacy leave behavior retained without new-engine claim')
    verify_status('POST',f'employees/{eid}/leave',{'leaveTypeId':sick,'startDate':'2026-10-13','endDate':'2026-10-14'},409,'legacy overlap regression')
    verify_status('POST',f'employees/{eid}/leave',{'leaveTypeId':sick,'startDate':'2026-10-14','endDate':'2026-10-13'},400,'legacy date validation regression')
    check(len(api('GET',f'employees/{eid}/leave?fromDate=2026-10-13&toDate=2026-10-13'))==1,'legacy range read regression')
    sql(f"UPDATE EmployeeLeave SET RequestedStartTime='13:30',RequestedEndTime='15:45',EndDate=StartDate,ChargeableMinutes=135,CalculationSnapshotVersion=1,CalculationSnapshotJson='{{\"version\":1}}' WHERE LeaveId='{l['leaveId']}'")
    check(rows('SELECT ChargeableMinutes FROM EmployeeLeave WHERE LeaveId='+ident(l['leaveId']))[0]['ChargeableMinutes']==135,'hourly schema persists precise interval and versioned snapshot')
    constraint(f"UPDATE EmployeeLeave SET RequestedStartTime=NULL WHERE LeaveId='{l['leaveId']}'",'SQL incomplete boundary pair rejected')
    constraint(f"UPDATE EmployeeLeave SET CalculationSnapshotVersion=NULL WHERE LeaveId='{l['leaveId']}'",'SQL snapshot requires version')
    constraint(f"UPDATE EmployeeLeave SET CalculationSnapshotJson='invalid' WHERE LeaveId='{l['leaveId']}'",'SQL malformed snapshot rejected')
    constraint(f"UPDATE EmployeeLeave SET ChargeableMinutes=-1 WHERE LeaveId='{l['leaveId']}'",'SQL negative charge minutes rejected')
    api('DELETE',f"employees/{eid}/leave/{l['leaveId']}",status=204);leave_ids.clear();check(True,'legacy fixture deleted')
    # Swagger includes all 19 foundation actions, typed contracts and response schemas.
    with urllib.request.urlopen(BASE+'/swagger/v1/swagger.json') as r:swagger=json.load(r)
    found=[(path,verb) for path,verbs in swagger['paths'].items() for verb in verbs if any(k in path for k in ('work-calendar','leave-policies','leave-entitlements'))]
    check(len(found)==19,'all 19 foundation actions visible in Swagger')
    check(all('responses' in swagger['paths'][path][verb] for path,verb in found),'Swagger response types present')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        statements=[]
        if entitlement_ids:
            ids=','.join(map(ident,entitlement_ids));statements += [f'DELETE EmployeeLeaveEntitlementAdjustments WHERE EmployeeLeaveEntitlementId IN ({ids})',f'DELETE EmployeeLeaveEntitlements WHERE Id IN ({ids})']
        if assignment_ids:statements.append('DELETE EmployeeWorkCalendarAssignments WHERE Id IN ('+','.join(map(ident,assignment_ids))+')')
        if policy_ids:statements.append('DELETE LeavePolicies WHERE Id IN ('+','.join(map(ident,policy_ids))+')')
        if calendar_ids:
            ids=','.join(map(ident,calendar_ids));statements += [f'DELETE WorkCalendarOverrideIntervals WHERE WorkCalendarDateOverrideId IN (SELECT Id FROM WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids}))',f'DELETE WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendarWeeklyIntervals WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendars WHERE Id IN ({ids})']
        if employee_ids:
            ids=','.join(map(ident,employee_ids));statements += [f'DELETE EmployeeLeave WHERE EmployeeId IN ({ids})',f'DELETE EmploymentRecords WHERE EmployeeId IN ({ids})',f'DELETE Employees WHERE EmployeeId IN ({ids})']
        if statements:sql('SET XACT_ABORT ON; BEGIN TRAN; '+';'.join(statements)+';COMMIT;')
        check(snapshot()==baseline,'Exact post-migration Development baseline restored, including all original timestamps')
        original=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-before-migration.json').read_text())
        after=snapshot();check(all(after[table]==value for table,value in original.items()),'Exact pre-migration application data preserved')
        check(all(not after[table] for table in tables),'All eight new tables empty after cleanup')
        check(rows('SELECT IsActive FROM Employees WHERE EmployeeId='+ident(EMP))[0]['IsActive']==False,'TEST-EMP-001 remains inactive')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':len(results),'results':results,'error':error},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} D8B live assertions; exact baseline restored.',flush=True)
