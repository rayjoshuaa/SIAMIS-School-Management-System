"""Local Development D6D classification/preview checks. All classifications are synthetic fixtures."""
import pathlib, sys
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
PRE=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6d-before-migration.json'
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6d-live-results.json'
if '--capture-before' in sys.argv:
    PRE.write_text(json.dumps(snapshot(),sort_keys=True),encoding='utf-8')
    print('Read-only pre-migration baseline captured.');raise SystemExit(0)
baseline=snapshot(); error=None; PREFIX='D6D-VERIFY-'
if '--verify-final' in sys.argv:
    before=json.loads(PRE.read_text(encoding='utf-8'))
    expected={t:sorted(json.dumps({**json.loads(r),**({'PitPaymentTreatment':'Unknown'} if t=='PayrollComponents' else {})},sort_keys=True) for r in v) for t,v in before.items()}
    check(baseline==expected,'final exact baseline including original timestamps, apart from approved Unknown column')
    check(rows('SELECT IsActive FROM Employees WHERE EmployeeId='+ident(EMP))[0]['IsActive']==False,'TEST-EMP-001 remains inactive')
    print(json.dumps({'finalCounts':{t:len(v) for t,v in baseline.items()},'migration':rows('SELECT TOP (1) MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC')},indent=2));raise SystemExit(0)
try:
    before=json.loads(PRE.read_text(encoding='utf-8'))
    expected={t:sorted(json.dumps({**json.loads(r),**({'PitPaymentTreatment':'Unknown'} if t=='PayrollComponents' else {})},sort_keys=True) for r in v) for t,v in before.items()}
    check(baseline==expected,'migration changes only approved Unknown component column; all other rows/timestamps unchanged')
    check(len(baseline)==51 and len(baseline['Employees'])==1 and len(baseline['PayrollComponents'])==17,'exact expected baseline')
    constraints=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name IN ('CK_PayrollComponents_PitPaymentTreatment','CK_EmployeePayrollLines_PitPaymentTreatmentSnapshot')")
    check(len(constraints)==2 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in constraints),'two trusted enabled SQL checks')
    for treatment in [None,'Unknown','Regular','Special']:
        b={'code':PREFIX+str(uuid.uuid4()),'name':PREFIX+'Synthetic','componentType':'Earning','calculationMethod':'FixedAmount','isTaxable':True}
        if treatment is not None:b['pitPaymentTreatment']=treatment
        c=api('POST','payroll-components',b,201);fixtures['components'].append(c['payrollComponentId'])
        check(c['pitPaymentTreatment']==(treatment or 'Unknown'),'API create payment classification '+str(treatment))
        check(rows('SELECT PitPaymentTreatment FROM PayrollComponents WHERE Id='+ident(c['payrollComponentId']))[0]['PitPaymentTreatment']==(treatment or 'Unknown'),'SQL classification persistence '+str(treatment))
        api('PUT','payroll-components/'+c['payrollComponentId'],{**b,'pitPaymentTreatment':'Regular'})
        kept=api('PUT','payroll-components/'+c['payrollComponentId'],{k:v for k,v in b.items() if k!='pitPaymentTreatment'})
        check(kept['pitPaymentTreatment']=='Regular','omitted update preserves classification')
        api('POST','payroll-components',{**b,'code':PREFIX+str(uuid.uuid4()),'pitPaymentTreatment':'invalid'},400)
        check(True,'invalid treatment rejected')
    # Generate from existing employment context without changing the employee core record.
    opt=api('POST','statutory-schemes',{'code':'TH-SSO-33','name':PREFIX+'Synthetic opt-out','jurisdiction':'TH','schemeType':'SocialSecurity'},201)
    fixtures['schemes'].append(opt['statutorySchemeId'])
    enrollment=api('POST',f'employees/{EMP}/statutory-enrollments',{'statutorySchemeId':opt['statutorySchemeId'],'effectiveFrom':'2026-09-01','effectiveTo':'2026-12-31','applicability':'NotApplicable'},201)
    fixtures['enrollments'].append(enrollment['employeeStatutoryEnrollmentId'])
    p=period(10)
    existing=rows("SELECT * FROM PayrollComponents WHERE Code='EARN-001'")[0]
    saved_component=existing.copy()
    sql('UPDATE PayrollComponents SET PitPaymentTreatment=\'Regular\',PitIncomeTreatment=\'Included\' WHERE Id='+ident(existing['Id']))
    paytype=rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id']
    comp=api('POST',f'employees/{EMP}/compensations',{'effectiveFrom':'2026-09-01','basicSalary':30000,'payTypeId':paytype,'currency':'THB','isCurrent':True,'remarks':PREFIX+'Synthetic'},201)
    fixtures['compensations'].append(comp['compensationId'])
    assignment=api('POST',f'employees/{EMP}/payroll-component-assignments',{'payrollComponentId':c['payrollComponentId'],'amount':1000,'effectiveFrom':'2026-09-01','remarks':PREFIX},201)
    fixtures['assignments'].append(assignment['employeePayrollComponentAssignmentId'])
    r=rule(c,'Supplement',300)
    pv=preview(p);g=generate(p);check(g['status']=='Generated','generation remains functional: '+str(g));pid=g['payrollId'];stored=detail(pid)
    selected=[l for l in stored['lines'] if l['sourceType'] in ('BasicSalary','Assignment','PayrollRule')]
    check(len(selected)==3 and all(l['pitPaymentTreatmentSnapshot']=='Regular' for l in selected),'three generated sources persist Regular snapshots')
    check(all(l['sourceId'] is None if l['sourceType']=='BasicSalary' else l['sourceId'] is not None for l in selected),'generated provenance remains correct')
    check(all(l['pitPaymentTreatmentSnapshot']=='Regular' for l in pv['lines']) and totals(pv)==totals(stored['payroll']),'Preview/Generation snapshot/amount parity')
    c=api('PUT','payroll-components/'+c['payrollComponentId'],{**b,'pitPaymentTreatment':'Special'})
    check(detail(pid)==stored,'live payment classification changes preserve complete historical header/lines/snapshots')
    check(generate(p,True)['status']=='Generated','deliberate regeneration works')
    pid=generate(p)['payrollId'];regenerated=detail(pid)
    check(all(l['pitPaymentTreatmentSnapshot']=='Special' for l in regenerated['lines'] if l['payrollComponentId']==c['payrollComponentId']),'regeneration snapshots current payment classification')
    check(totals(regenerated['payroll'])==totals(stored['payroll']),'payment classification does not change any payroll total')
    manual=api('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':c['payrollComponentId'],'amount':10,'remarks':PREFIX+'manual'},201)
    # The manual endpoint returns its line DTO.
    check(manual['sourceType']=='Manual' and manual['pitPaymentTreatmentSnapshot']=='Special','manual server-owned payment snapshot')
    c=api('PUT','payroll-components/'+c['payrollComponentId'],{**b,'pitPaymentTreatment':'Regular'})
    edited=api('PUT',f"employee-payrolls/{pid}/lines/{manual['employeePayrollLineId']}",{'payrollComponentId':c['payrollComponentId'],'amount':11,'remarks':PREFIX+'edit'})
    check(edited['pitPaymentTreatmentSnapshot']=='Special','manual amount update preserves snapshot')
    current_before=snapshot()
    advisory=api('GET',f'employees/{EMP}/payroll-periods/{p}/pit-preview')
    check(advisory['status']=='RequiresReview' and any('schedule' in z.lower() for z in advisory['reasons']),'advisory cannot invent annual schedule')
    check(snapshot()==current_before,'advisory performs zero writes across all application tables')
    swagger=json.loads(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json').read())
    check('/api/employees/{employeeId}/payroll-periods/{payrollPeriodId}/pit-preview' in swagger['paths'],'Swagger documents PIT advisory endpoint')
    check('pitPaymentTreatment' in swagger['components']['schemas']['PayrollComponentDto']['properties'],'Swagger exposes payment classification')
    check(not rows("SELECT name FROM sys.tables WHERE name='EmployeePayrollPitResult'"),'no PIT result table')
    check(not rows("SELECT EmployeePayrollLineId FROM EmployeePayrollLines WHERE ComponentCode='DEDUCT-002'"),'no PIT deduction line')
    for table,col,key,value in [('PayrollComponents','PitPaymentTreatment','Id',c['payrollComponentId']),('EmployeePayrollLines','PitPaymentTreatmentSnapshot','EmployeePayrollLineId',manual['employeePayrollLineId'])]:
        rejected=sql(f"BEGIN TRANSACTION; BEGIN TRY UPDATE [{table}] SET [{col}]='invalid' WHERE [{key}]={ident(value)}; ROLLBACK; THROW 51000,'Invalid classification accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER()<>547 THROW; SELECT 'Rejected547' AS Result; END CATCH;")
        check('Rejected547' in rejected,'SQL rejects invalid '+col)
except Exception as exc:
    error=str(exc);traceback.print_exc()
finally:
    try:
        if 'saved_component' in locals():
            sql('UPDATE PayrollComponents SET PitPaymentTreatment=\'Unknown\',PitIncomeTreatment=\'Unknown\' WHERE Id='+ident(saved_component['Id']))
        cleanup()
        final=snapshot();check(final==baseline,'exact all-table rows/timestamps restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':results,'error':error,'finalCounts':{t:len(v) for t,v in locals().get('final',{}).items()}},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} D6D live assertions; exact baseline restored.',flush=True)
