"""D7 localhost/SIAMIS only. All inputs are synthetic; restore every baseline row exactly."""
import pathlib, sys
capture_before='--capture-before' in sys.argv
if capture_before:sys.argv.remove('--capture-before')
exec(pathlib.Path(__file__).with_name('verify_d6e_live.py').read_text().split('\ntry:\n')[0])
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d7-live-results.json'
PREFIX='D7-VERIFY-'
if capture_before:
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d7-before.json').write_text(json.dumps(snapshot(),sort_keys=True),encoding='utf-8')
    print('Read-only D7 baseline captured.');raise SystemExit(0)
def rows(query):return json.loads(''.join(sql(query+' FOR JSON PATH, INCLUDE_NULL_VALUES').splitlines()) or '[]')
baseline=snapshot();error=None;extra_employee=None
saved_employee=rows('SELECT * FROM Employees WHERE EmployeeId='+ident(EMP))[0]
saved_employment=rows('SELECT * FROM EmploymentRecords WHERE EmployeeId='+ident(EMP))[0]
saved_labels={t:rows('SELECT * FROM ['+t+']') for t in ['Departments','Designations','EmploymentTypes','Locations']}
def restore(table,key,row):sql('UPDATE ['+table+'] SET '+','.join('['+k+']='+literal(v) for k,v in row.items() if k!=key)+' WHERE ['+key+']='+literal(row[key]))
def slip(pid,status=200):return api('GET',f'employee-payrolls/{pid}/payslip',status=status)
def review(pid):return api('GET',f'employee-payrolls/{pid}/review')
def summary(p):return api('GET',f'payroll-periods/{p}/summary')
def corrupt(pid,query,code):
    sql(query)
    try:
        check(code in [x['code'] for x in review(pid)['findings']], 'Review detects '+code)
        api('POST',f'employee-payrolls/{pid}/approve',{},409)
        check(True,'Approval rejects '+code+' without recalculation')
    finally:pass
def race(actions):
    def run(a):
        try:return a()
        except AssertionError as ex:
            if 'got 409' in str(ex) or 'got 404' in str(ex):return 'Conflict'
            raise
    with ThreadPoolExecutor(len(actions)) as pool:return list(pool.map(run,actions))
try:
    expected=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d7-before.json').read_text())
    # Pre-migration helper omits nulls; compare with the same representation.
    actual={t:sorted(json.dumps({k:v for k,v in json.loads(r).items() if v is not None},sort_keys=True) for r in rs) for t,rs in baseline.items()}
    expected.update(EmployeePayslips=[],OrganizationProfiles=[])
    check(actual==expected,'D7 migration adds only two empty tables; exact original rows/timestamps intact')
    constraints=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE OBJECT_NAME(parent_object_id) IN ('EmployeePayslips','OrganizationProfiles')")
    check(len(constraints)==4 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in constraints),'Four live D7 checks enabled/trusted')
    fk=rows("SELECT delete_referential_action_desc FROM sys.foreign_keys WHERE OBJECT_NAME(parent_object_id)='EmployeePayslips'")
    check(len(fk)==1 and fk[0]['delete_referential_action_desc']=='NO_ACTION','Live payslip FK NoAction')
    check(rows("SELECT is_unique FROM sys.indexes WHERE name='IX_EmployeePayslips_EmployeePayrollId'")[0]['is_unique'],'Live unique payslip owner index')
    api('GET','organization-profile',status=404)
    sso=scheme('TH-SSO-33','SocialSecurity');so=enroll(sso,'NotApplicable')
    thpit=scheme('TH-PIT','PersonalIncomeTax');po=enroll(thpit,'NotApplicable')
    paytype=rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id']
    c=api('POST',f'employees/{EMP}/compensations',{'payTypeId':paytype,'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-01','isCurrent':True,'remarks':PREFIX},201);fixtures['compensations'].append(c['compensationId'])
    p=period(10);nov=period(11);dec=period(12)
    draft=api('POST','employee-payrolls',{'employeeId':EMP,'payrollPeriodId':nov},201)['payroll']['employeePayrollId']
    slip(draft,409);check(not review(draft)['payslipReady'],'Draft formal payslip ineligible')
    sql('UPDATE EmploymentRecords SET LocationId='+ident(saved_labels['Locations'][0]['Id'])+' WHERE EmploymentRecordId='+ident(saved_employment['EmploymentRecordId']))
    g=generate(p);check(g['status']=='Generated','Generation succeeds without employer profile')
    pid=g['payrollId'];slip(pid,409)
    check('organization_unconfigured' in [x['code'] for x in review(pid)['findings']],'Missing employer clearly reported')
    api('POST',f'employee-payrolls/{pid}/approve',{},409)
    org=api('PUT','organization-profile',{'displayName':PREFIX+'Synthetic Employer','addressLine1':'Synthetic address'})
    check(api('GET','organization-profile')==org,'Organization PUT/GET persisted')
    api('PUT','organization-profile',{'displayName':' '},400);api('PUT','organization-profile',{'displayName':'Synthetic','createdAt':'2026-01-01'},400)
    check(True,'Organization rejects blank name and caller audit')
    slip(pid,409);check(True,'Organization configuration does not backfill a missing historical snapshot')
    g=generate(p,True);check(g['status']=='Generated','Configured employer permits generated snapshot')
    pid=g['payrollId'];initial=slip(pid);snap=initial['snapshot']
    check(initial['payrollStatus']=='Calculated' and review(pid)['canApprove'],'Calculated payslip exists before approval/payment')
    check(snap['employee']['displayName']=='Test Updated API Employee','Canonical frozen employee display name')
    check(snap['employmentContext']['departmentName']=='Teaching' and snap['employmentContext']['designationName']=='Computing','First eligible employment labels frozen')
    check(snap['currency']=='THB' and snap['totals']['netPay']==30000,'D3 currency and authoritative totals')
    check(snap['statutory']=={'employeeSso':None,'employerSso':None,'pitWithholding':None},'NotApplicable statutory absence, no invented zero')
    for attempt in range(3):
        actions=[lambda:generate(p,True)]+[lambda:slip(pid) for _ in range(4)]+[lambda:review(pid),lambda:detail(pid)]
        outcome=race(actions)
        check(outcome[0]['status']=='Generated','Regeneration racing concurrent stored-fact reads succeeds')
        for read in outcome[1:]:
            if isinstance(read,dict) and 'snapshot' in read:
                t=read['snapshot']['totals'];check(t['grossPay']-t['totalDeductions']==t['netPay'],'Concurrent payslip reader receives a coherent committed snapshot')
        pid=outcome[0]['payrollId'];initial=slip(pid);snap=initial['snapshot']
        check(review(pid)['payslipReady'],'Read/regeneration race leaves one coherent owning snapshot')
    before=snapshot();slip(pid);review(pid);summary(p);detail(pid)
    check(snapshot()==before,'Payslip/review/detail/summary GET perform no writes')
    # Identity edits are fixture-only and restored exactly, including nullable fields and timestamps.
    sql("UPDATE Employees SET PreferredName='D7 Changed Name' WHERE EmployeeId="+ident(EMP))
    for table,field in [('Departments','departmentId'),('Designations','designationId'),('EmploymentTypes','employmentTypeId')]:
        sql('UPDATE ['+table+"] SET Name='D7 Changed Label' WHERE Id="+ident(snap['employmentContext'][field]))
    loc=snap['employmentContext']['locationId']
    if loc:sql("UPDATE Locations SET Name='D7 Changed Location' WHERE Id="+ident(loc))
    api('PUT','organization-profile',{'displayName':PREFIX+'Changed Employer'})
    period_body=api('GET','payroll-periods/'+p)
    api('PUT','payroll-periods/'+p,{k:('D7 Changed Period' if k=='name' else period_body[k]) for k in ['code','name','startDate','endDate','payDate']})
    sql("UPDATE PayrollComponents SET Name='D7 Changed Salary Label' WHERE Code='EARN-001'")
    check(slip(pid)==initial,'Mutable employee/employment/employer/period/component labels do not rewrite payslip')
    hist=api('GET','employee-payrolls?employeeId='+EMP)
    check(next(x for x in hist['items'] if x['payroll']['employeePayrollId']==pid)['employeeName']==snap['employee']['displayName'],'Filtered history uses frozen canonical identity')
    deduction=component('Adjustment','Unknown','Deduction',False)
    m=api('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':deduction['payrollComponentId'],'amount':100,'remarks':PREFIX+'Manual'},201)
    refreshed=slip(pid)['snapshot']
    check(refreshed['totals']['netPay']==29900 and len(refreshed['deductions'])==1,'Manual adjustment atomically refreshes payslip amounts/lines')
    for field in ['employer','employee','employmentContext','period','currency','createdAt']:
        check(refreshed[field]==snap[field],'Manual adjustment preserves frozen '+field)
    m=api('PUT',f"employee-payrolls/{pid}/lines/{m['employeePayrollLineId']}",{'payrollComponentId':deduction['payrollComponentId'],'amount':200,'remarks':PREFIX})
    check(slip(pid)['snapshot']['totals']['netPay']==29800,'Manual update refreshes stored snapshot')
    before=snapshot();api('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':deduction['payrollComponentId'],'amount':999999,'remarks':PREFIX},400)
    check(snapshot()==before,'Failed manual mutation preserves exact payroll and payslip')
    api('DELETE',f"employee-payrolls/{pid}/lines/{m['employeePayrollLineId']}",status=204)
    check(slip(pid)['snapshot']==snap,'Manual delete restores monetary content without recapturing identity')
    for row in saved_components:restore_component(row)
    # Successful regeneration refreshes all presentation facts.
    g=generate(p,True);pid=g['payrollId'];renewed=slip(pid)
    check(renewed['snapshot']['employee']['displayName'].startswith('D7 Changed Name') and renewed['snapshot']['employer']['displayName'].endswith('Changed Employer') and renewed['snapshot']['period']['name']=='D7 Changed Period','Regeneration refreshes presentation facts')
    conflict=component('Conflict');r1=rule(conflict,'ReplaceAssignment');r2=rule(conflict,'ReplaceAssignment')
    before=snapshot();check(generate(p,True)['status']=='Failed' and snapshot()==before,'Failed regeneration preserves complete payroll/payslip/statutory state')
    check(generate(dec)['status']=='Failed' and not rows('SELECT * FROM EmployeePayrolls WHERE PayrollPeriodId='+ident(dec)),'Failed NEW generation retains no payroll/payslip')
    delete_rule(r1);delete_rule(r2)
    # Corrupt only a fixture and restore it before continuing.
    sql('UPDATE EmployeePayrolls SET GrossPay=GrossPay+1 WHERE EmployeePayrollId='+ident(pid))
    check('totals_mismatch' in [x['code'] for x in review(pid)['findings']],'Totals mismatch detected')
    api('POST',f'employee-payrolls/{pid}/approve',{},409)
    sql('UPDATE EmployeePayrolls SET GrossPay=GrossPay-1 WHERE EmployeePayrollId='+ident(pid))
    stored=rows('SELECT * FROM EmployeePayslips WHERE EmployeePayrollId='+ident(pid))[0]
    sql('DELETE EmployeePayslips WHERE EmployeePayrollId='+ident(pid))
    check('payslip_missing' in [x['code'] for x in review(pid)['findings']],'Missing required payslip detected')
    api('POST',f'employee-payrolls/{pid}/approve',{},409)
    sql('INSERT INTO EmployeePayslips ('+','.join('['+k+']' for k in stored)+') VALUES ('+','.join(literal(v) for v in stored.values())+')')
    rejected=sql("BEGIN TRANSACTION; BEGIN TRY INSERT INTO EmployeePayslips SELECT NEWID(),EmployeePayrollId,SnapshotVersion,SnapshotJson,CreatedAt,UpdatedAt FROM EmployeePayslips WHERE EmployeePayrollId="+ident(pid)+"; ROLLBACK; THROW 51000,'Duplicate accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER() NOT IN (2601,2627) THROW; SELECT 'Rejected' Result; END CATCH;")
    check('Rejected' in rejected,'Duplicate payslip rejected by actual SQL index')
    rejected=sql("BEGIN TRANSACTION; BEGIN TRY INSERT INTO OrganizationProfiles (OrganizationProfileId,DisplayName,CreatedAt,UpdatedAt) VALUES (NEWID(),'Synthetic',SYSUTCDATETIME(),SYSUTCDATETIME()); ROLLBACK; THROW 51000,'Singleton accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER()<>547 THROW; SELECT 'Rejected' Result; END CATCH;")
    check('Rejected' in rejected,'Singleton enforced by actual SQL constraint')
    # Preserve exact JSON and row audit values across lifecycle transitions.
    frozen=rows('SELECT * FROM EmployeePayslips WHERE EmployeePayrollId='+ident(pid))
    api('POST',f'employee-payrolls/{pid}/approve',{},204)
    check(rows('SELECT * FROM EmployeePayslips WHERE EmployeePayrollId='+ident(pid))==frozen and slip(pid)['payrollStatus']=='Approved','Approval preserves exact payslip row')
    api('POST',f'employee-payrolls/{pid}/mark-paid',{},204)
    check(rows('SELECT * FROM EmployeePayslips WHERE EmployeePayrollId='+ident(pid))==frozen and slip(pid)['payrollStatus']=='Paid','Payment preserves exact payslip row')
    for table in saved_labels:
        for row in saved_labels[table]:restore(table,'Id',row)
    restore('Employees','EmployeeId',saved_employee)
    check(rows('SELECT * FROM EmployeePayslips WHERE EmployeePayrollId='+ident(pid))==frozen,'Finalized labels remain unchanged after master edits/restoration')
    # Period summary counts only actual headers. Draft has unresolved currency.
    check(summary(nov)['unresolvedCurrencyCount']==1 and summary(nov)['currencySummaries']==[],'Draft currency remains unresolved')
    curr=summary(p)['currencySummaries'][0]
    check(curr['currency']=='THB' and curr['grossPayTotal']==30000 and curr['netPayTotal']==30000 and curr['employeeCount']==1,'Same currency totals and represented count correct')
    check(summary(p)['statusCounts']==[{'status':'Paid','count':1}],'Period status counts correct')
    # Regeneration / approval and regeneration / payment share the existing parent lock.
    ng=generate(nov,True);npid=ng['payrollId'];check(ng['status']=='Generated','Draft deliberately generated')
    race([lambda:generate(nov,True),lambda:api('POST',f'employee-payrolls/{npid}/approve',{},204)])
    current=api('GET','employee-payrolls?payrollPeriodId='+nov)['items'][0]['payroll']
    npid=current['employeePayrollId'];check(review(npid)['payslipReady'],'Regeneration/approval race retains coherent payroll/payslip')
    if current['status']=='Calculated':api('POST',f'employee-payrolls/{npid}/approve',{},204)
    race([lambda:generate(nov,True),lambda:api('POST',f'employee-payrolls/{npid}/mark-paid',{},204)])
    check(slip(npid)['payrollStatus']=='Paid' and review(npid)['payslipReady'],'Payment/regeneration race preserves Approved/Paid snapshot')
    # Statutory result display and integrity, using explicit synthetic policy values.
    cleanup();fixtures={k:[] for k in fixtures}
    sso=scheme('TH-SSO-33','SocialSecurity');thpit=scheme('TH-PIT','PersonalIncomeTax')
    enroll(sso);enroll(thpit);declaration();schedule(entries())
    c=api('POST',f'employees/{EMP}/compensations',{'payTypeId':paytype,'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-01','isCurrent':True,'remarks':PREFIX},201);fixtures['compensations'].append(c['compensationId'])
    dec=period(12);deduction=component('StatutoryAdjustment','Unknown','Deduction',False)
    for sid,method,kind in [(sso,'SSO-TH-V1','social-security'),(thpit,'PIT-TH-V1','personal-income-tax')]:
        pol=api('POST','statutory-policy-versions',{'statutorySchemeId':sid,'version':PREFIX+method,'effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','currency':'THB','officialReference':'Synthetic software fixture only','calculationMethodVersion':method},201)['statutoryPolicyVersionId']
        body={'employeeContributionRate':1,'employerContributionRate':1,'minimumContributionBase':1,'maximumContributionBase':99999,'insuredPersonClassification':'33'} if kind=='social-security' else {'taxYear':2026,'employmentExpenseDeductionRate':0,'employmentExpenseDeductionCap':1,'personalAllowanceAmount':0,'spouseAllowanceAmount':0,'childAllowanceAmount':0,'additionalChildAllowanceAmount':0,'parentAllowanceAmount':0,'adoptedChildCombinedCountLimit':3,'maximumEligibleParentCount':4,'withholdingMethodIdentifier':'Synthetic monthly','brackets':[{'lowerBoundInclusive':0,'upperBoundExclusive':None,'rate':1,'sortOrder':1}]}
        api('PUT',f'statutory-policy-versions/{pol}/'+kind,body);api('POST',f'statutory-policy-versions/{pol}/publish',{})
    sql("UPDATE PayrollComponents SET SsoWageTreatment='Included',PitIncomeTreatment='Included',PitPaymentTreatment='Regular' WHERE Code='EARN-001'")
    dg=generate(dec);dpid=dg['payrollId'];check(dg['status']=='Generated','Synthetic SSO/PIT generation succeeds: '+str(dg))
    ss=detail(dpid)['statutoryResults'][0];pr=pit(dpid);ps=slip(dpid)['snapshot']
    check(ps['statutory']=={'employeeSso':ss['employeeAmount'],'employerSso':ss['employerAmount'],'pitWithholding':pr['currentWithholding']},'Payslip statutory summary uses exact stored SSO/PIT')
    check(ps['totals']['totalDeductions']==ss['employeeAmount']+pr['currentWithholding'] and ps['totals']['netPay']==30000-ps['totals']['totalDeductions'],'Employer SSO is separate from net deductions')
    cs=summary(dec)['currencySummaries'][0]
    check(cs['employeeSsoTotal']==ss['employeeAmount'] and cs['employerSsoTotal']==ss['employerAmount'] and cs['pitWithholdingTotal']==pr['currentWithholding'],'SSO/PIT period aggregates exact')
    for code,finding in [('DEDUCT-001','sso_integrity'),('DEDUCT-002','pit_integrity')]:
        line=next(x for x in detail(dpid)['lines'] if x['componentCode']==code)
        corrupt(dpid,'UPDATE EmployeePayrollLines SET Amount=Amount+1 WHERE EmployeePayrollLineId='+ident(line['employeePayrollLineId']),finding)
        sql('UPDATE EmployeePayrollLines SET Amount=Amount-1 WHERE EmployeePayrollLineId='+ident(line['employeePayrollLineId']))
    sso_row=rows('SELECT * FROM EmployeePayrollStatutoryResults WHERE EmployeePayrollId='+ident(dpid))[0]
    sql('UPDATE EmployeePayrollStatutoryResults SET EmployeeStatutoryEnrollmentId='+ident(fixtures['enrollments'][-1])+' WHERE EmployeePayrollStatutoryResultId='+ident(sso_row['EmployeePayrollStatutoryResultId']))
    check('sso_integrity' in [x['code'] for x in review(dpid)['findings']],'SSO result/enrollment snapshot ownership inconsistency detected')
    api('POST',f'employee-payrolls/{dpid}/approve',{},409)
    restore('EmployeePayrollStatutoryResults','EmployeePayrollStatutoryResultId',sso_row)
    frozen=slip(dpid)['snapshot']
    api('POST',f'employee-payrolls/{dpid}/lines',{'payrollComponentId':deduction['payrollComponentId'],'amount':1,'remarks':PREFIX},201)
    check(slip(dpid)['snapshot']['statutory']==frozen['statutory'],'Allowed deduction refresh preserves statutory values')
    salaryid=next(x['Id'] for x in saved_components if x['Code']=='EARN-001')
    api('POST',f'employee-payrolls/{dpid}/lines',{'payrollComponentId':salaryid,'amount':1,'remarks':PREFIX},409)
    check(True,'Existing SSO/PIT stale-earning guard preserved')
    sql("UPDATE PayrollComponents SET PitIncomeTreatment='Excluded' WHERE Code='EARN-001'")
    check(slip(dpid)['snapshot']['statutory']==frozen['statutory'],'Live classification does not rewrite stored statutory presentation')
    zg=generate(dec,True);check(zg['status']=='Generated','Deliberate zero PIT regeneration succeeds')
    dpid=zg['payrollId'];zero=slip(dpid)['snapshot']
    check(zero['statutory']['pitWithholding']==0 and not [x for x in zero['deductions'] if x['componentCode']=='DEDUCT-002'],'Applicable zero PIT evidence without a fabricated deduction line')
    # Manual adjustment vs approval commits one internally consistent state.
    race([lambda:api('POST',f'employee-payrolls/{dpid}/lines',{'payrollComponentId':deduction['payrollComponentId'],'amount':1,'remarks':PREFIX},201),lambda:api('POST',f'employee-payrolls/{dpid}/approve',{},204)])
    check(slip(dpid)['payrollStatus']=='Approved' and review(dpid)['payslipReady'],'Manual adjustment/approval race coherent')
    # Cancelled snapshot retained: fresh month after clearing only fixture paid states by separate period.
    # Use a generated September joiner with opt-outs to avoid asserting PIT partial-month semantics.
    for row in saved_components:restore_component(row)
    cleanup();fixtures={k:[] for k in fixtures}
    sso=scheme('TH-SSO-33','SocialSecurity');enroll(sso,'NotApplicable');thpit=scheme('TH-PIT','PersonalIncomeTax');enroll(thpit,'NotApplicable')
    c=api('POST',f'employees/{EMP}/compensations',{'payTypeId':paytype,'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-01','isCurrent':True,'remarks':PREFIX},201);fixtures['compensations'].append(c['compensationId'])
    p=period(10);pid=generate(p)['payrollId'];frozen=rows('SELECT * FROM EmployeePayslips WHERE EmployeePayrollId='+ident(pid))
    api('POST',f'employee-payrolls/{pid}/cancel',{'reason':PREFIX},204)
    check(slip(pid)['payrollStatus']=='Cancelled' and rows('SELECT * FROM EmployeePayslips WHERE EmployeePayrollId='+ident(pid))==frozen,'Cancellation retains exact historical payslip and exposes status')
    body={'employeeNumber':PREFIX+'USD','firstName':'Synthetic','lastName':'Currency','departmentId':saved_employment['DepartmentId'],'designationId':saved_employment['DesignationId'],'employmentTypeId':saved_employment['EmploymentTypeId'],'employmentStatusId':saved_employment['EmploymentStatusId'],'hireDate':'2026-10-01'}
    extra_employee=api('POST','employees',body,201)['employeeId']
    usd=api('POST',f'employees/{extra_employee}/compensations',{'payTypeId':paytype,'basicSalary':1234,'currency':'USD','effectiveFrom':'2026-10-01','isCurrent':True,'remarks':PREFIX},201);fixtures['compensations'].append(usd['compensationId'])
    for sid in [sso,thpit]:
        en=api('POST',f'employees/{extra_employee}/statutory-enrollments',{'statutorySchemeId':sid,'effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','applicability':'NotApplicable'},201);fixtures['enrollments'].append(en['employeeStatutoryEnrollmentId'])
    ug=api('POST',f'payroll-periods/{p}/generate',{'employeeIds':[extra_employee]})['results'][0]
    check(ug['status']=='Generated' and slip(ug['payrollId'])['snapshot']['currency']=='USD','Non-THB generated currency comes from stored D3')
    sm=summary(p);gs={x['currency']:x for x in sm['currencySummaries']}
    check(set(gs)=={'THB','USD'} and gs['THB']['grossPayTotal']==30000 and gs['USD']['grossPayTotal']==1234 and sm['employeeCount']==2,'Period summary separates currencies without FX or combined monetary totals')
    salary=next(x for x in detail(ug['payrollId'])['lines'] if x['sourceType']=='BasicSalary')
    sql('UPDATE EmployeePayrollLines SET BasicSalaryCalculationSnapshotJson=NULL WHERE EmployeePayrollLineId='+ident(salary['employeePayrollLineId']))
    check(summary(p)['unresolvedCurrencyCount']==1 and [x['currency'] for x in summary(p)['currencySummaries']]==['THB'],'Missing D3 currency reported separately, never guessed')
    slip(ug['payrollId'],409);api('POST',f"employee-payrolls/{ug['payrollId']}/approve",{},409)
    check(True,'Missing authoritative currency blocks payslip and approval')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        restore('Employees','EmployeeId',saved_employee)
        restore('EmploymentRecords','EmploymentRecordId',saved_employment)
        for table,rs in saved_labels.items():
            for row in rs:restore(table,'Id',row)
        for row in saved_components:restore_component(row)
        cleanup()
        if extra_employee:sql('DELETE EmploymentRecords WHERE EmployeeId='+ident(extra_employee)+'; DELETE Employees WHERE EmployeeId='+ident(extra_employee))
        sql("DELETE OrganizationProfiles WHERE OrganizationProfileId='00000000-0000-0000-0000-000000000001'")
        final=snapshot();check(final==baseline,'Exact all-table Development baseline restored; no temporary organization/payroll/payslip fixtures')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':results,'error':error,'finalCounts':{t:len(v) for t,v in locals().get('final',{}).items()}},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} D7 live checks; exact baseline restored.',flush=True)
