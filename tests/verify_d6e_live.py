"""D6E local Development integration verification. Synthetic monetary parameters only."""
import pathlib, sys
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
PRE=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6e-before-migration.json'
if '--capture-before' in sys.argv:
    b=snapshot();PRE.write_text(json.dumps(b,sort_keys=True),encoding='utf-8')
    print('Pre-migration exact baseline captured:',len(b),'tables');print({t:len(v) for t,v in b.items() if v});raise SystemExit(0)
if '--verify-final' in sys.argv:
    b=snapshot();expected=json.loads(PRE.read_text(encoding='utf-8'))
    expected.update({t:[] for t in ['EmployeePitPaymentSchedules','EmployeePitPaymentScheduleEntries','EmployeePitPaymentScheduleSelections','EmployeePayrollPitResults']})
    check(b==expected,'Final independent exact baseline comparison: all original rows/timestamps plus four empty tables')
    check(all(x['SsoWageTreatment']==x['PitIncomeTreatment']==x['PitPaymentTreatment']=='Unknown' for x in rows('SELECT SsoWageTreatment,PitIncomeTreatment,PitPaymentTreatment FROM PayrollComponents')),'All 17 real component classifications remain Unknown')
    check(rows('SELECT IsActive FROM Employees WHERE EmployeeId='+ident(EMP))[0]['IsActive']==False,'TEST-EMP-001 remains inactive')
    final={'exactBaseline':True,'tableCounts':{t:len(v) for t,v in b.items()},'migration':rows('SELECT TOP(1) MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC')}
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6e-final-baseline.json').write_text(json.dumps(final,indent=2),encoding='utf-8')
    print(json.dumps(final,indent=2));raise SystemExit(0)
from concurrent.futures import ThreadPoolExecutor
import calendar
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6e-live-results.json'
PREFIX='D6E-VERIFY-'
baseline=snapshot();error=None
saved_components=rows('SELECT * FROM PayrollComponents')
def literal(v):
    if v is None:return 'NULL'
    if isinstance(v,bool):return '1' if v else '0'
    if isinstance(v,(int,float)):return str(v)
    return "N'"+str(v).replace("'","''")+"'"
def restore_component(r):
    sql('UPDATE PayrollComponents SET '+','.join('['+k+']='+literal(v) for k,v in r.items() if k!='Id')+' WHERE Id='+ident(r['Id']))
def scheme(code,kind):
    s=api('POST','statutory-schemes',{'code':code,'name':PREFIX+code,'jurisdiction':'TH','schemeType':kind},201)
    fixtures['schemes'].append(s['statutorySchemeId']);return s['statutorySchemeId']
def enroll(s,app='Applicable',start='2026-01-01',end='2026-12-31',status=201):
    e=api('POST',f'employees/{EMP}/statutory-enrollments',{'statutorySchemeId':s,'effectiveFrom':start,'effectiveTo':end,'applicability':app},status)
    if status==201:fixtures['enrollments'].append(e['employeeStatutoryEnrollmentId'])
    return e

def declaration():
    d=api('POST',f'employees/{EMP}/tax-declarations',{'taxYear':2026,'remarks':PREFIX+'Reviewed complete synthetic inputs'},201)['declaration']['employeeTaxDeclarationId']
    fixtures['declarations'].append(d);path=f'employees/{EMP}/tax-declarations/{d}'
    api('PUT',path+'/treatment',{'residencyStatus':'Resident','employmentTaxTreatment':'StandardSection40_1'})
    api('PUT',path+'/opening-balance',{'state':'ConfirmedZero','currency':'THB','asOfDate':'2025-12-31','openingBalanceScope':'CurrentEmployer','completenessAttested':True,'remarks':PREFIX+'Complete current employer zero opening'})
    api('POST',path+'/verify',{});return d

def entries(start=1,count=12,year=2026):return [{'paymentOrdinal':i+1,'plannedPayDate':f'{year}-{m:02}-{calendar.monthrange(year,m)[1]:02}'} for i,m in enumerate(range(start,start+count))]
def schedule(es,year=2026,verify=True):
    s=api('POST',f'employees/{EMP}/pit-payment-schedules',{'taxYear':year,'evidence':PREFIX+'Reviewed monthly payments','entries':es},201)
    fixtures['schedules'].append(s['employeePitPaymentScheduleId'])
    if verify:s=api('POST',f"employees/{EMP}/pit-payment-schedules/{s['employeePitPaymentScheduleId']}/verify",{})
    return s

def pit(pid):return api('GET',f'employee-payrolls/{pid}/pit-result')
def failed(p,fragment,old=None):
    before=detail(old) if old else None;oldpit=pit(old) if old else None
    pv=preview(p);g=generate(p,bool(old))
    check(pv['status']=='Failed' and fragment.lower() in pv['message'].lower(),'Preview unresolved: '+fragment+(' '+str(pv) if pv['status']!='Failed' or fragment.lower() not in pv['message'].lower() else ''))
    check(g['status']=='Failed' and fragment.lower() in g['message'].lower(),'Generation fails atomically: '+fragment+(' '+str(g) if g['status']!='Failed' or fragment.lower() not in g['message'].lower() else ''))
    if old:check(detail(old)==before and pit(old)==oldpit,'Failed regeneration preserves complete payroll/PIT: '+fragment)
    else:check(not rows('SELECT * FROM EmployeePayrolls WHERE PayrollPeriodId='+ident(p)) and not rows('SELECT * FROM EmployeePayrollPitResults WHERE PayrollPeriodId='+ident(p)),'No partial new header/result: '+fragment)

def parity(p,force=True):
    before=snapshot();pv=preview(p);check(snapshot()==before,'Preview writes no application data')
    g=generate(p,force);check(pv['status']=='Calculated' and g['status']=='Generated','Preview/Generation succeed: '+str(g))
    d=detail(g['payrollId']);r=pit(g['payrollId']);snap=json.loads(r['calculationSnapshotJson'])
    check(totals(pv)==totals(d['payroll']),'Preview/Generation monetary parity')
    check(pv['pit']['snapshot']['calculation']['currentWithholding']==r['currentWithholding'],'Preview/Generation PIT parity')
    lines=[l for l in d['lines'] if l['componentCode']=='DEDUCT-002']
    check(len(lines)==(1 if r['currentWithholding']>0 else 0),'Exactly one positive PIT line; zero persists result without line')
    if lines:check(lines[0]['sourceType']=='Statutory' and lines[0]['sourceId']==r['employeePayrollPitResultId'] and lines[0]['amount']==r['currentWithholding'],'PIT generated provenance matches exact result')
    check(all(l['sourceId'] is None for l in pv['lines'] if l['componentCode']=='DEDUCT-002'),'Preview never presents fake persisted PIT result ID')
    check(d['payroll']['totalDeductions']==sum(l['amount'] for l in d['lines'] if l['componentType']=='Deduction') and d['payroll']['netPay']==d['payroll']['grossPay']-d['payroll']['totalDeductions'],'PIT reconciled exactly once')
    return d,r,snap

try:
    expected=json.loads(PRE.read_text());expected.update({t:[] for t in ['EmployeePitPaymentSchedules','EmployeePitPaymentScheduleEntries','EmployeePitPaymentScheduleSelections','EmployeePayrollPitResults']})
    check(baseline==expected and len(baseline)==57,'Migration added four empty tables; original rows/timestamps untouched')
    check(rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddPitPayrollIntegration'")!=[],'D6E migration recorded')
    fks=rows("SELECT name,delete_referential_action_desc FROM sys.foreign_keys WHERE OBJECT_NAME(parent_object_id) IN ('EmployeePayrollPitResults','EmployeePitPaymentSchedules','EmployeePitPaymentScheduleEntries','EmployeePitPaymentScheduleSelections')")
    check(len(fks)==12 and all(x['delete_referential_action_desc']=='NO_ACTION' for x in fks),'All twelve new FKs NoAction')
    checks=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE OBJECT_NAME(parent_object_id) IN ('EmployeePayrollPitResults','EmployeePitPaymentSchedules','EmployeePitPaymentScheduleEntries','EmployeePitPaymentScheduleSelections')")
    check(len(checks)==8 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in checks),'Eight trusted enabled structural checks')
    sso=scheme('TH-SSO-33','SocialSecurity');ssoopt=enroll(sso,'NotApplicable')
    paytype=rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id']
    c=api('POST',f'employees/{EMP}/compensations',{'payTypeId':paytype,'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-01','isCurrent':True,'remarks':PREFIX+'Synthetic'},201);fixtures['compensations'].append(c['compensationId'])
    p=period(10);nov=period(11);dec=period(12)
    failed(p,'Unknown')
    thpit=scheme('TH-PIT','PersonalIncomeTax')
    failed(p,'Unknown')
    opt=enroll(thpit,'NotApplicable')
    g=generate(p);check(g['status']=='Generated','Explicit NotApplicable produces ordinary payroll')
    check(pit(g['payrollId']) is None and not [l for l in detail(g['payrollId'])['lines'] if l['componentCode']=='DEDUCT-002'],'NotApplicable no PIT result/line')
    # Replace only this synthetic enrollment to exercise Applicable; no real applicability backfill.
    sql('DELETE EmployeeStatutoryEnrollments WHERE EmployeeStatutoryEnrollmentId='+ident(opt['employeeStatutoryEnrollmentId']))
    en=enroll(thpit,start='2026-10-31')
    enroll(thpit,start='2026-10-31',status=409);check(True,'PIT enrollment overlap rejected')
    failed(p,'schedule',g['payrollId'])
    d=declaration()
    draft=schedule(entries(),verify=False)
    failed(p,'Verified',g['payrollId'])
    full=api('POST',f"employees/{EMP}/pit-payment-schedules/{draft['employeePitPaymentScheduleId']}/verify",{})
    full=api('GET',f"employees/{EMP}/pit-payment-schedules/{draft['employeePitPaymentScheduleId']}")
    check(len(full['entries'])==12 and full['isCurrentVerified'],'Verified full-year N=12 authority')
    api('POST',f"employees/{EMP}/pit-payment-schedules/{full['employeePitPaymentScheduleId']}/verify",{},409)
    check(True,'Verified schedule cannot be rewritten/reverified')
    pol=api('POST','statutory-policy-versions',{'statutorySchemeId':thpit,'version':PREFIX+'Synthetic-V1','effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','currency':'THB','officialReference':PREFIX+'Software test only, not legal policy','calculationMethodVersion':'PIT-TH-V1'},201)['statutoryPolicyVersionId']
    api('PUT',f'statutory-policy-versions/{pol}/personal-income-tax',{'taxYear':2026,'employmentExpenseDeductionRate':0,'employmentExpenseDeductionCap':1,'personalAllowanceAmount':0,'spouseAllowanceAmount':0,'childAllowanceAmount':0,'additionalChildAllowanceAmount':0,'parentAllowanceAmount':0,'adoptedChildCombinedCountLimit':3,'maximumEligibleParentCount':4,'withholdingMethodIdentifier':'Synthetic monthly','brackets':[{'lowerBoundInclusive':0,'upperBoundExclusive':None,'rate':1,'sortOrder':1}]})
    api('POST',f'statutory-policy-versions/{pol}/publish',{})
    sql('DELETE EmployeeStatutoryEnrollments WHERE EmployeeStatutoryEnrollmentId='+ident(ssoopt['employeeStatutoryEnrollmentId']))
    enroll(sso)
    ssopol=api('POST','statutory-policy-versions',{'statutorySchemeId':sso,'version':PREFIX+'Synthetic-SSO','effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','currency':'THB','officialReference':PREFIX+'Synthetic software fixture','calculationMethodVersion':'SSO-TH-V1'},201)['statutoryPolicyVersionId']
    api('PUT',f'statutory-policy-versions/{ssopol}/social-security',{'employeeContributionRate':1,'employerContributionRate':1,'minimumContributionBase':1,'maximumContributionBase':99999,'insuredPersonClassification':'33'})
    api('POST',f'statutory-policy-versions/{ssopol}/publish',{})
    sql("UPDATE PayrollComponents SET SsoWageTreatment='Included' WHERE Code='EARN-001'")
    failed(p,'candidate',g['payrollId'])
    sql("UPDATE PayrollComponents SET PitIncomeTreatment='Included',PitPaymentTreatment='Regular' WHERE Code='EARN-001'")
    octdata,octpit,octsnap=parity(p)
    check(octpit['currentWithholding']==299.75 and octdata['payroll']['grossPay']==30000 and octdata['payroll']['totalDeductions']==599.75 and octdata['payroll']['netPay']==29400.25,'Synthetic employee SSO 300 plus PIT 299.75 exact; no change Basic/Gross/Taxable')
    check(octsnap['calculation']['recognizedEmployeeSso']==300 and len(octsnap['calculation']['input']['ssoSources'])==2,'Current employee-side SSO exact; employer never recognized')
    check(octsnap['schedule']['employeePitPaymentScheduleId']==full['employeePitPaymentScheduleId'] and octsnap['calculation']['applicablePaymentCount']==12 and octsnap['calculation']['input']['schedule']['paymentOrdinal']==10 and not octsnap['calculation']['isFinalScheduledPayment'],'Exact schedule revision N/date/ordinal/final provenance')
    # SQL-invalid JSON/result amount rolls back and constraints are really enforced.
    rejected=sql('BEGIN TRANSACTION; BEGIN TRY UPDATE EmployeePayrollPitResults SET CurrentWithholding=-1 WHERE EmployeePayrollPitResultId='+ident(octpit['employeePayrollPitResultId'])+"; ROLLBACK; THROW 51000,'Accepted invalid amount',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER()<>547 THROW; SELECT 'Rejected547' Result; END CATCH;")
    check('Rejected547' in rejected,'SQL negative result rejected')
    basic=next(r for r in saved_components if r['Code']=='EARN-001')['Id'];deduct=next(r for r in saved_components if r['Code']=='DEDUCT-002')['Id'];other=next(r for r in saved_components if r['Code']=='DEDUCT-007')['Id']
    pid=octdata['payroll']['employeePayrollId'];manualbody={'payrollComponentId':basic,'amount':1,'remarks':PREFIX+'Manual'}
    api('POST',f'employee-payrolls/{pid}/lines',manualbody,409)
    api('POST',f'employee-payrolls/{pid}/lines',{**manualbody,'payrollComponentId':deduct},409)
    pitline=next(l for l in octdata['lines'] if l['componentCode']=='DEDUCT-002')
    api('PUT',f"employee-payrolls/{pid}/lines/{pitline['employeePayrollLineId']}",{**manualbody,'payrollComponentId':deduct},409)
    api('DELETE',f"employee-payrolls/{pid}/lines/{pitline['employeePayrollLineId']}",status=409)
    check(True,'Manual PIT create/update/delete and manual earning create blocked')
    m=api('POST',f'employee-payrolls/{pid}/lines',{**manualbody,'payrollComponentId':other,'amount':5},201)
    api('PUT',f"employee-payrolls/{pid}/lines/{m['employeePayrollLineId']}",manualbody,409)
    api('DELETE',f"employee-payrolls/{pid}/lines/{m['employeePayrollLineId']}",status=204)
    check(totals(detail(pid)['payroll'])==totals(octdata['payroll']) and detail(pid)['lines']==octdata['lines'] and pit(pid)==octpit,'Unrelated manual deduction reconciliation leaves PIT evidence/amount unchanged')
    # Canonical component ownership on assignment and rule paths.
    a=api('POST',f'employees/{EMP}/payroll-component-assignments',{'payrollComponentId':deduct,'amount':1,'effectiveFrom':'2026-09-01','remarks':PREFIX},201);fixtures['assignments'].append(a['employeePayrollComponentAssignmentId'])
    failed(nov,'PIT-owned');api('DELETE',f"employees/{EMP}/payroll-component-assignments/{a['employeePayrollComponentAssignmentId']}",status=204)
    dc=api('GET','payroll-components/'+deduct);r=rule(dc,stage='Deduction',amount=1)
    failed(nov,'PIT-owned');delete_rule(r)
    # The same transactional path serializes concurrent generation.
    with ThreadPoolExecutor(2) as pool:concurrent=list(pool.map(lambda _:generate(nov),range(2)))
    check(sorted(x['status'] for x in concurrent)==['Generated','Skipped'],'Concurrent new generation: one Generated, one Skipped, no duplicate')
    novpid=next(x['payrollId'] for x in concurrent if x['status']=='Generated')
    check(len(rows('SELECT * FROM EmployeePayrollPitResults WHERE PayrollPeriodId='+ident(nov)))==1,'Concurrent generation one authoritative PIT result')
    check(json.loads(pit(novpid)['calculationSnapshotJson'])['calculation']['priorRecognizedWithholding']==0,'Calculated predecessor contributes no history')
    api('POST',f'employee-payrolls/{pid}/approve',{},204)
    check(generate(p,True)['status']=='Skipped','Approved payroll protected')
    preview_before=preview(dec)['pit']['snapshot']['calculation'];check(preview_before['priorRecognizedWithholding']==0,'Approved history excluded')
    api('POST',f'employee-payrolls/{pid}/mark-paid',{},204)
    check(generate(p,True)['status']=='Skipped','Paid payroll protected')
    dpv=preview(dec)['pit']['snapshot']['calculation']
    check(dpv['priorRecognizedIncome']==30000 and dpv['priorRecognizedWithholding']==299.75 and dpv['recognizedEmployeeSso']==600,'Paid income/exact PIT plus historical/current employee SSO recognized')
    sql("UPDATE PayrollComponents SET PitIncomeTreatment='Excluded',PitPaymentTreatment='Special' WHERE Code='EARN-001'")
    check(pit(pid)==octpit and detail(pid)['lines']==octdata['lines'],'Live classifications cannot mutate Paid PIT snapshot or lines')
    d2=schedule(entries(7,6))
    check(d2['revisionNumber']==2 and d2['replacesScheduleId']==full['employeePitPaymentScheduleId'] and len(d2['entries'])==6,'Replacement selected N=6 midyear schedule')
    oldfull=api('GET',f"employees/{EMP}/pit-payment-schedules/{full['employeePitPaymentScheduleId']}")
    check(oldfull['entries']==full['entries'] and oldfull['verifiedAt']==full['verifiedAt'] and not oldfull['isCurrentVerified'] and pit(pid)==octpit,'Later schedule selection preserves Verified predecessor and historical PIT result')
    # Concurrent revision creation: only one Draft; verification safe with employee lock.
    def concurrent_create(_):
        try:return api('POST',f'employees/{EMP}/pit-payment-schedules',{'taxYear':2026,'evidence':PREFIX+'Concurrent replacement','entries':entries(7,6)},201)
        except AssertionError as ex:
            if 'got 409' in str(ex):return None
            raise
    with ThreadPoolExecutor(2) as pool:created=list(pool.map(concurrent_create,range(2)))
    created=[s for s in created if s];check(len(created)==1,'Concurrent replacement admits only one Draft')
    fixtures['schedules'].append(created[0]['employeePitPaymentScheduleId'])
    api('POST',f"employees/{EMP}/pit-payment-schedules/{created[0]['employeePitPaymentScheduleId']}/verify",{})
    # Current Excluded means zero; Paid history still recognized from stored Included snapshots.
    dd,dr,ds=parity(dec)
    check(dr['currentWithholding']==0 and ds['calculation']['priorRecognizedIncome']==30000 and ds['calculation']['overWithheldAmount']==299.75,'Zero result persists; stored Paid income unchanged; excess withheld not a refund')
    zpid=dd['payroll']['employeePayrollId'];api('POST',f'employee-payrolls/{zpid}/lines',manualbody,409)
    check(True,'Zero PIT result still blocks manual earning mutations')
    # RequiresReview regeneration leaves zero ledger and complete old payroll intact.
    sql("UPDATE PayrollComponents SET PitIncomeTreatment='Unknown',PitPaymentTreatment='Regular' WHERE Code='EARN-001'")
    failed(dec,'candidate',zpid)
    sql("UPDATE PayrollComponents SET PitIncomeTreatment='Included',PitPaymentTreatment='Regular' WHERE Code='EARN-001'")
    dd2,dr2,ds2=parity(dec)
    check(dr2['employeePayrollPitResultId']!=dr['employeePayrollPitResultId'] and ds2['calculation']['isFinalScheduledPayment'],'Successful editable regeneration coherently replaces zero/PIT result; final ordinal')
    check(dr2['currentWithholding']==1494.25,'Final N=6 allocation residual recognizes prior exact Paid withholding and actual employee SSO once')
    # Manual same component update preserves source ownership checks even on zero ledgers.
    api('POST',f"employee-payrolls/{novpid}/cancel",{'reason':PREFIX+'Test cancellation'},204)
    check(generate(nov,True)['status']=='Skipped','Cancelled payroll protected')
    check(preview(dec)['pit']['snapshot']['calculation']['priorRecognizedWithholding']==299.75,'Cancelled prior result excluded')
    # Empty Draft never verifies, invalid input structures rejected without fixture insertion.
    empty=schedule([],2027,False);api('POST',f"employees/{EMP}/pit-payment-schedules/{empty['employeePitPaymentScheduleId']}/verify",{},400)
    check(True,'Zero-entry Draft verification rejected')
    for bad in [[{'paymentOrdinal':1,'plannedPayDate':'2028-01-31'},{'paymentOrdinal':2,'plannedPayDate':'2028-01-31'}], [{'paymentOrdinal':1,'plannedPayDate':'2028-01-31'},{'paymentOrdinal':3,'plannedPayDate':'2028-02-29'}], [{'paymentOrdinal':1,'plannedPayDate':'2028-02-29'},{'paymentOrdinal':2,'plannedPayDate':'2028-01-31'}], [{'paymentOrdinal':1,'plannedPayDate':'2029-01-31'}]]:
        api('POST',f'employees/{EMP}/pit-payment-schedules',{'taxYear':2028,'evidence':PREFIX,'entries':bad},400)
        check(True,'Invalid schedule dates/ordinals/year rejected')
    sw=json.loads(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json').read())
    check('/api/employees/{employeeId}/pit-payment-schedules' in sw['paths'] and '/api/employee-payrolls/{payrollId}/pit-result' in sw['paths'],'Swagger schedule and focused historical result routes')
    check('sourceType' not in sw['components']['schemas']['EmployeePayrollLineRequest']['properties'],'Swagger manual provenance remains server-controlled')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        for c in saved_components:restore_component(c)
        cleanup();final=snapshot();check(final==baseline,'Exact contents and timestamps of all 57 Development tables restored')
        check(rows('SELECT IsActive FROM Employees WHERE EmployeeId='+ident(EMP))[0]['IsActive']==False,'TEST-EMP-001 remains unchanged/inactive')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':results,'error':error,'finalCounts':{t:len(v) for t,v in locals().get('final',{}).items()}},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} D6E live assertions; exact baseline restored.',flush=True)
