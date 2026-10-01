import json, urllib.request, urllib.error, subprocess, uuid, pathlib, hashlib, traceback
ROOT=pathlib.Path(__file__).resolve().parents[1]
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d5a-live-results.json'
BASE='http://localhost:5155'
EMP='433f2c1a-6222-494f-a64f-cd0c31126dc4'
PREFIX='D5A-VERIFY-'
results=[]
fixtures={'components':[], 'periods':[], 'rules':[], 'assignments':[], 'compensations':[], 'schemes':[], 'enrollments':[], 'declarations':[]}
def check(ok,name):
    if not ok: raise AssertionError(name)
    results.append(name)
    print('PASS:',name,flush=True)
def api(method,path,body=None,status=200):
    req=urllib.request.Request(BASE+'/api/'+path,data=None if body is None else json.dumps(body).encode(),method=method,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=30) as r: code,data=r.status,r.read()
    except urllib.error.HTTPError as e: code,data=e.code,e.read()
    if code != status: raise AssertionError(f'{method} {path}: expected {status}, got {code}: {data.decode()[:2000]}')
    return json.loads(data) if data else None

def sql(query):
    p=subprocess.run(['sqlcmd','-S','localhost','-d','SIAMIS','-E','-C','-I','-b','-y','0','-w','65535','-Q','SET NOCOUNT ON; '+query],capture_output=True,text=True)
    if p.returncode: raise AssertionError(p.stdout+p.stderr)
    return p.stdout.strip()
def rows(query): return json.loads(''.join(sql(query+' FOR JSON PATH').splitlines()) or '[]')
def ident(v): return "'"+str(uuid.UUID(v))+"'"
def cleanup():
    # Target only recorded fixture IDs; no baseline rows are deleted/updated.
    clauses=[]
    if fixtures['periods']:
        ids=','.join(map(ident,fixtures['periods']))
        clauses += [f'DELETE FROM EmployeePayrollSocialSecurityResults WHERE EmployeePayrollStatutoryResultId IN (SELECT EmployeePayrollStatutoryResultId FROM EmployeePayrollStatutoryResults WHERE EmployeePayrollId IN (SELECT EmployeePayrollId FROM EmployeePayrolls WHERE PayrollPeriodId IN ({ids})))', f'DELETE FROM EmployeePayrollStatutoryResults WHERE EmployeePayrollId IN (SELECT EmployeePayrollId FROM EmployeePayrolls WHERE PayrollPeriodId IN ({ids}))',f'DELETE FROM EmployeePayrollLines WHERE EmployeePayrollId IN (SELECT EmployeePayrollId FROM EmployeePayrolls WHERE PayrollPeriodId IN ({ids}))',f'DELETE FROM EmployeePayrolls WHERE PayrollPeriodId IN ({ids})',f'DELETE FROM PayrollPeriods WHERE PayrollPeriodId IN ({ids})']
    for table,key,group in [('PayrollRuleTargets','PayrollRuleId','rules'),('PayrollRules','PayrollRuleId','rules'),('EmployeePayrollComponentAssignments','EmployeePayrollComponentAssignmentId','assignments'),('EmployeeCompensations','EmployeeCompensationId','compensations'),('EmployeeStatutoryEnrollments','EmployeeStatutoryEnrollmentId','enrollments')]:
        if fixtures[group]: clauses.append(f'DELETE FROM [{table}] WHERE [{key}] IN ('+','.join(map(ident,fixtures[group]))+')')
    if fixtures['declarations']:
        ids=','.join(map(ident,fixtures['declarations']))
        for table,key in [('EmployeeTaxDeclarationSelections','CurrentDeclarationId'),('EmployeeTaxClaims','EmployeeTaxDeclarationId'),('EmployeeTaxOpeningBalances','EmployeeTaxDeclarationId'),('EmployeeTaxDeclarations','EmployeeTaxDeclarationId')]:
            clauses.append(f'DELETE FROM [{table}] WHERE [{key}] IN ({ids})')
    if fixtures['schemes']:
        ids=','.join(map(ident,fixtures['schemes']))
        clauses += [f'DELETE FROM SocialSecurityPolicyConfigurations WHERE StatutoryPolicyVersionId IN (SELECT StatutoryPolicyVersionId FROM StatutoryPolicyVersions WHERE StatutorySchemeId IN ({ids}))',f'DELETE FROM StatutoryPolicyVersions WHERE StatutorySchemeId IN ({ids})',f'DELETE FROM StatutorySchemes WHERE StatutorySchemeId IN ({ids})']
    if fixtures['components']: clauses.append('DELETE FROM PayrollComponents WHERE Id IN ('+','.join(map(ident,fixtures['components']))+')')
    if clauses: sql('SET XACT_ABORT ON; BEGIN TRANSACTION; '+'; '.join(clauses)+'; COMMIT;')

def snapshot():
    return {r['name']:sorted([json.dumps(x,sort_keys=True) for x in rows('SELECT * FROM ['+r['name']+']')]) for r in rows("SELECT name FROM sys.tables WHERE name <> '__EFMigrationsHistory'")}
def component(code,treatment=None,category='Earning',taxable=True):
    body={'code':PREFIX+code,'name':PREFIX+code,'componentType':category,'calculationMethod':'FixedAmount','isTaxable':taxable}
    if treatment is not None: body['ssoWageTreatment']=treatment
    c=api('POST','payroll-components',body,201);fixtures['components'].append(c['payrollComponentId']);return c

def update(c,treatment=...):
    body={'code':c['code'],'name':c['name'],'componentType':c['componentType'],'calculationMethod':c['calculationMethod'],'isTaxable':c['isTaxable'],'isStatutory':c['isStatutory'],'contributionSide':c['contributionSide']}
    if treatment is not ...: body['ssoWageTreatment']=treatment
    return api('PUT','payroll-components/'+c['payrollComponentId'],body)
def period(month):
    start=f'2026-{month:02}-01'; end=f'2026-{month:02}-'+('30' if month in (9,11) else '31')
    p=api('POST','payroll-periods',{'code':PREFIX+str(month),'name':PREFIX+str(month),'startDate':start,'endDate':end,'payDate':end},201)
    fixtures['periods'].append(p['payrollPeriodId']);return p['payrollPeriodId']
def rule(c,mode='Supplement',amount=300,stage='Earning',method='FixedAmount',rate=None,base=None):
    body={'code':PREFIX+str(uuid.uuid4()),'name':PREFIX+'SyntheticRule','payrollComponentId':c['payrollComponentId'],'applicationMode':mode,'priority':1,'ruleType':'Other','calculationMethod':method,'calculationStage':stage,'appliesTo':'Employee','effectiveFrom':'2026-09-01','isActive':True}
    if method=='FixedAmount':body['fixedAmount']=amount
    else: body.update(rate=rate,baseType=base)
    r=api('POST','payroll-rules',body,201);fixtures['rules'].append(r['payrollRuleId']);return r

def delete_rule(r): api('DELETE','payroll-rules/'+r['payrollRuleId'],status=204)
def preview(p): return api('POST',f'payroll-periods/{p}/preview',{'employeeIds':[EMP]})['results'][0]
def generate(p,force=False): return api('POST',f'payroll-periods/{p}/generate',{'employeeIds':[EMP],'forceRegenerate':force})['results'][0]
def detail(pid):return api('GET','employee-payrolls/'+pid)
def totals(d):return tuple(d[k] for k in ('basicSalary','grossPay','taxableEarnings','totalDeductions','netPay'))

baseline=snapshot()
error=None
try:
    check(len(baseline['Employees'])==1 and len(baseline['PayrollComponents'])==17 and not baseline['EmployeePayrolls'] and not baseline['PayrollRules'],'baseline verified before fixtures')
    check(rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId='20261001032904_AddSsoWageTreatmentClassification'")!=[], 'D5A migration recorded')
    schema=rows("SELECT OBJECT_NAME(c.object_id) AS [Table], c.name, TYPE_NAME(c.user_type_id) AS TypeName, c.max_length, c.is_nullable, d.definition AS DefaultValue FROM sys.columns c LEFT JOIN sys.default_constraints d ON d.object_id=c.default_object_id WHERE (OBJECT_NAME(c.object_id)='PayrollComponents' AND c.name='SsoWageTreatment') OR (OBJECT_NAME(c.object_id)='EmployeePayrollLines' AND c.name='SsoWageTreatmentSnapshot')")
    check(len(schema)==2 and all(x['TypeName']=='nvarchar' and x['max_length']==40 and not x['is_nullable'] and 'Unknown' in x['DefaultValue'] for x in schema),'live columns required nvarchar(20), default Unknown')
    constraints=rows("SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name IN ('CK_PayrollComponents_SsoWageTreatment','CK_EmployeePayrollLines_SsoWageTreatmentSnapshot')")
    check(len(constraints)==2 and all(not x['is_disabled'] and not x['is_not_trusted'] and all(v in x['definition'] for v in ('Unknown','Included','Excluded')) for x in constraints),'live check constraints enabled and trusted')
    existing=api('GET','payroll-components?includeInactive=true')
    check(len(existing)==17 and all(x['ssoWageTreatment']=='Unknown' for x in existing),'17 existing components Unknown')
    taxable_salary=30000 if next(c for c in existing if c['name']=='Basic Salary')['isTaxable'] else 0
    # Monetary integration requires an explicit opt-out for these non-statutory regression fixtures.
    opt=api('POST','statutory-schemes',{'code':'TH-SSO-33','name':PREFIX+'Explicit opt-out','jurisdiction':'TH','schemeType':'SocialSecurity'},201); fixtures['schemes'].append(opt['statutorySchemeId'])
    opten=api('POST',f'employees/{EMP}/statutory-enrollments',{'statutorySchemeId':opt['statutorySchemeId'],'effectiveFrom':'2026-09-01','effectiveTo':'2026-12-31','applicability':'NotApplicable'},201); fixtures['enrollments'].append(opten['employeeStatutoryEnrollmentId'])
    a=component('Default'); u=component('ExplicitUnknown','Unknown'); e=component('Included','Included'); x=component('Excluded','Excluded'); d=component('Deduction','Unknown','Deduction',False)
    for c,value in [(a,'Unknown'),(u,'Unknown'),(e,'Included'),(x,'Excluded')]:
        check(c['ssoWageTreatment']==value and api('GET','payroll-components/'+c['payrollComponentId'])['ssoWageTreatment']==value,'component create/detail '+value)
        check(rows('SELECT SsoWageTreatment FROM PayrollComponents WHERE Id='+ident(c['payrollComponentId']))[0]['SsoWageTreatment']==value,'SQL component persistence '+value)
    api('POST','payroll-components',{'code':PREFIX+'Invalid','name':'Invalid','componentType':'Earning','calculationMethod':'FixedAmount','ssoWageTreatment':'Invalid'},400);check(True,'invalid create HTTP 400')
    invalid={'code':a['code'],'name':a['name'],'componentType':'Earning','calculationMethod':'FixedAmount','ssoWageTreatment':''}
    api('PUT','payroll-components/'+a['payrollComponentId'],invalid,400);check(True,'invalid update HTTP 400')
    a=update(a,'Included');check(a['ssoWageTreatment']=='Included','classification update persists')
    a=update(a);check(a['ssoWageTreatment']=='Included','omitted update preserves classification')
    a=update(a,None);check(a['ssoWageTreatment']=='Included','null update preserves classification')
    filtered=api('GET','payroll-components?componentType=Earning&isTaxable=true&search='+PREFIX)
    check(len(filtered)==4 and all('ssoWageTreatment' in c for c in filtered),'existing type/tax/search filters return classification')
    paytype=rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id']
    comp=api('POST',f'employees/{EMP}/compensations',{'payTypeId':paytype,'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-01','isCurrent':True,'remarks':PREFIX+'Synthetic compensation'},201)
    fixtures['compensations'].append(comp['compensationId'])
    for c,amount in [(e,1000),(x,2000),(d,500)]:
        ass=api('POST',f'employees/{EMP}/payroll-component-assignments',{'payrollComponentId':c['payrollComponentId'],'amount':amount,'effectiveFrom':'2026-09-01','remarks':PREFIX+'Assignment'},201)
        fixtures['assignments'].append(ass['employeePayrollComponentAssignmentId'])
    octp=period(10); novp=period(11); sepp=period(9)
    r=rule(e)
    pv=preview(octp); gen=generate(octp);check(gen['status']=='Generated','initial generation succeeds')
    pid=gen['payrollId']; original=detail(pid)
    check(totals(original['payroll'])==(30000,33300,taxable_salary+3300,500,32800),'baseline salary/gross/tax/deductions/net unchanged')
    salary=next(l for l in original['lines'] if l['sourceType']=='BasicSalary')
    check(salary['ssoWageTreatmentSnapshot']=='Unknown' and salary['sourceId'] is None,'BasicSalary live snapshot Unknown and truthful source')
    assigned=next(l for l in original['lines'] if l['sourceId']==fixtures['assignments'][0])
    ruled=next(l for l in original['lines'] if l['sourceId']==r['payrollRuleId'])
    check(assigned['sourceType']=='Assignment' and assigned['ssoWageTreatmentSnapshot']=='Included','Assignment live snapshot/provenance')
    check(ruled['sourceType']=='PayrollRule' and ruled['ssoWageTreatmentSnapshot']=='Included','PayrollRule live snapshot/provenance')
    def signature(lines):return sorted((l['sourceType'],l['sourceId'] or '',l['payrollComponentId'],l['amount'],l['ssoWageTreatmentSnapshot']) for l in lines)
    check(signature(pv['lines'])==signature(original['lines']) and totals(pv)==totals(original['payroll']),'Preview/Generation amounts and classifications parity')
    persisted=rows('SELECT SourceType,SourceId,Amount,SsoWageTreatmentSnapshot FROM EmployeePayrollLines WHERE EmployeePayrollId='+ident(pid))
    check(len(persisted)==5 and any(l['SourceType']=='PayrollRule' and l['SsoWageTreatmentSnapshot']=='Included' for l in persisted),'SQL generated line snapshots persisted')
    e=update(e,'Excluded');check(detail(pid)==original,'live classification update leaves entire historical payroll unchanged')
    check(generate(octp,True)['status']=='Generated','deliberate regeneration succeeds')
    pid=generate(octp)['payrollId']; regen=detail(pid)
    check(totals(regen['payroll'])==totals(original['payroll']) and all(l['ssoWageTreatmentSnapshot']=='Excluded' for l in regen['lines'] if l['payrollComponentId']==e['payrollComponentId']),'regeneration uses current classification without amount changes')
    check(signature(preview(octp)['lines'])==signature(regen['lines']),'regenerated Preview/Generation parity')
    check(json.loads(salary['basicSalaryCalculationSnapshotJson'])['amount']==30000,'D3 full-month entitlement/audit unchanged')
    check(preview(sepp)['basicSalary']==2000,'D3 September 29 joiner ThirtyDay salary entitlement unchanged')
    # Actual invalid SQL attempts roll back and are restricted to temporary fixtures.
    for table,col,key,idval in [('PayrollComponents','SsoWageTreatment','Id',e['payrollComponentId']),('EmployeePayrollLines','SsoWageTreatmentSnapshot','EmployeePayrollLineId',regen['lines'][0]['employeePayrollLineId'])]:
        msg=sql(f"BEGIN TRANSACTION; BEGIN TRY UPDATE [{table}] SET [{col}]='Invalid' WHERE [{key}]={ident(idval)}; ROLLBACK; THROW 51000,'Constraint failed to reject invalid value',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER()<>547 THROW; SELECT 'Rejected547' AS Result; END CATCH;")
        check('Rejected547' in msg,'SQL constraint rejects invalid '+col)
    rr=rule(e,'ReplaceAssignment');delete_rule(r)
    check(preview(octp)['grossPay']==32300,'earning ReplaceAssignment suppresses matching assignment only')
    rd=rule(d,'Supplement',200,'Deduction');check(preview(octp)['totalDeductions']==700,'deduction Supplement unchanged')
    delete_rule(rd); rd=rule(d,'ReplaceAssignment',200,'Deduction');check(preview(octp)['totalDeductions']==200,'deduction ReplaceAssignment unchanged')
    conflict=rule(e,'ReplaceAssignment',400)
    before=detail(pid)
    check(generate(novp)['status']=='Failed' and not rows('SELECT EmployeePayrollId FROM EmployeePayrolls WHERE PayrollPeriodId='+ident(novp)),'failed NEW generation leaves no header')
    check(not rows('SELECT EmployeePayrollLineId FROM EmployeePayrollLines WHERE EmployeePayrollId NOT IN (SELECT EmployeePayrollId FROM EmployeePayrolls)'),'failed NEW generation no orphan lines')
    check(generate(octp,True)['status']=='Failed' and detail(pid)==before,'failed regeneration preserves complete header/totals/IDs/lines/audits')
    delete_rule(conflict);delete_rule(rr);delete_rule(rd)
    percent=rule(e,'Supplement',method='Percentage',rate=10,base='BasicSalary');check(preview(octp)['grossPay']==36000,'earning Percentage BasicSalary formula unchanged');delete_rule(percent)
    percent=rule(d,'Supplement',stage='Deduction',method='Percentage',rate=10,base='GrossPay');check(preview(octp)['totalDeductions']==3800,'deduction Percentage GrossPay formula unchanged');delete_rule(percent)
    # Manual reconciliation retains source snapshots when editing only amounts.
    ml=api('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':e['payrollComponentId'],'amount':100,'remarks':PREFIX+'Manual'},201)
    check(ml['sourceType']=='Manual' and ml['sourceId'] is None and ml['ssoWageTreatmentSnapshot']=='Excluded','manual creation server source and component snapshot')
    check(rows('SELECT SourceType,SsoWageTreatmentSnapshot FROM EmployeePayrollLines WHERE EmployeePayrollLineId='+ident(ml['employeePayrollLineId']))[0]['SsoWageTreatmentSnapshot']=='Excluded','manual snapshot persisted in SQL')
    e=update(e,'Included')
    ml=api('PUT',f"employee-payrolls/{pid}/lines/{ml['employeePayrollLineId']}",{'payrollComponentId':e['payrollComponentId'],'amount':200,'remarks':PREFIX+'Manual updated'})
    check(ml['ssoWageTreatmentSnapshot']=='Excluded' and ml['sourceType']=='Manual','manual amount update preserves historical classification')
    check(totals(detail(pid)['payroll'])==(30000,33500,taxable_salary+3500,500,33000),'manual update reconciles totals preserving BasicSalary')
    ml=api('PUT',f"employee-payrolls/{pid}/lines/{ml['employeePayrollLineId']}",{'payrollComponentId':a['payrollComponentId'],'amount':200,'remarks':PREFIX+'Manual explicit component change'})
    check(ml['ssoWageTreatmentSnapshot']=='Included' and ml['sourceType']=='Manual' and ml['sourceId'] is None,'manual explicit component replacement snapshots new component without source change')
    api('DELETE',f"employee-payrolls/{pid}/lines/{ml['employeePayrollLineId']}",status=204)
    check(totals(detail(pid)['payroll'])==totals(regen['payroll']),'manual deletion restores calculated totals')
    api('PUT',f"employee-payrolls/{pid}/lines/{salary['employeePayrollLineId']}",{'payrollComponentId':salary['payrollComponentId'],'amount':10,'remarks':'Disallowed'},404) # old regenerated ID must no longer exist
    current_salary=next(l for l in detail(pid)['lines'] if l['sourceType']=='BasicSalary')
    api('DELETE',f"employee-payrolls/{pid}/lines/{current_salary['employeePayrollLineId']}",status=409);check(True,'generated line manual mutation protected')
    # Payroll and payroll-period state machine guards.
    api('POST',f'payroll-periods/{octp}/start-processing',{},204)
    api('POST',f'payroll-periods/{octp}/close',{},409);check(True,'unfinished period closure rejected')
    api('POST',f'employee-payrolls/{pid}/approve',{},204)
    check(generate(octp,True)['status']=='Skipped','Approved payroll regeneration protected')
    api('POST',f'employee-payrolls/{pid}/lines',{'payrollComponentId':e['payrollComponentId'],'amount':100,'remarks':'Disallowed'},409);check(True,'Approved payroll manual adjustment rejected')
    api('POST',f'employee-payrolls/{pid}/mark-paid',{},204)
    check(generate(octp,True)['status']=='Skipped','Paid payroll regeneration protected')
    api('POST',f'employee-payrolls/{pid}/cancel',{'reason':'Disallowed'},409);check(True,'Paid payroll cancellation rejected')
    api('POST',f'payroll-periods/{octp}/close',{},204)
    api('POST',f'payroll-periods/{octp}/generate',{'employeeIds':[EMP]},409);check(True,'Closed payroll period mutation protected')
    np=generate(novp);check(np['status']=='Generated','new generation after removing conflict succeeds')
    api('POST',f"employee-payrolls/{np['payrollId']}/cancel",{'reason':PREFIX+'Cancel'},204)
    check(generate(novp,True)['status']=='Skipped','Cancelled payroll regeneration protected')
    api('POST',f'payroll-periods/{novp}/cancel',{'reason':PREFIX+'Cancel period'},204)
    api('POST',f'payroll-periods/{novp}/preview',{'employeeIds':[EMP]},409);check(True,'Cancelled period preview protected')
    # D4A/B remain independently configured; values below are synthetic verification fixtures, not legal policy.
    decp=period(12); before_statutory=preview(decp)
    scheme=api('POST','statutory-schemes',{'code':PREFIX+'Synthetic-SSO','name':PREFIX+'Synthetic SSO','jurisdiction':'TH','schemeType':'SocialSecurity'},201);sid=scheme['statutorySchemeId'];fixtures['schemes'].append(sid)
    policy=api('POST','statutory-policy-versions',{'statutorySchemeId':sid,'version':PREFIX+'Synthetic','effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','currency':'THB','calculationMethodVersion':'SSO-TH-V1','officialReference':'Synthetic verification only; not legal values'},201);polid=policy['statutoryPolicyVersionId']
    api('POST',f'statutory-policy-versions/{polid}/publish',{},400);check(True,'D4A incomplete publication rejected')
    api('PUT',f'statutory-policy-versions/{polid}/social-security',{'employeeContributionRate':1.23,'employerContributionRate':1.23,'minimumContributionBase':100,'maximumContributionBase':200,'insuredPersonClassification':'33'})
    api('POST',f'statutory-policy-versions/{polid}/publish',{})
    check(api('GET',f'statutory-schemes/{sid}/resolve?governingDate=2026-10-01')['outcome']=='Resolved','D4A publication/resolution unchanged; caller date only')
    api('PUT',f'statutory-policy-versions/{polid}/social-security',{'employeeContributionRate':1},409);check(True,'D4A Published immutability unchanged')
    unknown=api('GET',f'employees/{EMP}/statutory-enrollments/resolve?schemeId={sid}&date=2026-10-01');check(unknown['outcome']=='Unknown' if 'outcome' in unknown else unknown['applicability']=='Unknown','D4B absent enrollment Unknown')
    en=api('POST',f'employees/{EMP}/statutory-enrollments',{'statutorySchemeId':sid,'effectiveFrom':'2026-10-01','effectiveTo':'2026-12-31','applicability':'Applicable','remarks':PREFIX+'Synthetic enrollment'},201);fixtures['enrollments'].append(en['employeeStatutoryEnrollmentId'])
    resolved=api('GET',f'employees/{EMP}/statutory-enrollments/resolve?schemeId={sid}&date=2026-10-01');check(resolved.get('applicability',resolved.get('outcome'))=='Applicable','D4B inclusive enrollment resolution unchanged')
    api('POST',f'employees/{EMP}/statutory-enrollments',{'statutorySchemeId':sid,'effectiveFrom':'2026-11-01','applicability':'NotApplicable'},409);check(True,'D4B overlapping enrollment rejected')
    check(totals(preview(decp))==totals(before_statutory)==(30000,33000,taxable_salary+3000,500,32500),'independent scheme enrollment does not override explicit canonical NotApplicable')
    decgen=generate(decp);check(decgen['status']=='Generated' and totals(detail(decgen['payrollId'])['payroll'])==totals(before_statutory),'Generation with canonical NotApplicable preserves generic payroll money')
    decl=api('POST',f'employees/{EMP}/tax-declarations',{'taxYear':2026,'remarks':PREFIX+'Synthetic declaration'},201);did=decl['declaration']['employeeTaxDeclarationId'];fixtures['declarations'].append(did)
    api('POST',f'employees/{EMP}/tax-declarations/{did}/verify',{},400);check(True,'D4B declaration needs explicit opening state')
    api('PUT',f'employees/{EMP}/tax-declarations/{did}/opening-balance',{'state':'Unknown','currency':'THB','asOfDate':'2025-12-31'})
    api('POST',f'employees/{EMP}/tax-declarations/{did}/verify',{})
    api('PUT',f'employees/{EMP}/tax-declarations/{did}',{'remarks':'Disallowed'},409);check(True,'D4B Verified declaration immutability unchanged')
except Exception as exc:
    error=str(exc);traceback.print_exc()
finally:
    try:
        cleanup()
        final=snapshot()
        check(final==baseline,'cleanup restores exact pre-fixture contents of all application tables')
        check(rows('SELECT IsActive FROM Employees WHERE EmployeeId='+ident(EMP))[0]['IsActive']==False,'TEST-EMP-001 remains inactive')
    except Exception as cleanup_error:
        error=(error or '')+' CLEANUP: '+str(cleanup_error);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':results,'error':error,'fixtures':fixtures,'schema':locals().get('schema'),'constraints':locals().get('constraints'),'finalCounts':{t:len(v) for t,v in locals().get('final',{}).items()}},indent=2))
if error:raise SystemExit(error)
print(f'PASS: {len(results)} live assertions; exact baseline restored.',flush=True)
