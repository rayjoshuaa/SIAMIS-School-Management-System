"""Additional D6E boundary/concurrency checks using synthetic local fixtures only."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d6e_live.py').read_text().split('\ntry:\n')[0])
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6e-boundaries-results.json'
PREFIX='D6E-BOUNDARY-'
saved_employee=rows('SELECT * FROM Employees WHERE EmployeeId='+ident(EMP))[0]
saved_employee.setdefault('NationalityId',None)
def insert_row(table,row):sql('INSERT INTO ['+table+'] ('+','.join('['+k+']' for k in row)+') VALUES ('+','.join(literal(v) for v in row.values())+')')
def restore_row(table,key,row):sql('UPDATE ['+table+'] SET '+','.join('['+k+']='+literal(v) for k,v in row.items() if k!=key)+' WHERE ['+key+']='+literal(row[key]))
def opening_body(income=30000,withheld=300):return {'state':'VerifiedAmount','currency':'THB','asOfDate':'2026-10-31','priorTaxableEmploymentIncome':income,'priorTaxWithheld':withheld,'priorSocialSecurityContribution':0,'openingBalanceScope':'CurrentEmployer','completenessAttested':True,'remarks':PREFIX+'Reviewed complete current-employer cutoff'}
def revision():
    d=api('POST',f'employees/{EMP}/tax-declarations',{'taxYear':2026,'remarks':PREFIX+'Correction'},201)['declaration']['employeeTaxDeclarationId'];fixtures['declarations'].append(d)
    api('PUT',f'employees/{EMP}/tax-declarations/{d}/treatment',{'residencyStatus':'Resident','employmentTaxTreatment':'StandardSection40_1'})
    api('PUT',f'employees/{EMP}/tax-declarations/{d}/opening-balance',opening_body())
    return d

def simultaneous(actions):
    with ThreadPoolExecutor(len(actions)) as pool:return list(pool.map(lambda a:a(),actions))
try:
    check(len(baseline)==55 and not baseline['EmployeePayrollPitResults'],'Clean D6E boundary baseline')
    sso=scheme('TH-SSO-33','SocialSecurity');enroll(sso,'NotApplicable')
    thpit=scheme('TH-PIT','PersonalIncomeTax');en=enroll(thpit)
    paytype=rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id']
    c=api('POST',f'employees/{EMP}/compensations',{'payTypeId':paytype,'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-01','isCurrent':True,'remarks':PREFIX},201);fixtures['compensations'].append(c['compensationId'])
    p=period(10);nov=period(11);dec=period(12);sep=period(9)
    d=declaration();sc=schedule(entries())
    saved_enrollment=rows('SELECT * FROM EmployeeStatutoryEnrollments WHERE EmployeeStatutoryEnrollmentId='+ident(en['employeeStatutoryEnrollmentId']))[0]
    sql('DELETE EmployeeStatutoryEnrollments WHERE EmployeeStatutoryEnrollmentId='+ident(en['employeeStatutoryEnrollmentId']))
    failed(p,'Unknown')
    insert_row('EmployeeStatutoryEnrollments',saved_enrollment)
    check(True,'Declaration and schedule presence alone cannot infer PIT applicability')
    pol=api('POST','statutory-policy-versions',{'statutorySchemeId':thpit,'version':PREFIX+'Synthetic','effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','currency':'THB','officialReference':PREFIX+'Software fixture only','calculationMethodVersion':'PIT-TH-V1'},201)['statutoryPolicyVersionId']
    body={'taxYear':2026,'employmentExpenseDeductionRate':0,'employmentExpenseDeductionCap':1,'personalAllowanceAmount':0,'spouseAllowanceAmount':0,'childAllowanceAmount':0,'additionalChildAllowanceAmount':0,'parentAllowanceAmount':0,'adoptedChildCombinedCountLimit':3,'maximumEligibleParentCount':4,'withholdingMethodIdentifier':'Synthetic monthly','brackets':[{'lowerBoundInclusive':0,'upperBoundExclusive':None,'rate':1,'sortOrder':1}]}
    api('PUT',f'statutory-policy-versions/{pol}/personal-income-tax',body)
    failed(p,'Published')
    api('POST',f'statutory-policy-versions/{pol}/publish',{})
    sql("UPDATE PayrollComponents SET PitIncomeTreatment='Included',PitPaymentTreatment='Regular' WHERE Code='EARN-001'")
    # PayDate, not period EndDate, determines enrollment.
    sql("UPDATE EmployeeStatutoryEnrollments SET EffectiveFrom='2026-10-31' WHERE EmployeeStatutoryEnrollmentId="+ident(en['employeeStatutoryEnrollmentId']))
    sql("UPDATE PayrollPeriods SET PayDate='2026-10-30' WHERE PayrollPeriodId="+ident(p));failed(p,'Unknown')
    sql("UPDATE EmployeeStatutoryEnrollments SET EffectiveFrom='2026-01-01' WHERE EmployeeStatutoryEnrollmentId="+ident(en['employeeStatutoryEnrollmentId']))
    failed(p,'matching')
    sql("UPDATE PayrollPeriods SET PayDate='2026-10-31' WHERE PayrollPeriodId="+ident(p))
    failed(sep,'partial')
    # No nationality inference. These fixture-only core changes are restored byte-for-byte.
    thai=rows("SELECT Id FROM Nationalities WHERE Name='Thai'")[0]['Id'];foreign=rows("SELECT Id FROM Nationalities WHERE Name='Filipino'")[0]['Id']
    sql('UPDATE Employees SET NationalityId='+ident(thai)+' WHERE EmployeeId='+ident(EMP))
    sql("UPDATE EmployeeStatutoryEnrollments SET Applicability='NotApplicable' WHERE EmployeeStatutoryEnrollmentId="+ident(en['employeeStatutoryEnrollmentId']))
    check(preview(p)['pit']['status']=='NotApplicable','Thai nationality does not override explicit NotApplicable')
    sql('UPDATE Employees SET NationalityId='+ident(foreign)+' WHERE EmployeeId='+ident(EMP))
    sql("UPDATE EmployeeStatutoryEnrollments SET Applicability='Applicable' WHERE EmployeeStatutoryEnrollmentId="+ident(en['employeeStatutoryEnrollmentId']))
    octdata,octpit,octsnap=parity(p);pid=octdata['payroll']['employeePayrollId']
    check(octpit['currentWithholding']==300,'Foreign employee explicit Applicable uses supported PIT treatment')
    restore_row('Employees','EmployeeId',saved_employee)
    # PIT consumes the final resolved rule/assignment set without changing generic formulas.
    extras=[]
    for code,category,income,payment in [('Included','Earning','Included','Regular'),('Excluded','Earning','Excluded','Unknown'),('Deduction','Deduction','Unknown','Unknown')]:
        x=api('POST','payroll-components',{'code':PREFIX+code,'name':PREFIX+code,'componentType':category,'calculationMethod':'FixedAmount','isTaxable':category=='Earning','pitIncomeTreatment':income,'pitPaymentTreatment':payment},201)
        fixtures['components'].append(x['payrollComponentId']);extras.append(x)
    assignments=[]
    for x,amount in zip(extras,[1000,200,100]):
        a=api('POST',f'employees/{EMP}/payroll-component-assignments',{'payrollComponentId':x['payrollComponentId'],'amount':amount,'effectiveFrom':'2026-09-01','remarks':PREFIX},201)
        fixtures['assignments'].append(a['employeePayrollComponentAssignmentId']);assignments.append(a['employeePayrollComponentAssignmentId'])
    er=rule(extras[0],amount=300)
    data,pr,ps=parity(p)
    check(data['payroll']['grossPay']==31500 and pr['currentWithholding']==313 and data['payroll']['totalDeductions']==413,'Applicable PIT Earning Supplement consumes final Included 31300, excludes 200')
    check({l['sourceType'] for l in ps['calculation']['input']['currentLines']} >= {'BasicSalary','Assignment','PayrollRule'},'PIT snapshot retains BasicSalary/Assignment/PayrollRule final provenance')
    delete_rule(er);er=rule(extras[0],'ReplaceAssignment',500)
    data,pr,ps=parity(p)
    check(data['payroll']['grossPay']==30700 and pr['currentWithholding']==305 and any(l['sourceId']==assignments[1] for l in data['lines']),'Applicable PIT Earning ReplaceAssignment suppresses only matching assignment')
    drule=rule(extras[2],amount=20,stage='Deduction')
    data,pr,ps=parity(p)
    check(pr['currentWithholding']==305 and data['payroll']['totalDeductions']==425,'Applicable PIT Deduction Supplement leaves PIT base unchanged')
    delete_rule(drule);drule=rule(extras[2],'ReplaceAssignment',40,'Deduction')
    data,pr,ps=parity(p)
    check(pr['currentWithholding']==305 and data['payroll']['totalDeductions']==345,'Applicable PIT Deduction ReplaceAssignment preserves PIT and replaces matching deduction')
    conflict=rule(extras[0],'ReplaceAssignment',600)
    failed(nov,'Multiple applicable');failed(p,'Multiple applicable',data['payroll']['employeePayrollId']);delete_rule(conflict)
    conflict=rule(extras[2],'ReplaceAssignment',50,'Deduction')
    failed(nov,'Multiple applicable');failed(p,'Multiple applicable',data['payroll']['employeePayrollId']);delete_rule(conflict)
    delete_rule(er);delete_rule(drule)
    for a in assignments:api('DELETE',f'employees/{EMP}/payroll-component-assignments/{a}',status=204)
    octdata,octpit,octsnap=parity(p);pid=octdata['payroll']['employeePayrollId']
    check(octpit['currentWithholding']==300,'Removing synthetic rules/assignments restores original PIT inputs')
    api('POST',f'employee-payrolls/{pid}/approve',{},204);api('POST',f'employee-payrolls/{pid}/mark-paid',{},204)
    saved_pit=rows('SELECT * FROM EmployeePayrollPitResults WHERE EmployeePayrollId='+ident(pid))[0]
    sql('DELETE EmployeePayrollPitResults WHERE EmployeePayrollId='+ident(pid))
    failed(nov,'reconcile')
    insert_row('EmployeePayrollPitResults',saved_pit)
    check(pit(pid)==octpit,'Missing ledger test restores exact Paid PIT authority')
    pitline=rows("SELECT * FROM EmployeePayrollLines WHERE EmployeePayrollId="+ident(pid)+" AND ComponentCode='DEDUCT-002'")[0]
    sql('UPDATE EmployeePayrollLines SET Amount=999 WHERE EmployeePayrollLineId='+ident(pitline['EmployeePayrollLineId']))
    check(preview(nov)['pit']['snapshot']['calculation']['priorRecognizedWithholding']==300,'Deduction line amount cannot replace exact historical PIT result authority')
    restore_row('EmployeePayrollLines','EmployeePayrollLineId',pitline)
    # A Draft post-cutoff payroll is not recognized, even with manual deduction lines.
    sql("UPDATE PayrollPeriods SET PayDate='2026-11-15' WHERE PayrollPeriodId="+ident(sep))
    draft=api('POST','employee-payrolls',{'employeeId':EMP,'payrollPeriodId':sep,'remarks':PREFIX+'Draft'},201)
    draftpid=draft['payroll']['employeePayrollId'];other=next(x for x in saved_components if x['Code']=='DEDUCT-007')['Id']
    # Draft without earning cannot accept deductions exceeding GrossPay; this rejected request must leave no line.
    api('POST',f'employee-payrolls/{draftpid}/lines',{'payrollComponentId':other,'amount':1,'remarks':PREFIX},400)
    check(preview(dec)['pit']['snapshot']['calculation']['priorRecognizedWithholding']==300,'Draft payroll/manual deduction cannot fabricate history')
    # Declaration selection race uses exactly one verified revision, never Draft state.
    d2=revision()
    race=simultaneous([lambda:generate(nov),lambda:api('POST',f'employees/{EMP}/tax-declarations/{d2}/verify',{})])
    ng=race[0];check(ng['status']=='Generated','Generation racing declaration verification succeeds')
    ns=json.loads(pit(ng['payrollId'])['calculationSnapshotJson'])['calculation']
    used=ns['input']['declaration']['declaration']['employeeTaxDeclarationId'];ob=ns['input']['declaration']['openingBalance']
    check((used==d and ob['asOfDate']=='2025-12-31' and len(ns['input']['history'])==1) or (used==d2 and ob['asOfDate']=='2026-10-31' and not ns['input']['history']),'Generation race retains coherent declaration/opening/history')
    now=preview(dec)['pit']['snapshot']['calculation']
    check(now['priorRecognizedIncome']==30000 and now['priorRecognizedWithholding']==300 and not now['input']['history'],'Opening cutoff includes earlier Paid payroll exactly once')
    check(pit(pid)==octpit,'Declaration correction does not rewrite Paid result')
    # Opening mutation racing verification is serialized by the same employee lock.
    d3=revision()
    def opening_change():
        try:return api('PUT',f'employees/{EMP}/tax-declarations/{d3}/opening-balance',opening_body(40000,400))
        except AssertionError as ex:
            if 'got 409' in str(ex):return None
            raise
    simultaneous([opening_change,lambda:api('POST',f'employees/{EMP}/tax-declarations/{d3}/verify',{})])
    verified=api('GET',f'employees/{EMP}/tax-declarations/{d3}')
    opening=verified['openingBalance'];check((opening['priorTaxableEmploymentIncome'],opening['priorTaxWithheld']) in [(30000,300),(40000,400)],'Opening/verification race yields one complete committed state')
    before=snapshot();api('PUT',f'employees/{EMP}/tax-declarations/{d3}/opening-balance',opening_body(),409)
    check(snapshot()==before,'Verified opening mutation rejected with zero writes')
    # Concurrent editable regeneration remains single-owner/single-line after both successful commits.
    concurrent=simultaneous([lambda:generate(nov,True),lambda:generate(nov,True)])
    check(all(x['status']=='Generated' for x in concurrent),'Concurrent regenerations serialize successfully')
    nrows=rows('SELECT EmployeePayrollId FROM EmployeePayrolls WHERE PayrollPeriodId='+ident(nov));check(len(nrows)==1,'Concurrent regeneration one payroll header')
    npid=nrows[0]['EmployeePayrollId'];nr=pit(npid)
    check(len(rows('SELECT * FROM EmployeePayrollPitResults WHERE PayrollPeriodId='+ident(nov)))==1 and len([l for l in detail(npid)['lines'] if l['componentCode']=='DEDUCT-002'])==1,'Concurrent regeneration one PIT result/deduction')
    # Prior payment transition race: history is consistently either before or after Paid.
    api('POST',f'employee-payrolls/{npid}/approve',{},204)
    race=simultaneous([lambda:generate(dec),lambda:api('POST',f'employee-payrolls/{npid}/mark-paid',{},204)])
    check(race[0]['status']=='Generated','Generation racing prior payment transition succeeds')
    dpit=pit(race[0]['payrollId']);ds=json.loads(dpit['calculationSnapshotJson'])['calculation'];h=ds['input']['history']
    check(len(h) in (0,1) and ds['priorRecognizedWithholding']==opening['priorTaxWithheld']+(nr['currentWithholding'] if h else 0)
          and ds['priorRecognizedIncome']==opening['priorTaxableEmploymentIncome']+(30000 if h else 0),'Paid transition race cannot mix income and withholding recognition')
    ddata,dr,dss=parity(dec)
    check(dss['calculation']['priorRecognizedWithholding']==opening['priorTaxWithheld']+nr['currentWithholding'],'Committed Paid transition recognized exactly once')
    dpid=ddata['payroll']['employeePayrollId']
    # Legacy overlapping enrollment is diagnosed rather than silently selecting an interval.
    clone=rows('SELECT * FROM EmployeeStatutoryEnrollments WHERE EmployeeStatutoryEnrollmentId='+ident(en['employeeStatutoryEnrollmentId']))[0]
    clone['EmployeeStatutoryEnrollmentId']=str(uuid.uuid4());insert_row('EmployeeStatutoryEnrollments',clone);fixtures['enrollments'].append(clone['EmployeeStatutoryEnrollmentId'])
    failed(dec,'Ambiguous',dpid)
    sql('DELETE EmployeeStatutoryEnrollments WHERE EmployeeStatutoryEnrollmentId='+ident(clone['EmployeeStatutoryEnrollmentId']))
    api('PUT',f'statutory-policy-versions/{pol}/personal-income-tax',body,409)
    bracket=rows('SELECT * FROM PitTaxBrackets WHERE StatutoryPolicyVersionId='+ident(pol))[0]
    sql('UPDATE PitTaxBrackets SET Rate=2 WHERE StatutoryPolicyVersionId='+ident(pol))
    check(pit(pid)==octpit and pit(npid)==nr,'Live fixture policy changes never mutate Paid PIT results')
    check(preview(dec)['pit']['snapshot']['calculation']['priorRecognizedWithholding']==opening['priorTaxWithheld']+nr['currentWithholding'],'Historical PIT not recalculated using changed policy rates')
    restore_row('PitTaxBrackets','PitTaxBracketId',bracket)
    sql('UPDATE PitTaxBrackets SET Rate=100 WHERE StatutoryPolicyVersionId='+ident(pol))
    failed(dec,'GrossPay',dpid)
    restore_row('PitTaxBrackets','PitTaxBracketId',bracket)
    # Manual guards when no SSO result exists (including corrupt legacy manual earnings).
    m=api('POST',f'employee-payrolls/{dpid}/lines',{'payrollComponentId':other,'amount':1,'remarks':PREFIX},201)
    manual=rows('SELECT * FROM EmployeePayrollLines WHERE EmployeePayrollLineId='+ident(m['employeePayrollLineId']))[0]
    basic=next(x for x in saved_components if x['Code']=='EARN-001')['Id']
    sql("UPDATE EmployeePayrollLines SET ComponentType='Earning',PayrollComponentId="+ident(basic)+",ComponentCode='EARN-001' WHERE EmployeePayrollLineId="+ident(m['employeePayrollLineId']))
    api('PUT',f"employee-payrolls/{dpid}/lines/{m['employeePayrollLineId']}",{'payrollComponentId':other,'amount':2,'remarks':PREFIX},409)
    api('DELETE',f"employee-payrolls/{dpid}/lines/{m['employeePayrollLineId']}",status=409)
    check(pit(dpid)==dr,'Rejected legacy manual earning update/delete preserves PIT ledger')
    restore_row('EmployeePayrollLines','EmployeePayrollLineId',manual)
    api('DELETE',f"employee-payrolls/{dpid}/lines/{m['employeePayrollLineId']}",status=204)
    # A zero PIT result alone must guard earnings; temporarily exclude live income and regenerate.
    sql("UPDATE PayrollComponents SET PitIncomeTreatment='Excluded' WHERE Code='EARN-001'")
    zg=generate(dec,True);check(zg['status']=='Generated' and pit(zg['payrollId'])['currentWithholding']==0,'Zero PIT ledger with SSO NotApplicable')
    api('POST',f"employee-payrolls/{zg['payrollId']}/lines",{'payrollComponentId':basic,'amount':1,'remarks':PREFIX},409)
    check(not rows('SELECT * FROM EmployeePayrollStatutoryResults WHERE EmployeePayrollId='+ident(zg['payrollId'])),'Zero PIT guard does not rely on an SSO result')
    # Draft editing is available, Verified editing and year reassignment are not.
    empty=schedule([],2027,False);eid=empty['employeePitPaymentScheduleId']
    api('PUT',f'employees/{EMP}/pit-payment-schedules/{eid}',{'taxYear':2027,'evidence':PREFIX+'Reviewed','entries':entries(1,12,2027)})
    api('PUT',f'employees/{EMP}/pit-payment-schedules/{eid}',{'taxYear':2027,'evidence':PREFIX+'Corrected Draft','entries':entries(1,12,2027)})
    api('POST',f'employees/{EMP}/pit-payment-schedules/{eid}/verify',{})
    api('PUT',f'employees/{EMP}/pit-payment-schedules/{eid}',{'taxYear':2027,'evidence':PREFIX,'entries':[]},409)
    check(True,'Draft can be corrected; Verified schedule update prohibited')
    empty=schedule([],2028,False);eid=empty['employeePitPaymentScheduleId']
    api('PUT',f'employees/{EMP}/pit-payment-schedules/{eid}',{'taxYear':2029,'evidence':PREFIX,'entries':[]},400)
    check(True,'Draft year cannot be reassigned')
    api('GET',f'employees/{EMP}/pit-payment-schedules/current/2029')
    api('GET',f'employees/{uuid.uuid4()}/pit-payment-schedules/{sc["employeePitPaymentScheduleId"]}',status=404)
    check(True,'Schedule current absence and ownership checks')
    # Parent period lifecycle remains authoritative; stored PIT results cannot be lost through these operations.
    api('POST',f'payroll-periods/{nov}/start-processing',{},204);api('POST',f'payroll-periods/{nov}/close',{},204)
    api('POST',f'payroll-periods/{nov}/generate',{'employeeIds':[EMP],'forceRegenerate':True},409)
    api('POST',f'payroll-periods/{nov}/preview',{'employeeIds':[EMP]},409)
    check(pit(npid)==nr,'Closed parent protects exact Paid PIT result')
    zpid=zg['payrollId'];zr=pit(zpid)
    api('POST',f'employee-payrolls/{zpid}/cancel',{'reason':PREFIX},204)
    api('POST',f'payroll-periods/{dec}/cancel',{'reason':PREFIX},204)
    api('POST',f'payroll-periods/{dec}/generate',{'employeeIds':[EMP],'forceRegenerate':True},409)
    api('POST',f'payroll-periods/{dec}/preview',{'employeeIds':[EMP]},409)
    check(pit(zpid)==zr,'Cancelled parent preserves zero PIT result and blocks calculation')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        restore_row('Employees','EmployeeId',saved_employee)
        for c in saved_components:restore_component(c)
        cleanup();final=snapshot();check(final==baseline,'Exact 55-table boundary baseline restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':results,'error':error,'finalCounts':{t:len(v) for t,v in locals().get('final',{}).items()}},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} D6E boundary assertions; exact baseline restored.',flush=True)
