"""D8D synthetic Development receipts, cases, lifecycle, SQL constraints and concurrent operations.
All fixture IDs are recorded and removed; exact original application data is compared after cleanup.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d8c_live.py').read_text(encoding='utf-8').split('\ntry:\n')[0])
PREFIX='D8D-VERIFY-'
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8d-live-results.json'
new_tables=['EmployeeLeaveEvidence','EmployeeLeaveEvidenceEvents','EmployeeLeaveApprovalEvidence','EmployeeLeaveSandwichCases','EmployeeLeaveSandwichEvents','EmployeeLeaveSandwichDates','EmployeeLeaveSandwichAllocations']
error=None;races=[]
def cases(e):return api('GET',f'employees/{e}/leave-sandwich-cases')
def receipt(e,l,t=doc,old=None,status=201,**kw):
    return expect('POST',f"employees/{e}/leave/{l['leaveId']}/evidence",{'documentTypeId':t,'externalReference':'SYNTHETIC-'+str(uuid.uuid4()),'supersedesEvidenceId':old}|kw,status,'external receipt '+str(status))
def review(e,l,r,accept=True,status=200):
    return expect('POST',f"employees/{e}/leave/{l['leaveId']}/evidence/{r['id']}/"+('accept' if accept else 'reject'),{'reviewRemarks':'Synthetic independent external review'},status,'external review '+str(status))
def pair(e,date='2030-01-11',end='2030-01-14',a=None,b=None,t=annual):
    l=create(e,request(date,t=t,a=a,b=b),420 if a is None or a=='08:00' else 60,'before boundary')
    r=create(e,request(end,t=t,a=a,b=b),420 if a is None or a=='08:00' else 60,'after boundary');return l,r
def new(suffix,minutes=10000,t=annual):
    e=employee(suffix);c=calendar(suffix,[('08:00','12:00'),('13:00','16:00')]);assign(e,c,end='2033-12-31')
    if minutes is not None:entitlement(e,t,minutes=minutes)
    return e,c
def sandwich_review(e,c,status=200,outcome="ReasonAccepted",**kw):
    return expect('POST',f"employees/{e}/leave-sandwich-cases/{c['id']}/review",{'expectedStatus':'ReviewPending','outcome':outcome,'reason':'Synthetic deliberate HR review'}|kw,status,'sandwich review '+str(status))
def case_frozen(c):return rows('SELECT CalculationSnapshotJson FROM EmployeeLeaveSandwichCases WHERE Id='+ident(c['id']))
def cleanup_d8d():
    statements=[]
    if employee_ids:
        ids=','.join(map(ident,employee_ids));leaves=f'SELECT LeaveId FROM EmployeeLeave WHERE EmployeeId IN ({ids})'; cs=f'SELECT Id FROM EmployeeLeaveSandwichCases WHERE EmployeeId IN ({ids})'
        for table in ['EmployeeLeaveSandwichEvents','EmployeeLeaveSandwichDates','EmployeeLeaveSandwichAllocations']:
            statements.append(f'DELETE {table} WHERE CaseId IN ({cs})')
        statements.append(f'DELETE EmployeeLeaveSandwichCases WHERE EmployeeId IN ({ids})')
        statements.append(f'DELETE EmployeeLeaveApprovalEvidence WHERE EmployeeId IN ({ids})')
        statements.append(f'DELETE EmployeeLeaveEvidenceEvents WHERE EvidenceId IN (SELECT Id FROM EmployeeLeaveEvidence WHERE EmployeeId IN ({ids}))')
        # Successors newest first: NoAction self ownership FK must be respected.
        evidence=rows(f'SELECT Id,RecordedAt FROM EmployeeLeaveEvidence WHERE EmployeeId IN ({ids}) ORDER BY RecordedAt DESC')
        statements.extend('DELETE EmployeeLeaveEvidence WHERE Id='+ident(x['Id']) for x in evidence)
        statements += [f'DELETE EmployeeLeaveAllocations WHERE EmployeeLeaveId IN ({leaves})',f'DELETE EmployeeLeave WHERE EmployeeId IN ({ids})',f'DELETE EmployeeLeaveEntitlementAdjustments WHERE EmployeeLeaveEntitlementId IN (SELECT Id FROM EmployeeLeaveEntitlements WHERE EmployeeId IN ({ids}))',f'DELETE EmployeeLeaveEntitlements WHERE EmployeeId IN ({ids})',f'DELETE EmployeeWorkCalendarAssignments WHERE EmployeeId IN ({ids})',f'DELETE EmploymentRecords WHERE EmployeeId IN ({ids})',f'DELETE Employees WHERE EmployeeId IN ({ids})']
    if policy_ids:statements.append('DELETE LeavePolicies WHERE Id IN ('+','.join(map(ident,policy_ids))+')')
    if calendar_ids:
        ids=','.join(map(ident,calendar_ids));statements += [f'DELETE WorkCalendarOverrideIntervals WHERE WorkCalendarDateOverrideId IN (SELECT Id FROM WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids}))',f'DELETE WorkCalendarDateOverrides WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendarWeeklyIntervals WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendars WHERE Id IN ({ids})']
    statements += ['UPDATE LeaveTypes SET IsPaid='+('1' if original_paid[t] else '0')+' WHERE Id='+ident(t) for t in [annual,personal]]
    if statements:sql('SET XACT_ABORT ON; BEGIN TRAN; '+';'.join(statements)+';COMMIT;')

try:
    check(len(baseline)==73 and all(not baseline[t] for t in new_tables),'73-table D8D empty new schema baseline')
    pre=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8d-before-migration.json').read_text())
    check(all(baseline[t]==v for t,v in pre.items()),'migration exact prior data preservation')
    check(rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20261003121650_AddLeaveEvidenceAndSandwichFoundation'")!=[],'migration applied locally')
    check(rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20261003132846_AddLeaveSandwichReviewLifecycle'")!=[],'review follow-up recorded without rewriting original migration')
    check(rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20261003164508_AddCappedLeaveSandwichConsumption'")!=[],'capped consumption follow-up recorded')
    column=rows("SELECT TYPE_NAME(user_type_id) AS TypeName,is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('EmployeeLeaveSandwichAllocations') AND name='AppliedDebitMinutes'")
    check(len(column)==1 and column[0]['TypeName']=='int' and column[0]['is_nullable'],'single nullable applied-minute column preserves unknown legacy audit')
    states=rows("SELECT definition FROM sys.check_constraints WHERE name IN ('CK_LeaveSandwich_State','CK_LeaveSandwichEvent_State')")
    check(len(states)==2 and all(all(v in x['definition'] for v in ['ReviewPending','ReasonAccepted','ReasonNotAccepted','Reserved','Exempted']) for x in states),'new review states and historical legacy states preserved in SQL constraints')
    constraints=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name LIKE 'CK_LeaveEvidence%' OR name LIKE 'CK_LeaveSandwich%' OR name='CK_LeavePolicy_SandwichMinutes'")
    check(len(constraints)==12 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in constraints),'12 focused checks enabled and trusted')
    fks=rows("SELECT f.name,f.delete_referential_action_desc FROM sys.foreign_keys f JOIN sys.tables t ON f.parent_object_id=t.object_id WHERE t.name IN ("+','.join("'"+t+"'" for t in new_tables)+")")
    check(all(f['delete_referential_action_desc']=='NO_ACTION' for f in fks),'all focused FKs NoAction')
    check(not rows("SELECT constraint_object_id FROM sys.foreign_key_columns WHERE COL_NAME(parent_object_id,parent_column_id)='ActorId'"),'no actor FK to Employees/users')
    check(not rows("SELECT * FROM LeavePolicies WHERE SandwichEquivalentDayMinutes IS NOT NULL"),'no default equivalent minutes seeded')
    p,bp=policy(annual,'ANNUAL',True,sandwichParticipation=True,sandwichEquivalentDayMinutes=300)
    p2,_=policy(annual,'ANNUAL-NEXT',True,start='2031-01-01',end='2031-12-31',sandwichParticipation=True,sandwichEquivalentDayMinutes=90)
    policy(personal,'UNTRACKED',False,end='2031-12-31',sandwichParticipation=True,sandwichEquivalentDayMinutes=75)
    policy(other,'NO-PARTICIPATION',False,end='2031-12-31')
    e,c=new('MAIN');l,r=pair(e);cc=cases(e)[0]
    check(len(cases(e))==1 and cc['state']=='ReviewPending' and cc['sandwichDebitMinutes']==600 and cc['appliedSandwichDebitMinutes']==0,'separate requests freeze potential 600 with zero applied debit')
    check(all(d['scheduledMinutes']==0 and d['sandwichDebitMinutes']==300 for d in cc['dates']),'gap zero schedule separate debit')
    check(l['chargeableMinutes']==r['chargeableMinutes']==420 and r['days']==1,'normal actual 420-minute work unchanged')
    check(balance(e)['availableMinutes']==9160 and balance(e)['sandwichPendingMinutes']==0 and balance(e)['sandwichUsedMinutes']==0,'ReviewPending reserves only normal 840')
    for query,label,num in [
        ('UPDATE EmployeeLeaveSandwichDates SET ScheduledMinutes=1 WHERE CaseId='+ident(cc['id']),'SQL forbids fictional working minutes',547),
        ('UPDATE EmployeeLeaveSandwichDates SET SandwichDebitMinutes=0 WHERE CaseId='+ident(cc['id']),'SQL positive per-date debit',547),
        ('UPDATE EmployeeLeaveSandwichAllocations SET LeaveYear=0 WHERE CaseId='+ident(cc['id']),'SQL valid sandwich year',547),
        ('UPDATE EmployeeLeaveSandwichAllocations SET AppliedDebitMinutes=-1 WHERE CaseId='+ident(cc['id']),'SQL rejects negative applied debit',547),
        ('UPDATE EmployeeLeaveSandwichAllocations SET AppliedDebitMinutes=SandwichDebitMinutes+1 WHERE CaseId='+ident(cc['id']),'SQL rejects applied debit above potential',547),
        ("UPDATE EmployeeLeaveSandwichCases SET State='Other' WHERE Id="+ident(cc['id']),'SQL typed case state',547),
        ('UPDATE EmployeeLeaveSandwichCases SET IsActive=0 WHERE Id='+ident(cc['id']),'SQL state/active consistency',547),
        ('INSERT EmployeeLeaveSandwichCases SELECT NEWID(),EmployeeId,LeaveTypeId,BeforeLeaveId,AfterLeaveId,GapStart,GapEnd,Revision+1,IsActive,IsPaid,BalanceTracked,State,DetectedAt,CalculationSnapshotJson FROM EmployeeLeaveSandwichCases WHERE Id='+ident(cc['id']),'SQL unique active span',2601)]:constraint(query,label,num)
    detail=api('GET',f"employees/{e}/leave/{r['leaveId']}");listing=api('GET',f'employees/{e}/leave');history=api('GET',f'leave-requests?employeeId={e}')['items']
    check(detail['sandwichDebitMinutes']==0 and len(detail['sandwichCases'])==1 and detail['evidence']['requiredDocumentTypeIds']==[],'detail separate sandwich/evidence read metadata')
    check(all(x['sandwichCases'][0]['id']==cc['id'] for x in listing+history),'list/history expose same case identity without duplicate budget owner')
    sandwich_review(EMP,cc,404)
    old=case_frozen(cc);old_l=frozen(l)
    command(e,l,'approve');check(cases(e)[0]['state']=='ReviewPending','first boundary approval while ReviewPending succeeds')
    before=complete(r);old_case=rows('SELECT * FROM EmployeeLeaveSandwichCases WHERE Id='+ident(cc['id']));old_events=rows('SELECT * FROM EmployeeLeaveSandwichEvents WHERE CaseId='+ident(cc['id']));old_balance=balance(e)
    command(e,r,'approve',status=409)
    check(complete(r)==before and old_case==rows('SELECT * FROM EmployeeLeaveSandwichCases WHERE Id='+ident(cc['id'])) and old_events==rows('SELECT * FROM EmployeeLeaveSandwichEvents WHERE CaseId='+ident(cc['id'])) and balance(e)==old_balance,'final boundary approval gate makes no mutation')
    decided=sandwich_review(e,cc,outcome='ReasonNotAccepted')
    check(decided['state']=='ReasonNotAccepted' and decided['reviewOutcome']=='ReasonNotAccepted' and decided['reviewedAt'].endswith('Z') and decided['appliedSandwichDebitMinutes']==600,'explicit rejection of reason establishes reservation and UTC review metadata')
    check(balance(e)['sandwichPendingMinutes']==600 and balance(e)['availableMinutes']==8560,'reason not accepted reserves 600')
    expect('POST',f"employees/{e}/leave-entitlements/{rows('SELECT Id FROM EmployeeLeaveEntitlements WHERE EmployeeId='+ident(e))[0]['Id']}/adjustments",{'adjustmentMinutes':-9000,'reason':'Synthetic below commitment'},409,'adjustment includes reviewed sandwich commitments')
    command(e,r,'approve');check(cases(e)[0]['state']=='Charged' and balance(e)['sandwichUsedMinutes']==600,'both Approved charge exactly once')
    sandwich_review(e,cc,409);command(e,r,'approve',status=409)
    api('POST',f'work-calendars/{c}/overrides',{'date':'2030-01-12','overrideType':'ExceptionalWorkingDay','intervals':[{'startTime':'08:00','endTime':'09:00'}]},201)
    check(case_frozen(cc)==old and frozen(l)==old_l,'calendar mutation preserves frozen case and Approved request')
    cancel(e,r,True);check(cases(e)[0]['state']=='Released' and balance(e)['sandwichUsedMinutes']==0,'Approved cancellation reverses once')
    cancel(e,r,True) if False else command(e,r,'cancel',{'expectedStatus':'Approved','cancellationRemarks':'repeat'},409)
    check(balance(e)['usedMinutes']==420 and balance(e)['availableMinutes']==9580,'other Approved boundary intact')

    e,c=new('SAME');same=create(e,request('2030-01-11',end='2030-01-14'),840,'one request spans weekend');cc=cases(e)[0]
    check(cc['beforeLeaveId']==cc['afterLeaveId']==same['leaveId'],'same request case')
    sandwich_review(e,cc,400,reason='');sandwich_review(e,cc,400,expectedStatus='Charged');sandwich_review(e,cc)
    check(cases(e)[0]['state']=='ReasonAccepted' and cases(e)[0]['appliedSandwichDebitMinutes']==0 and balance(e)['availableMinutes']==9160 and balance(e)['sandwichPendingMinutes']==0,'reason accepted has zero applied sandwich debit')
    command(e,same,'approve');check(cases(e)[0]['state']=='ReasonAccepted' and balance(e)['usedMinutes']==840 and balance(e)['sandwichUsedMinutes']==0,'reason accepted final approval charges only actual scheduled minutes')
    cancel(e,same,True);replacement=create(e,request('2030-01-11',end='2030-01-14'),840,'replacement span')
    check(len(cases(e))==2 and cases(e)[1]['revision']==2 and cases(e)[1]['state']=='ReviewPending','new revision does not inherit prior review')
    command(e,replacement,'reject');check(cases(e)[1]['state']=='Released','rejection releases reservation')

    e,c=new('PARTIAL');pair(e,a='15:00',b='16:00');check(cases(e)==[],'hourly partial boundaries cannot sandwich')
    # Two disjoint partial requests on each boundary are not unioned.
    for d in ['2030-02-01','2030-02-04']:
        create(e,request(d,a='08:00',b='12:00'),240,'partial split morning');create(e,request(d,a='13:00',b='16:00'),180,'partial split afternoon')
    check(cases(e)==[],'multiple partial boundaries never union')
    e,c=new('TIMED');pair(e,a='08:00',b='16:00');check(cases(e)[0]['sandwichDebitMinutes']==600,'Timed full split-schedule coverage qualifies')
    e=employee('GAP-CALENDAR');c=calendar('GAP-CALENDAR',[('08:00','12:00'),('13:00','16:00')]);assign(e,c,end='2030-01-11');assign(e,c,start='2030-01-14',end='2031-12-31');entitlement(e)
    create(e,request('2030-01-11'),420,'before calendar gap')
    expect('POST',f'employees/{e}/leave',request('2030-01-14'),409,'uncaptured gap requires explicit calendar coverage')
    check(cases(e)==[] and len(api('GET',f'employees/{e}/leave'))==1,'calendar gap formation rollback')
    e,c=new('FROZEN-GAP-CONFLICT');create(e,request('2030-01-11',end='2030-01-13'),420,'old frozen zero-working gap')
    api('POST',f'work-calendars/{c}/overrides',{'date':'2030-01-13','overrideType':'PublicHoliday'},201)
    expect('POST',f'employees/{e}/leave',request('2030-01-12',end='2030-01-14'),409,'conflicting frozen gap sources require review')
    check(cases(e)==[] and len(api('GET',f'employees/{e}/leave'))==1,'no selecting current gap meaning over old snapshot')
    for suffix,kind,day in [('EXCEPTIONAL','ExceptionalWorkingDay','2030-01-12'),('WORK-SATURDAY',None,None)]:
        e,c=new(suffix)
        if kind:api('POST',f'work-calendars/{c}/overrides',{'date':day,'overrideType':kind,'intervals':[{'startTime':'08:00','endTime':'10:00'}]},201)
        else:api('POST',f'work-calendars/{c}/weekly-intervals',{'dayOfWeek':6,'startTime':'08:00','endTime':'10:00'},201)
        pair(e);check(cases(e)==[],suffix+' interrupts continuous nonworking span')
    e,c=new('HOLIDAY')
    api('POST',f'work-calendars/{c}/overrides',{'date':'2030-01-14','overrideType':'PublicHoliday'},201)
    pair(e,end='2030-01-15');check(len(cases(e)[0]['dates'])==3 and cases(e)[0]['sandwichDebitMinutes']==900,'holiday extends weekend span')
    e,c=new('UNTRACKED',None);l,r=pair(e,t=personal)
    check(cases(e)[0]['isPaid']==False and cases(e)[0]['balanceTracked']==False and cases(e)[0]['sandwichDebitMinutes']==150,'unpaid untracked explicit debit facts')
    uc=cases(e)[0];original_case=case_frozen(uc)[0]['CalculationSnapshotJson'];before=complete(l)
    sql("UPDATE EmployeeLeaveSandwichCases SET CalculationSnapshotJson=JSON_MODIFY(CalculationSnapshotJson,'$.isPaid',NULL) WHERE Id="+ident(uc['id']))
    expect('GET',f'employees/{e}/leave-sandwich-cases',None,409,'missing frozen classification is not false')
    command(e,l,'approve',status=409);check(complete(l)==before,'missing case fact blocks approval without partial mutation')
    sql("UPDATE EmployeeLeaveSandwichCases SET CalculationSnapshotJson='"+original_case.replace("'","''")+"' WHERE Id="+ident(uc['id']))
    command(e,l,'approve');check(next(b for b in api('GET',f'employees/{e}/leave-balances?leaveYear=2030') if b['leaveTypeId']==personal)['availableMinutes'] is None,'untracked no invented entitlement')
    sandwich_review(e,uc,outcome='ReasonNotAccepted');command(e,r,'approve')
    ub=balance(e,t=personal)
    check(cases(e)[0]['state']=='Charged' and ub['sandwichPendingMinutes']==ub['sandwichUsedMinutes']==0 and ub['availableMinutes'] is None,'untracked reason not accepted retains applicable policy facts but no entitlement use')
    e,c=new('MIXED');create(e,request('2030-01-11'),420,'paid before');create(e,request('2030-01-14',t=personal),420,'unpaid different type after');check(cases(e)==[],'mixed LeaveType excluded')
    e,c=new('MIXED-PAID');create(e,request('2030-01-11'),420,'old Paid boundary');sql('UPDATE LeaveTypes SET IsPaid=0 WHERE Id='+ident(annual))
    create(e,request('2030-01-14'),420,'new Unpaid boundary');check(cases(e)==[],'same type mixed frozen IsPaid excluded');sql('UPDATE LeaveTypes SET IsPaid=1 WHERE Id='+ident(annual))
    e,c=new('NONPART',None);pair(e,t=other);check(cases(e)==[],'nonparticipating boundary excludes sandwich')
    for budget,label in [(None,'MISSING'),(0,'ZERO'),(1000,'INSUFFICIENT')]:
        e,c=new(label,budget)
        if budget is None:expect('POST',f'employees/{e}/leave',request('2030-01-11'),409,label+' normal budget rejected');continue
        if budget==0:
            exhausted=create(e,request('2030-01-11'),420,'D11 configured zero is unpaid')
            check(exhausted['paidMinutes']==0 and exhausted['unpaidMinutes']==420 and balance(e)['availableMinutes']==0,'D11 zero paid allocation has no entitlement debt')
            continue
        l=create(e,request('2030-01-11'),420,'older before budget');command(e,l,'approve');old=complete(l)
        r=create(e,request('2030-01-14'),420,'potential pair despite insufficient sandwich budget');cc=cases(e)[0]
        old_case=case_frozen(cc);old_balance=balance(e)
        capped=sandwich_review(e,cc,outcome='ReasonNotAccepted')
        check(capped['potentialDebitMinutes']==600 and capped['appliedDebitMinutes']==160 and capped['unabsorbedDebitMinutes']==440 and case_frozen(cc)==old_case and complete(l)==old,'partial review preserves potential facts and older Approved boundary')
        command(e,r,'approve');check(balance(e)['usedMinutes']==840 and balance(e)['sandwichUsedMinutes']==160 and balance(e)['availableMinutes']==0,'partial debit bottoms entitlement at zero')
    e,c=new('CROSSYEAR');entitlement(e,year=2031,minutes=1000)
    for d in ['2030-12-31','2031-01-01']:api('POST',f'work-calendars/{c}/overrides',{'date':d,'overrideType':'SchoolHoliday'},201)
    l,r=pair(e,date='2030-12-30',end='2031-01-02');cc=cases(e)[0]
    check([(a['leaveYear'],a['sandwichDebitMinutes']) for a in cc['allocations']]==[(2030,300),(2031,90)],'cross-year separate budgets and variable policy minutes')
    sandwich_review(e,cc,outcome='ReasonNotAccepted');check(balance(e)['sandwichPendingMinutes']==300 and balance(e,year=2031)['sandwichPendingMinutes']==90,'cross-year reviewed reservation atomically covers both years')
    command(e,l,'approve');command(e,r,'approve');check(balance(e)['sandwichUsedMinutes']==300 and balance(e,year=2031)['sandwichUsedMinutes']==90,'cross-year final approval charges both frozen allocations')
    e,c=new('CROSSYEAR-MISSING')
    for d in ['2030-12-31','2031-01-01']:api('POST',f'work-calendars/{c}/overrides',{'date':d,'overrideType':'SchoolHoliday'},201)
    expect('POST',f'employees/{e}/leave',request('2030-12-30',end='2031-01-02'),409,'cross-year operation requires both budgets')
    check(cases(e)==[] and api('GET',f'employees/{e}/leave')==[],'cross-year failed operation fully rolled back')
    e,c=new('CROSSYEAR-ZERO',420);entitlement(e,year=2031,minutes=1000)
    for d in ['2030-12-31','2031-01-01']:api('POST',f'work-calendars/{c}/overrides',{'date':d,'overrideType':'SchoolHoliday'},201)
    l=create(e,request('2030-12-30'),420,'before exhausts year budget')
    r=create(e,request('2031-01-02'),420,'cross-year potential pair with exhausted prior year');cc=cases(e)[0];before=balance(e,year=2031)
    capped=sandwich_review(e,cc,outcome='ReasonNotAccepted')
    check([(a['appliedDebitMinutes'],a['unabsorbedDebitMinutes']) for a in capped['allocations']]==[(0,300),(90,0)] and balance(e)['availableMinutes']==0,'cross-year zero availability capped independently without borrowing')
    e,c=new('YEAR-MISSING');entitlement(e,year=2031)
    for d in ['2030-12-31','2031-01-01']:api('POST',f'work-calendars/{c}/overrides',{'date':d,'overrideType':'SchoolHoliday'},201)
    l,r=pair(e,date='2030-12-30',end='2031-01-02');cc=cases(e)[0]
    # Only this recorded synthetic employee's entitlement is removed to exercise an integrity failure.
    sql('DELETE EmployeeLeaveEntitlements WHERE EmployeeId='+ident(e)+' AND LeaveYear=2031')
    before=complete(l),complete(r);sandwich_review(e,cc,409,outcome='ReasonNotAccepted')
    check(cases(e)[0]['state']=='ReviewPending' and (complete(l),complete(r))==before and balance(e)['sandwichPendingMinutes']==0,'missing review-year entitlement makes no partial reservation or leave mutation')
    # Boundary participation true, gap participation false; revisions may differ.
    policy(annual,'2032-BEFORE',True,start='2032-01-01',end='2032-01-02',sandwichParticipation=True,sandwichEquivalentDayMinutes=135)
    policy(annual,'2032-GAP-NONPART',True,start='2032-01-03',end='2032-01-04')
    future,_=policy(annual,'2032-AFTER',True,start='2032-01-05',end='2032-12-31',sandwichParticipation=True,sandwichEquivalentDayMinutes=135)
    e,c=new('GAP-NONPART');entitlement(e,year=2032)
    pair(e,date='2032-01-02',end='2032-01-05');check(cases(e)==[],'both participating boundaries cannot imply gap participation')
    # Simulate an inherited pre-D8D Published policy with nullable units. No API mutates Published configuration.
    sql('UPDATE LeavePolicies SET SandwichEquivalentDayMinutes=NULL WHERE Id='+ident(future['id']))
    e,c=new('LEGACY-POLICY');entitlement(e,year=2032)
    l=create(e,request('2032-01-09'),420,'legacy before boundary')
    expect('POST',f'employees/{e}/leave',request('2032-01-12'),409,'legacy participating policy cannot invent equivalent minutes')
    check(cases(e)==[] and len(api('GET',f'employees/{e}/leave'))==1,'legacy configuration conflict atomic rollback')
    sql('UPDATE LeavePolicies SET SandwichEquivalentDayMinutes=135 WHERE Id='+ident(future['id']))
    for value in [None,0,-1]:
        expect('POST','leave-policies',{'leaveTypeId':other,'version':PREFIX+'INVALID-'+str(value),'effectiveFrom':'2032-01-01','sandwichParticipation':True,'sandwichEquivalentDayMinutes':value},400,'explicit positive policy debit validation '+str(value))

    for paid in [True,False]:
        sql('UPDATE LeaveTypes SET IsPaid='+('1' if paid else '0')+' WHERE Id='+ident(annual))
        for outcome in ['ReasonAccepted','ReasonNotAccepted']:
            e,c=new(('PAID' if paid else 'UNPAID')+('-ACCEPT' if outcome=='ReasonAccepted' else '-REJECT'));l,r=pair(e);cc=cases(e)[0]
            check(cc['isPaid']==paid and cc['reviewOutcome'] is None and balance(e)['sandwichPendingMinutes']==0,'paid classification independent of pending review')
            old_l=complete(l);old_r=complete(r)
            reviewed=sandwich_review(e,cc,outcome=outcome)
            check(complete(l)==old_l and complete(r)==old_r,'review never rewrites normal leave minutes/days/status')
            command(e,l,'approve');command(e,r,'approve')
            check(cases(e)[0]['reviewOutcome']==outcome and balance(e)['usedMinutes']==(840 if paid else 0) and balance(e)['sandwichUsedMinutes']==(600 if outcome=='ReasonNotAccepted' else 0),'D11 normal paid-only usage and unchanged D8D '+outcome+' separate sandwich debit')
            sandwich_review(e,cc,409,outcome=outcome)
            cancel(e,r,True);check(cases(e)[0]['reviewOutcome']==outcome and balance(e)['sandwichPendingMinutes']==balance(e)['sandwichUsedMinutes']==0,'release retains explicit historical review outcome')
    sql('UPDATE LeaveTypes SET IsPaid=1 WHERE Id='+ident(annual))
    e,c=new('REVIEW-CONTRACT');l,r=pair(e);cc=cases(e)[0]
    for payload in [{'expectedStatus':'ReviewPending','reason':'x'}, {'outcome':'ReasonAccepted','reason':'x'}, {'expectedStatus':'ReviewPending','outcome':'Other','reason':'x'}, {'expectedStatus':0,'outcome':'ReasonAccepted','reason':'x'}, {'expectedStatus':'ReviewPending','outcome':0,'reason':'x'}]:
        expect('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/review",payload,400,'strict explicit review contract')
    for field in ['actorId','reviewedAt','isExempt','sourceId']:
        sandwich_review(e,cc,400,**{field:'forged'})
    expect('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/exception",{'expectedState':'Reserved','reason':'old bypass'},404,'old exception route removed')
    # Same request cannot bypass the final-pair gate.
    e,c=new('SAME-GATE');l=create(e,request('2030-01-11',end='2030-01-14'),840,'same request review gate');before=complete(l)
    command(e,l,'approve',status=409);check(complete(l)==before and cases(e)[0]['state']=='ReviewPending','same-request final approval gate makes no mutation')
    sandwich_review(e,cases(e)[0],outcome='ReasonNotAccepted');command(e,l,'approve');check(balance(e)['sandwichUsedMinutes']==600,'same-request rejected reason charged once')
    # A request completing two cases cannot partially charge a reviewed case before another gate fails.
    e,c=new('MULTI-GATE');left=create(e,request('2030-01-11'),420,'left outer boundary');right=create(e,request('2030-01-21'),420,'right outer boundary')
    middle=create(e,request('2030-01-14',end='2030-01-18'),2100,'middle covers both inner boundaries');ccs=cases(e)
    check(len(ccs)==2 and all(x['state']=='ReviewPending' for x in ccs),'middle request detects two separate potential cases')
    command(e,left,'approve');command(e,right,'approve');sandwich_review(e,ccs[0],outcome='ReasonNotAccepted')
    before=complete(middle);old_cases=rows('SELECT * FROM EmployeeLeaveSandwichCases WHERE EmployeeId='+ident(e));old_balance=balance(e)
    command(e,middle,'approve',status=409)
    check(complete(middle)==before and old_cases==rows('SELECT * FROM EmployeeLeaveSandwichCases WHERE EmployeeId='+ident(e)) and balance(e)==old_balance,'multiple-case gate rolls back all prospective state changes')
    sandwich_review(e,ccs[1]);command(e,middle,'approve');check(balance(e)['usedMinutes']==2940 and balance(e)['sandwichUsedMinutes']==600,'multiple-case final approval charges only reason-not-accepted case')

    # Two frozen policy revisions require two distinct accepted document types.
    doc2=rows("SELECT Id FROM DocumentTypes WHERE Code='DOC-006'")[0]['Id'].lower()
    policy(sick,'EVIDENCE1',False,end='2030-01-11',supportingDocumentPolicy='AlwaysRequired',documentTypeId=doc)
    policy(sick,'EVIDENCE2',False,start='2030-01-12',end='2031-12-31',supportingDocumentPolicy='AlwaysRequired',documentTypeId=doc2)
    e,c=new('EVIDENCE',None);l=create(e,request('2030-01-11',end='2030-01-14',t=sick),840,'multi-type evidence Pending')
    before=complete(l);expect('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{},409,'missing Accepted prerequisite')
    check(complete(l)==before,'failed evidence approval exact request/allocation preservation')
    summary=api('GET',f"employees/{e}/leave/{l['leaveId']}/evidence")
    check(set(summary['requiredDocumentTypeIds'])=={doc,doc2} and l['calculation']['requiredDocumentTypeIds']==summary['requiredDocumentTypeIds'],'frozen required type mapping')
    r1=receipt(e,l);check(r1['state']=='Recorded' and r1['evidenceKind']=='ExternalReceipt' and r1['recordedAt'].endswith('Z'),'external receipt server UTC, no binary claim')
    expect('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{},409,'Recorded not sufficient')
    r1=review(e,l,r1);review(e,l,r1,False,409)
    check(r1['history'][-1]['occurredAt'].endswith('Z') and r1['history'][-1]['remarks']=='Synthetic independent external review','acceptance uses server UTC and explicit review remarks')
    constraint('UPDATE EmployeeLeaveEvidence SET EmployeeId='+ident(EMP)+' WHERE Id='+ident(r1['id']),'SQL relational evidence ownership')
    expect('PUT',f"employees/{e}/leave/{l['leaveId']}/evidence/{r1['id']}",{},404,'no mutable receipt endpoint')
    expect('POST',f"employees/{EMP}/leave/{l['leaveId']}/evidence",{'documentTypeId':doc,'externalReference':'SYNTHETIC'},404,'receipt wrong employee')
    expect('POST',f"employees/{e}/leave/{uuid.uuid4()}/evidence/{r1['id']}/accept",{'reviewRemarks':'Synthetic'},404,'review wrong request')
    expect('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{},409,'all types required, one Accepted insufficient')
    wrong=receipt(e,l,t=rows("SELECT Id FROM DocumentTypes WHERE Code='DOC-001'")[0]['Id']);review(e,l,wrong)
    expect('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{},409,'wrong type Accepted cannot substitute')
    r2=receipt(e,l,t=doc2);review(e,l,r2,False);expect('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{},409,'Rejected prerequisite insufficient')
    r2=receipt(e,l,t=doc2,old=r2['id']);review(e,l,r2)
    successor=receipt(e,l,old=r1['id']);expect('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{},409,'superseded Accepted cannot satisfy approval');review(e,l,successor)
    approved=command(e,l,'approve');ids=approved['evidence']['approvedEvidenceIds']
    check(set(ids)=={successor['id'],r2['id']},'approval exact accepted versions')
    receipt(e,l,old=successor['id']);check(api('GET',f"employees/{e}/leave/{l['leaveId']}")['evidence']['approvedEvidenceIds']==ids,'later supersession preserves approval association')
    cancel(e,l,True);check(len(api('GET',f"employees/{e}/leave/{l['leaveId']}/evidence")['receipts'])==6,'cancelled evidence history retained')
    for field in ['verifiedAt','actorId','storageKey','evidenceKind','recordedAt']:
        receipt(e,l,status=400,**{field:'forged'})
    receipt(e,l,status=400,externalReference='../private')
    check('storageKey' not in json.dumps(approved) and 'ActorId' not in json.dumps(approved),'no private storage/actor fields in response')
    check(not rows('SELECT * FROM EmployeeLeaveEvidenceEvents WHERE ActorId IS NOT NULL'),'no invented authenticated identity')
    rejected=create(e,request('2030-01-09',t=sick),420,'rejected request history');rr=receipt(e,rejected);review(e,rejected,rr,False);command(e,rejected,'reject')
    check(api('GET',f"employees/{e}/leave/{rejected['leaveId']}/evidence")['receipts'][0]['state']=='Rejected','Rejected leave retains rejected evidence history')

    # Accepted evidence must not be attached by a failed final-pair approval.
    policy(annual,'REVIEW-EVIDENCE-GATE',True,start='2033-01-01',end='2033-12-31',sandwichParticipation=True,sandwichEquivalentDayMinutes=200,supportingDocumentPolicy='AlwaysRequired',documentTypeId=doc)
    e,c=new('GATE-EVIDENCE');entitlement(e,year=2033);l,r=pair(e,date='2033-01-07',end='2033-01-10')
    for leave in [l,r]:
        rr=receipt(e,leave);review(e,leave,rr)
    command(e,l,'approve');before=complete(r);associations=rows('SELECT * FROM EmployeeLeaveApprovalEvidence WHERE EmployeeId='+ident(e));events=rows('SELECT * FROM EmployeeLeaveEvidenceEvents WHERE EvidenceId IN (SELECT Id FROM EmployeeLeaveEvidence WHERE EmployeeId='+ident(e)+')')
    command(e,r,'approve',status=409)
    check(complete(r)==before and associations==rows('SELECT * FROM EmployeeLeaveApprovalEvidence WHERE EmployeeId='+ident(e)) and events==rows('SELECT * FROM EmployeeLeaveEvidenceEvents WHERE EvidenceId IN (SELECT Id FROM EmployeeLeaveEvidence WHERE EmployeeId='+ident(e)+')'),'final review gate preserves Accepted evidence and approval associations without mutation')
    sandwich_review(e,cases(e)[0]);command(e,r,'approve');check(len(rows('SELECT * FROM EmployeeLeaveApprovalEvidence WHERE EmployeeId='+ident(e)))==2,'review completion allows exact evidence association on final approval')

    # Review vs final approval: approval first conflicts; review first allows final charge.
    for iteration in range(4):
        e,c=new('RACE-'+str(iteration));l,r=pair(e);command(e,l,'approve');cc=cases(e)[0]
        outcome='ReasonAccepted' if iteration%2==0 else 'ReasonNotAccepted'
        paths=[('POST',f"employees/{e}/leave/{r['leaveId']}/approve",{}),('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/review",{'expectedStatus':'ReviewPending','outcome':outcome,'reason':'Synthetic race'})]
        outcomes=race(paths);races.append({'kind':'review-final-approval','outcomes':[x[0] for x in outcomes],'outcome':outcome})
        check(outcomes[1][0]==200 and outcomes[0][0] in [200,409],'review vs final approval respects ordering')
        if outcomes[0][0]==409:command(e,r,'approve')
        b=balance(e);check(b['sandwichPendingMinutes']==0 and b['sandwichUsedMinutes']==(600 if outcome=='ReasonNotAccepted' else 0),'review race exact final balance')
    e,c=new('REVIEW-RACE');l,r=pair(e);cc=cases(e)[0]
    outcomes=race([('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/review",{'expectedStatus':'ReviewPending','outcome':o,'reason':'Synthetic competing review'}) for o in ['ReasonAccepted','ReasonNotAccepted']])
    check(sorted(x[0] for x in outcomes)==[200,409] and len([x for x in cases(e)[0]['history'] if x['state'] in ['ReasonAccepted','ReasonNotAccepted']])==1,'concurrent decisions only one review and no double debit')
    for action in ['reject','cancel']:
        e,c=new('REVIEW-'+action.upper());l,r=pair(e);cc=cases(e)[0]
        outcomes=race([('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/review",{'expectedStatus':'ReviewPending','outcome':'ReasonNotAccepted','reason':'Synthetic boundary race'}),('POST',f"employees/{e}/leave/{r['leaveId']}/{action}",{'expectedStatus':'Pending'} if action=='cancel' else {})])
        check(outcomes[1][0]==200 and outcomes[0][0] in [200,409] and cases(e)[0]['state']=='Released' and balance(e)['sandwichPendingMinutes']==balance(e)['sandwichUsedMinutes']==0,'boundary '+action+' vs review never retains debit')
    e,c=new('CREATE-RACE');l=create(e,request('2030-01-11'),420,'first race boundary')
    outcomes=race([('POST',f'employees/{e}/leave',request('2030-01-14')),('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{})])
    check(sorted(x[0] for x in outcomes)==[200,201] and len(cases(e))==1,'second request vs approval one case')
    r=next(x for x in api('GET',f'employees/{e}/leave') if x['leaveId']!=l['leaveId'])
    sandwich_review(e,cases(e)[0],outcome='ReasonNotAccepted')
    outcomes=race([('POST',f"employees/{e}/leave/{r['leaveId']}/approve",{}),('POST',f"employees/{e}/leave/{l['leaveId']}/cancel",{'expectedStatus':'Approved','cancellationRemarks':'Synthetic race'})])
    check(all(x[0]==200 for x in outcomes) and cases(e)[0]['state']=='Released' and balance(e)['sandwichUsedMinutes']==0,'Approved cancellation vs final charge releases once')
    e,c=new('DUPLICATE-RACE');create(e,request('2030-01-11'),420,'duplicate race first')
    outcomes=race([('POST',f'employees/{e}/leave',request('2030-01-14'))]*2)
    check(sorted(x[0] for x in outcomes)==[201,409] and len(cases(e))==1 and balance(e)['sandwichPendingMinutes']==0,'concurrent forming requests one potential case and zero reservation')
    e,c=new('ADJUSTMENT-RACE');create(e,request('2030-01-11'),420,'adjustment race first')
    ent=rows('SELECT Id FROM EmployeeLeaveEntitlements WHERE EmployeeId='+ident(e))[0]['Id']
    r=create(e,request('2030-01-14'),420,'adjustment race potential');cc=cases(e)[0]
    outcomes=race([('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/review",{'expectedStatus':'ReviewPending','outcome':'ReasonNotAccepted','reason':'Synthetic review'}),('POST',f'employees/{e}/leave-entitlements/{ent}/adjustments',{'adjustmentMinutes':-9000,'reason':'Synthetic competing reduction'})])
    observed=balance(e)
    check((outcomes[0][0],outcomes[1][0]) in [(200,409),(200,201)] and observed['availableMinutes']>=0 and observed['sandwichPendingMinutes']==(600 if outcomes[1][0]==409 else 160),'adjustment vs review respects capped ordering: '+json.dumps({'outcomes':outcomes,'balance':observed}))
    e,c=new('REJECTION-RACE');l,r=pair(e);command(e,l,'approve');sandwich_review(e,cases(e)[0],outcome='ReasonNotAccepted')
    outcomes=race([('POST',f"employees/{e}/leave/{r['leaveId']}/approve",{}),('POST',f"employees/{e}/leave/{r['leaveId']}/reject",{})])
    check(sorted(x[0] for x in outcomes)==[200,409] and cases(e)[0]['state']==('Charged' if outcomes[0][0]==200 else 'Released'),'boundary rejection vs final charge one winner')
    # Evidence acceptance and supersession race against approval use the same employee serialization.
    e,c=new('EVIDENCE-RACE',None);l=create(e,request('2030-01-07',t=sick),420,'evidence race Pending');rr=receipt(e,l)
    outcomes=race([('POST',f"employees/{e}/leave/{l['leaveId']}/evidence/{rr['id']}/accept",{'reviewRemarks':'Synthetic race'}),('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{})])
    check(outcomes[0][0]==200 and outcomes[1][0] in [200,409],'acceptance vs approval prerequisite ordering')
    if outcomes[1][0]==409:command(e,l,'approve')
    historical=api('GET',f"employees/{e}/leave/{l['leaveId']}")['evidence']['approvedEvidenceIds'];cancel(e,l,True)
    l=create(e,request('2030-01-08',t=sick),420,'supersession race Pending');rr=receipt(e,l);review(e,l,rr)
    outcomes=race([('POST',f"employees/{e}/leave/{l['leaveId']}/evidence",{'documentTypeId':doc,'externalReference':'SYNTHETIC-RACE','supersedesEvidenceId':rr['id']}),('POST',f"employees/{e}/leave/{l['leaveId']}/approve",{})])
    check(outcomes[0][0]==201 and outcomes[1][0] in [200,409],'supersession vs approval ordering')
    current=api('GET',f"employees/{e}/leave/{l['leaveId']}")
    check(current['evidence']['approvedEvidenceIds']==([rr['id']] if outcomes[1][0]==200 else []),'race never approves already-superseded version')
    with urllib.request.urlopen(BASE+'/swagger/v1/swagger.json') as resp:swagger=json.load(resp)
    paths=[p for p in swagger['paths'] if '/evidence' in p or 'leave-sandwich-cases' in p]
    check(len(paths)==5 and sum(len(swagger['paths'][p]) for p in paths)==6,'all six D8D actions Swagger visible')
    check(all('responses' in swagger['paths'][p][v] for p in paths for v in swagger['paths'][p]),'Swagger response documentation')
    for schema in ['LeaveEvidenceRequest','LeaveEvidenceReviewRequest','LeaveSandwichReviewRequest']:
        check(not any(k in swagger['components']['schemas'][schema]['properties'] for k in ['actorId','verifiedAt','storageKey','occurredAt']),'strict Swagger '+schema)
    check(all(not rows('SELECT * FROM '+t) for t in ['Attendance','EmployeePayrolls','EmployeePayrollLines']),'no Attendance or Payroll effects')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        cleanup_d8d();after=snapshot();check(after==baseline,'Exact 73-table Development baseline including timestamps restored')
        check(all(not after[t] for t in new_tables),'all seven new tables empty after cleanup')
        check(len(after['Employees'])==1 and not json.loads(after['Employees'][0])['IsActive'] and len(after['EmploymentRecords'])==1,'single original inactive employee and employment retained')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':len(results),'error':error,'races':races,'counts':{t:len(v) for t,v in snapshot().items()},'results':results},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS:',len(results),'D8D live assertions; exact baseline restored.')
