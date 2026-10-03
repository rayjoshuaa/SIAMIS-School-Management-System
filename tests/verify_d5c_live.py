"""Local Development only. Synthetic policies are test inputs, never legal classifications/values.

Reuses the live regression helpers. Fixture-only SQL exercises corrupt legacy inputs and
historic scenarios unavailable through immutable public contracts. Baseline rows are
restored, including timestamps, and every application table is compared after cleanup.
"""
import pathlib
helpers = pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text()
exec(helpers.split('baseline=snapshot()')[0])
# Include nulls so restoring a row can clear nullable values changed by a fixture.
def rows(query): return json.loads(''.join(sql(query+' FOR JSON PATH, INCLUDE_NULL_VALUES').splitlines()) or '[]')

OUT = ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d5c-live-results.json'
PREFIX = 'D5C-VERIFY-'
baseline = snapshot()
(ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d5c-baseline.json').write_text(json.dumps(baseline,indent=2))
error = None
original_components = rows('SELECT * FROM PayrollComponents')
original_employment = rows('SELECT * FROM EmploymentRecords WHERE EmployeeId='+ident(EMP))
extra_employment = []

def literal(value):
    if value is None: return 'NULL'
    if isinstance(value, bool): return '1' if value else '0'
    if isinstance(value, (int,float)): return str(value)
    return "N'"+str(value).replace("'","''")+"'"

def restore_row(table,key,row):
    sql('UPDATE ['+table+'] SET '+','.join('['+k+']='+literal(v) for k,v in row.items() if k != key)+' WHERE ['+key+']='+literal(row[key]))

def assert_failure(p,fragment,previous=None):
    before = detail(previous) if previous else None
    pv = preview(p); gen = generate(p, bool(previous))
    check(pv['status']=='Failed' and fragment.lower() in pv['message'].lower(), 'Preview failure: '+fragment)
    check(gen['status']=='Failed' and fragment.lower() in gen['message'].lower(), 'Generation failure: '+fragment)
    if previous:
        check(detail(previous)==before, 'Failed regeneration preserves entire statutory/header/line snapshot: '+fragment)
    else:
        check(not rows('SELECT * FROM EmployeePayrolls WHERE PayrollPeriodId='+ident(p)), 'Failed NEW generation no header: '+fragment)

def parity(p, force=True):
    before=snapshot()
    pv=preview(p)
    check(snapshot()==before,'Preview is read-only')
    gen=generate(p,force)
    check(pv['status']=='Calculated' and gen['status']=='Generated','Preview/Generation succeed')
    d=detail(gen['payrollId']); s=d['statutoryResults'][0]
    check(totals(pv)==totals(d['payroll']), 'Preview/Generation payroll totals match')
    check(json.loads(pv['socialSecurity']['calculationSnapshotJson'])==json.loads(s['calculationSnapshotJson']), 'Preview/Generation statutory explanation matches')
    stat=[l for l in d['lines'] if l['sourceType']=='Statutory']
    check(len(stat)==(1 if s['employeeAmount']>0 else 0),'Only positive employee SSO creates a line; employer never creates one')
    if stat:
        check(stat[0]['sourceId']==s['employeePayrollStatutoryResultId'] and stat[0]['amount']==s['employeeAmount'] and stat[0]['componentCode']=='DEDUCT-001','Truthful Statutory result source and canonical deduction amount')
    check(s['employeeAmount']==s['employerAmount'],'Employer equals rounded employee')
    check(d['payroll']['totalDeductions']==sum(l['amount'] for l in d['lines'] if l['componentType']=='Deduction') and d['payroll']['netPay']==d['payroll']['grossPay']-d['payroll']['totalDeductions'],'Employee deduction counted exactly once; employer isolated')
    return d,s

try:
    # This suite deliberately moves a fixture PayDate into 2027 while SSO still resolves on EndDate.
    pit_opt_out('2027-12-31')
    check(len(baseline)==55 and len(baseline['Employees'])==1 and len(baseline['PayrollComponents'])==17,'55-table clean baseline')
    migration=rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '%_AddSection33PayrollResults'")
    check(len(migration)==1,'D5C migration applied once')
    schema=rows("SELECT OBJECT_NAME(object_id) AS [Table],name,TYPE_NAME(user_type_id) AS TypeName,precision,scale,is_nullable FROM sys.columns WHERE OBJECT_NAME(object_id) IN ('EmployeePayrollStatutoryResults','EmployeePayrollSocialSecurityResults')")
    check(len(schema)==21 and next(x for x in schema if x['name']=='RawEmployeeAmount')['scale']==10,'Actual structured schema and unrounded raw precision')
    fks=rows("SELECT name,delete_referential_action_desc FROM sys.foreign_keys WHERE OBJECT_NAME(parent_object_id) IN ('EmployeePayrollStatutoryResults','EmployeePayrollSocialSecurityResults')")
    check(len(fks)==5 and all(x['delete_referential_action_desc']=='NO_ACTION' for x in fks),'Five statutory result FKs NoAction')
    constraints=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE OBJECT_NAME(parent_object_id) IN ('EmployeePayrollStatutoryResults','EmployeePayrollSocialSecurityResults')")
    check(len(constraints)==6 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in constraints),'Six result check constraints enabled and trusted')
    check(not rows("SELECT constraint_object_id FROM sys.foreign_key_columns WHERE COL_NAME(parent_object_id,parent_column_id)='SourceId'"),'SourceId remains snapshot-only without FK')
    check(all(c['SsoWageTreatment']=='Unknown' for c in original_components),'All 17 existing components still Unknown')
    basic=next(c for c in original_components if c['Name']=='Basic Salary'); canonical=next(c for c in original_components if c['Code']=='DEDUCT-001')
    # Temporarily classify existing Basic Salary only as a synthetic fixture, restore its full row.
    sql('UPDATE PayrollComponents SET SsoWageTreatment=\'Excluded\' WHERE Id='+ident(basic['Id']))
    paytype=rows("SELECT Id FROM PayTypes WHERE Code='PAY-001'")[0]['Id']
    comp=api('POST',f'employees/{EMP}/compensations',{'payTypeId':paytype,'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-01','isCurrent':True,'remarks':PREFIX+'Synthetic'},201); fixtures['compensations'].append(comp['compensationId'])
    earning=component('Included','Included'); excluded=component('Excluded','Excluded'); deduction=component('Deduction','Unknown','Deduction',False)
    ass=api('POST',f'employees/{EMP}/payroll-component-assignments',{'payrollComponentId':earning['payrollComponentId'],'amount':150,'effectiveFrom':'2026-09-01','remarks':PREFIX},201); aid=ass['employeePayrollComponentAssignmentId'];fixtures['assignments'].append(aid)
    octp=period(10); novp=period(11); sepp=period(9)
    assert_failure(octp,'TH-SSO-33')
    scheme=api('POST','statutory-schemes',{'code':'TH-SSO-33','name':PREFIX+'Synthetic','jurisdiction':'TH','schemeType':'SocialSecurity'},201); sid=scheme['statutorySchemeId'];fixtures['schemes'].append(sid)
    assert_failure(octp,'enrollment')
    en=api('POST',f'employees/{EMP}/statutory-enrollments',{'statutorySchemeId':sid,'effectiveFrom':'2026-09-01','effectiveTo':'2026-12-31','applicability':'Applicable','remarks':PREFIX},201); enid=en['employeeStatutoryEnrollmentId'];fixtures['enrollments'].append(enid)
    assert_failure(octp,'policy')
    pol=api('POST','statutory-policy-versions',{'statutorySchemeId':sid,'version':PREFIX+'Synthetic','effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','currency':'THB','calculationMethodVersion':'SSO-TH-V1','officialReference':'Synthetic verification inputs only; not legal values'},201); polid=pol['statutoryPolicyVersionId']
    params={'employeeContributionRate':10,'employerContributionRate':11,'minimumContributionBase':100,'maximumContributionBase':200,'insuredPersonClassification':'33'}
    api('PUT',f'statutory-policy-versions/{polid}/social-security',params)
    api('POST',f'statutory-policy-versions/{polid}/publish',{},400); check(True,'Unequal V1 rates publication HTTP400')
    assert_failure(octp,'policy')
    params['employerContributionRate']=10; api('PUT',f'statutory-policy-versions/{polid}/social-security',params)
    api('POST',f'statutory-policy-versions/{polid}/publish',{});check(True,'Equal V1 rates publish')
    api('PUT',f'statutory-policy-versions/{polid}/social-security',params,409);check(True,'Published policy remains immutable')
    future=api('POST','statutory-policy-versions',{'statutorySchemeId':sid,'version':PREFIX+'Future','effectiveFrom':'2027-01-01','currency':'THB','calculationMethodVersion':'SSO-TH-V2','officialReference':'Synthetic only'},201)
    api('PUT',f"statutory-policy-versions/{future['statutoryPolicyVersionId']}/social-security",{**params,'employerContributionRate':11})
    future_error=api('POST',f"statutory-policy-versions/{future['statutoryPolicyVersionId']}/publish",{},400)
    check('supported D4A' in json.dumps(future_error) and 'requires equal' not in json.dumps(future_error),'Future identifier rejected by existing method contract, not blanket equality')

    d,s=parity(octp);pid=d['payroll']['employeePayrollId']
    check((s['contributionWage'],s['contributionBase'],s['employeeAmount'])==(150,150,15),'Synthetic contribution from actual final Included wage')
    check((d['payroll']['basicSalary'],d['payroll']['grossPay'],d['payroll']['taxableEarnings'])==(30000,30150,(30000 if basic['IsTaxable'] else 0)+150),'BasicSalary GrossPay TaxableEarnings unchanged by statutory stage')
    # EndDate, not PayDate: move the fixture PayDate to a month without policy/enrollment.
    sql('UPDATE PayrollPeriods SET PayDate=\'2027-02-01\' WHERE PayrollPeriodId='+ident(octp))
    check(preview(octp)['socialSecurity']['governingDate']=='2026-10-31' and s['contributionMonth']=='2026-10','EndDate governs despite a different PayDate')
    history=detail(pid)
    sql('UPDATE SocialSecurityPolicyConfigurations SET EmployerContributionRate=11 WHERE StatutoryPolicyVersionId='+ident(polid))
    assert_failure(novp,'equal');assert_failure(octp,'equal',pid)
    check(detail(pid)==history,'Corrupt Published policy rejected without rewriting historical result')
    sql('UPDATE SocialSecurityPolicyConfigurations SET EmployerContributionRate=10 WHERE StatutoryPolicyVersionId='+ident(polid))
    sql("UPDATE StatutoryPolicyVersions SET CalculationMethodVersion='SSO-TH-V2' WHERE StatutoryPolicyVersionId="+ident(polid))
    assert_failure(novp,'unsupported')
    sql("UPDATE StatutoryPolicyVersions SET CalculationMethodVersion='SSO-TH-V1' WHERE StatutoryPolicyVersionId="+ident(polid))
    sql("UPDATE StatutoryPolicyVersions SET EffectiveTo='2026-09-30' WHERE StatutoryPolicyVersionId="+ident(polid))
    assert_failure(novp,'policy')
    sql("UPDATE StatutoryPolicyVersions SET EffectiveTo='2026-12-31' WHERE StatutoryPolicyVersionId="+ident(polid))
    sql("UPDATE EmployeeCompensations SET Currency='USD' WHERE EmployeeCompensationId="+ident(comp['compensationId']))
    assert_failure(novp,'THB')
    sql("UPDATE EmployeeCompensations SET Currency='THB' WHERE EmployeeCompensationId="+ident(comp['compensationId']))
    sql("UPDATE PayrollComponents SET SsoWageTreatment='Unknown' WHERE Id="+ident(earning['payrollComponentId']))
    assert_failure(novp,'Unknown');assert_failure(octp,'Unknown',pid)
    check(detail(pid)==history,'Live classification edit preserves historical monetary and classification snapshot')
    sql("UPDATE PayrollComponents SET SsoWageTreatment='Included' WHERE Id="+ident(earning['payrollComponentId']))
    for changes,label in [("Code='D5C-MISSING'",'missing'),('IsActive=0','inactive'),("Category='Earning'",'wrong category')]:
        sql('UPDATE PayrollComponents SET '+changes+' WHERE Id='+ident(canonical['Id']))
        assert_failure(novp,'DEDUCT-001');restore_row('PayrollComponents','Id',canonical)
        check(True,'Canonical component '+label+' rejected without repair')
    for wage,basis,employee in [(50,100,10),(100,100,10),(150,150,15),(200,200,20),(250,200,20),(14.9,100,10)]:
        sql('UPDATE EmployeePayrollComponentAssignments SET Amount='+str(wage)+' WHERE EmployeePayrollComponentAssignmentId='+ident(aid))
        check(detail(pid)==history,'Assignment edits do not rewrite history')
        d,s=parity(octp);pid=d['payroll']['employeePayrollId'];history=d
        check((s['contributionWage'],s['contributionBase'],s['employeeAmount'])==(wage,basis,employee),'Live wage/base bounds '+str(wage))
    for wage,rounded in [(14.9,1),(15,2),(15.1,2)]:
        sql('UPDATE SocialSecurityPolicyConfigurations SET MinimumContributionBase=0 WHERE StatutoryPolicyVersionId='+ident(polid))
        sql('UPDATE EmployeePayrollComponentAssignments SET Amount='+str(wage)+' WHERE EmployeePayrollComponentAssignmentId='+ident(aid))
        d,s=parity(octp);pid=d['payroll']['employeePayrollId']
        check(s['rawEmployeeAmount']==wage/10 and s['employeeAmount']==rounded,'Live rounding '+str(wage/10))
    sql('UPDATE SocialSecurityPolicyConfigurations SET MinimumContributionBase=100 WHERE StatutoryPolicyVersionId='+ident(polid))
    sql("UPDATE PayrollComponents SET SsoWageTreatment='Excluded' WHERE Id="+ident(earning['payrollComponentId']))
    d,s=parity(octp);pid=d['payroll']['employeePayrollId']
    check(s['contributionWage']==s['contributionBase']==s['employeeAmount']==s['employerAmount']==0 and json.loads(s['calculationSnapshotJson'])['zeroWage'],'Zero wage persists explicit zero result without deduction line')
    sql("UPDATE PayrollComponents SET SsoWageTreatment='Included' WHERE Id="+ident(earning['payrollComponentId']))
    # Final generated deduction conflict, without changing assignment/rule formulas or configuration.
    ca=api('POST',f'employees/{EMP}/payroll-component-assignments',{'payrollComponentId':canonical['Id'],'amount':1,'effectiveFrom':'2026-09-01','remarks':PREFIX},201);fixtures['assignments'].append(ca['employeePayrollComponentAssignmentId'])
    assert_failure(novp,'reserved');assert_failure(octp,'reserved',pid)
    check(rows('SELECT Amount FROM EmployeePayrollComponentAssignments WHERE EmployeePayrollComponentAssignmentId='+ident(ca['employeePayrollComponentAssignmentId']))[0]['Amount']==1,'Conflicting assignment configuration preserved')
    sql('DELETE FROM EmployeePayrollComponentAssignments WHERE EmployeePayrollComponentAssignmentId='+ident(ca['employeePayrollComponentAssignmentId']))
    canonical_dto=api('GET','payroll-components/'+canonical['Id'])
    cr=rule(canonical_dto,amount=1,stage='Deduction');assert_failure(novp,'reserved');assert_failure(octp,'reserved',pid)
    check(api('GET','payroll-rules/'+cr['payrollRuleId'])['fixedAmount']==1,'Conflicting rule configuration preserved');delete_rule(cr)
    # A financially skipped rule does not appear in the FINAL set and must not conflict.
    cr=rule(canonical_dto,stage='Deduction',method='Percentage',rate=1,base='BasicSalary')
    sql('UPDATE PayrollRules SET MinimumBase=40000 WHERE PayrollRuleId='+ident(cr['payrollRuleId']))
    check(preview(octp)['status']=='Calculated','Skipped canonical rule is absent from final set, no conflict');delete_rule(cr)
    sql("UPDATE EmployeeStatutoryEnrollments SET Applicability='NotApplicable' WHERE EmployeeStatutoryEnrollmentId="+ident(enid))
    cr=rule(canonical_dto,amount=1,stage='Deduction')
    pv=preview(octp);check(pv['status']=='Calculated' and pv['socialSecurity'] is None and pv['totalDeductions']==1,'NotApplicable preserves canonical generic rule deduction')
    gen=generate(octp,True);pid=gen['payrollId'];check(detail(pid)['statutoryResults']==[],'NotApplicable regeneration removes old statutory result atomically');delete_rule(cr)
    # Make a manual earning before SSO, then configure enrollment Applicable: guards must protect both old and proposed type.
    ml=api('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':earning['payrollComponentId'],'amount':5,'remarks':PREFIX},201)
    md=api('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':deduction['payrollComponentId'],'amount':1,'remarks':PREFIX},201)
    sql("UPDATE EmployeeStatutoryEnrollments SET Applicability='Applicable' WHERE EmployeeStatutoryEnrollmentId="+ident(enid))
    oldpid=pid; d,s=parity(octp);pid=d['payroll']['employeePayrollId']
    check(not any(l['sourceType']=='Manual' for l in d['lines']) and not rows('SELECT * FROM EmployeePayrolls WHERE EmployeePayrollId='+ident(oldpid)),'Regeneration removes Manual lines and replaces the header')
    # Legacy Manual earning fixture on generated payroll, totals are updated together for a valid initial state.
    mid=str(uuid.uuid4())
    sql(f"INSERT EmployeePayrollLines (EmployeePayrollLineId,EmployeePayrollId,PayrollComponentId,SourceType,SourceId,ComponentCode,ComponentName,ComponentType,Amount,IsTaxableSnapshot,IsStatutorySnapshot,SsoWageTreatmentSnapshot,Remarks) VALUES ({ident(mid)},{ident(pid)},{ident(earning['payrollComponentId'])},'Manual',NULL,{literal(earning['code'])},{literal(earning['name'])},'Earning',5,1,0,'Included','Synthetic legacy fixture'); UPDATE EmployeePayrolls SET GrossPay=GrossPay+5,TaxableEarnings=TaxableEarnings+5,NetPay=NetPay+5 WHERE EmployeePayrollId={ident(pid)};")
    protected=detail(pid)
    for method,path,body,label in [
        ('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':earning['payrollComponentId'],'amount':5,'remarks':PREFIX},'create earning'),
        ('PUT',f'employee-payrolls/{pid}/lines/{mid}',{'payrollComponentId':earning['payrollComponentId'],'amount':6,'remarks':PREFIX},'change earning amount'),
        ('PUT',f'employee-payrolls/{pid}/lines/{mid}',{'payrollComponentId':excluded['payrollComponentId'],'amount':5,'remarks':PREFIX},'change earning component'),
        ('PUT',f'employee-payrolls/{pid}/lines/{mid}',{'payrollComponentId':deduction['payrollComponentId'],'amount':5,'remarks':PREFIX},'earning to deduction'),
        ('DELETE',f'employee-payrolls/{pid}/lines/{mid}',None,'delete earning')]:
        api(method,path,body,409);check(detail(pid)==protected,'HTTP409 preserves complete snapshot: '+label)
    md=api('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':deduction['payrollComponentId'],'amount':1,'remarks':PREFIX},201)
    protected=detail(pid)
    api('PUT',f"employee-payrolls/{pid}/lines/{md['employeePayrollLineId']}",{'payrollComponentId':earning['payrollComponentId'],'amount':1,'remarks':PREFIX},409)
    check(detail(pid)==protected,'Deduction-to-earning HTTP409 preserves complete snapshot')
    md=api('PUT',f"employee-payrolls/{pid}/lines/{md['employeePayrollLineId']}",{'payrollComponentId':deduction['payrollComponentId'],'amount':2,'remarks':PREFIX})
    check(md['sourceType']=='Manual' and md['sourceId'] is None and detail(pid)['statutoryResults']==protected['statutoryResults'],'Manual deduction update allowed; statutory history unchanged')
    api('DELETE',f"employee-payrolls/{pid}/lines/{md['employeePayrollLineId']}",status=204);check(True,'Manual deduction deletion allowed')
    stat=next(l for l in d['lines'] if l['sourceType']=='Statutory')
    protected=detail(pid)
    api('PUT',f"employee-payrolls/{pid}/lines/{stat['employeePayrollLineId']}",{'payrollComponentId':canonical['Id'],'amount':1,'remarks':PREFIX},409)
    api('DELETE',f"employee-payrolls/{pid}/lines/{stat['employeePayrollLineId']}",status=409)
    check(detail(pid)==protected,'Generated Statutory cannot be manually updated/deleted')
    # New SQL check constraints reject invalid provenance and fractional employee contributions.
    for query,label in [(f"UPDATE EmployeePayrollLines SET SourceId=NULL WHERE EmployeePayrollLineId={ident(stat['employeePayrollLineId'])}",'Statutory null SourceId'),(f"UPDATE EmployeePayrollSocialSecurityResults SET EmployeeAmount=1.5,EmployerAmount=1.5 WHERE EmployeePayrollStatutoryResultId={ident(s['employeePayrollStatutoryResultId'])}",'fractional contribution')]:
        msg=sql("BEGIN TRAN; BEGIN TRY "+query+"; ROLLBACK; THROW 51000,'Invalid accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER()<>547 THROW; SELECT 'Rejected547'; END CATCH;")
        check('Rejected547' in msg,'SQL rejects '+label)
    d,s=parity(octp);pid=d['payroll']['employeePayrollId'];check(not any(l['sourceType']=='Manual' for l in d['lines']),'Successful regeneration removes legacy manual fixture')
    # D3 partial-month wages feed monthly clamp without any independent SSO day proration.
    sql('DELETE FROM EmployeePayrollComponentAssignments WHERE EmployeePayrollComponentAssignmentId='+ident(aid))
    sql("UPDATE PayrollComponents SET SsoWageTreatment='Included' WHERE Id="+ident(basic['Id']))
    d,s=parity(sepp);check(d['payroll']['basicSalary']==2000 and s['contributionWage']==2000 and s['contributionBase']==200,'September 29 joiner salary 2000, full monthly max 200')
    original=original_employment[0]
    sql("UPDATE EmploymentRecords SET EndDate='2026-10-10',IsCurrent=0 WHERE EmploymentRecordId="+ident(original['EmploymentRecordId']))
    d,s=parity(octp);check(d['payroll']['basicSalary']==10000 and s['contributionWage']==10000 and s['contributionBase']==200,'October 10 leaver D3 salary 10000, unprorated SSO bounds')
    clone=dict(original); clone.update(EmploymentRecordId=str(uuid.uuid4()),StartDate='2026-10-20',EndDate=None,IsCurrent=True)
    extra_employment.append(clone['EmploymentRecordId'])
    sql('INSERT EmploymentRecords ('+','.join('['+k+']' for k in clone)+') VALUES ('+','.join(literal(v) for v in clone.values())+')')
    d,s=parity(octp);check(d['payroll']['basicSalary']==22000 and s['contributionWage']==22000 and s['contributionBase']==200,'Employment gap yields D3 salary 22000 with monthly SSO cap unchanged')
    sql('DELETE EmploymentRecords WHERE EmploymentRecordId='+ident(clone['EmploymentRecordId']));extra_employment.clear();restore_row('EmploymentRecords','EmploymentRecordId',original)
    # Stored explanation includes exact policy/enrollment and final line provenance independently of joins.
    snap=json.loads(s['calculationSnapshotJson'])
    check(snap['employeeRate']==snap['employerRate']==10 and snap['officialReference'] and snap['statutoryPolicyVersionId']==polid and snap['employeeStatutoryEnrollmentId']==enid and snap['wageInputs'][0]['sourceType']=='BasicSalary','Historical policy/enrollment/both rates/final provenance complete')

    # Existing rule formulas remain intact with Applicable SSO, on the final line set.
    for c,amount in [(earning,100),(excluded,200),(deduction,5)]:
        a=api('POST',f'employees/{EMP}/payroll-component-assignments',{'payrollComponentId':c['payrollComponentId'],'amount':amount,'effectiveFrom':'2026-09-01','remarks':PREFIX},201);fixtures['assignments'].append(a['employeePayrollComponentAssignmentId'])
    er=rule(earning,amount=30)
    d,s=parity(octp); pid=d['payroll']['employeePayrollId']
    check(d['payroll']['grossPay']==30330 and s['contributionWage']==30130 and d['payroll']['totalDeductions']==25,'Applicable SSO Earning Supplement retains assignment and rule once')
    check(all(l['sourceId'] is not None for l in d['lines'] if l['sourceType'] in ('Assignment','PayrollRule')) and any(l['sourceId']==er['payrollRuleId'] for l in d['lines']),'Assignment and PayrollRule source IDs retained alongside Statutory provenance')
    delete_rule(er);er=rule(earning,'ReplaceAssignment',30)
    d,s=parity(octp);pid=d['payroll']['employeePayrollId']
    check(d['payroll']['grossPay']==30230 and s['contributionWage']==30030 and any(l['payrollComponentId']==excluded['payrollComponentId'] and l['sourceType']=='Assignment' for l in d['lines']),'Applicable SSO Earning ReplaceAssignment suppresses only matching assignment')
    dr=rule(deduction,amount=2,stage='Deduction')
    d,s=parity(octp);pid=d['payroll']['employeePayrollId']
    check(d['payroll']['totalDeductions']==27,'Applicable SSO Deduction Supplement stays additive')
    delete_rule(dr);dr=rule(deduction,'ReplaceAssignment',3,'Deduction')
    d,s=parity(octp);pid=d['payroll']['employeePayrollId']
    check(d['payroll']['totalDeductions']==23 and s['contributionWage']==30030,'Applicable SSO Deduction ReplaceAssignment suppresses matching assignment, no wage impact')
    duplicate=rule(earning,'ReplaceAssignment',4)
    assert_failure(novp,'Multiple applicable');assert_failure(octp,'Multiple applicable',pid);delete_rule(duplicate)
    duplicate=rule(deduction,'ReplaceAssignment',4,'Deduction')
    assert_failure(novp,'Multiple applicable');assert_failure(octp,'Multiple applicable',pid);delete_rule(duplicate)
    stable=detail(pid)
    sql("UPDATE EmployeeStatutoryEnrollments SET EffectiveTo='2026-09-30' WHERE EmployeeStatutoryEnrollmentId="+ident(enid))
    assert_failure(octp,'enrollment',pid);check(detail(pid)==stable,'Enrollment edits cannot rewrite stored statutory snapshot')
    sql("UPDATE EmployeeStatutoryEnrollments SET EffectiveTo='2026-12-31' WHERE EmployeeStatutoryEnrollmentId="+ident(enid))
    sql('UPDATE SocialSecurityPolicyConfigurations SET EmployeeContributionRate=20,EmployerContributionRate=20 WHERE StatutoryPolicyVersionId='+ident(polid))
    check(detail(pid)==stable,'Live fixture policy rate change does not rewrite history')
    d,s=parity(octp);pid=d['payroll']['employeePayrollId']
    check(s['employeeAmount']==40 and d['payroll']['totalDeductions']==43,'Deliberate regeneration uses current policy rates and replaces complete result')
    persisted=rows("SELECT p.EmployeePayrollId,p.TotalDeductions,r.EmployeePayrollStatutoryResultId,s.EmployeeAmount,s.EmployerAmount,l.Amount,l.SourceType,l.SourceId FROM EmployeePayrolls p JOIN EmployeePayrollStatutoryResults r ON r.EmployeePayrollId=p.EmployeePayrollId JOIN EmployeePayrollSocialSecurityResults s ON s.EmployeePayrollStatutoryResultId=r.EmployeePayrollStatutoryResultId JOIN EmployeePayrollLines l ON l.EmployeePayrollId=p.EmployeePayrollId AND l.SourceType='Statutory' WHERE p.EmployeePayrollId="+ident(pid))
    check(len(persisted)==1 and persisted[0]['EmployeeAmount']==persisted[0]['Amount']==40 and persisted[0]['SourceId'].lower()==persisted[0]['EmployeePayrollStatutoryResultId'].lower(),'Live SQL exact amount/provenance persistence')

    pid=d['payroll']['employeePayrollId']
    api('POST',f'employee-payrolls/{pid}/approve',{},204);check(generate(octp,True)['status']=='Skipped','SSO Approved payroll regeneration protected')
    api('POST',f'employee-payrolls/{pid}/mark-paid',{},204);check(generate(octp,True)['status']=='Skipped','SSO Paid payroll regeneration protected')
except Exception as exc:
    error=str(exc);traceback.print_exc()
finally:
    try:
        cleanup()
        if extra_employment: sql('DELETE EmploymentRecords WHERE EmploymentRecordId IN ('+','.join(map(ident,extra_employment))+')')
        for row in original_employment: restore_row('EmploymentRecords','EmploymentRecordId',row)
        for row in original_components: restore_row('PayrollComponents','Id',row)
        final=snapshot()
        check(final==baseline,'Exact contents of all 55 application tables restored, including timestamps')
        check(rows('SELECT IsActive FROM Employees WHERE EmployeeId='+ident(EMP))[0]['IsActive']==False,'TEST-EMP-001 inactive')
    except Exception as exc:
        error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':results,'error':error,'fixtures':fixtures,'schema':locals().get('schema'),'constraints':locals().get('constraints'),'foreignKeys':locals().get('fks'),'finalCounts':{t:len(v) for t,v in locals().get('final',{}).items()}},indent=2))
if error: raise SystemExit(error)
print(f'PASS: {len(results)} D5C live assertions; exact baseline restored.',flush=True)

