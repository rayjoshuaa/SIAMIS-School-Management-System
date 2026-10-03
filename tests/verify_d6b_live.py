"""Local Development D6B verification. Classifications are synthetic fixtures, not legal determinations.
All existing component columns/timestamps are restored and every application table is compared.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6b-live-results.json'
PREFIX='D6B-VERIFY-'
def rows(query): return json.loads(''.join(sql(query+' FOR JSON PATH, INCLUDE_NULL_VALUES').splitlines()) or '[]')
def literal(v):
    if v is None:return 'NULL'
    if isinstance(v,bool):return '1' if v else '0'
    if isinstance(v,(int,float)):return str(v)
    return "N'"+str(v).replace("'","''")+"'"
def restore(table,key,row):
    sql('UPDATE ['+table+'] SET '+','.join('['+k+']='+literal(v) for k,v in row.items() if k!=key)+' WHERE ['+key+']='+literal(row[key]))
def pit_update(c,value):
    return api('PUT','payroll-components/'+c['payrollComponentId'],{
        'code':c['code'],'name':c['name'],'componentType':c['componentType'],'calculationMethod':c['calculationMethod'],
        'percentageBase':c['percentageBase'],'isTaxable':c['isTaxable'],'isStatutory':c['isStatutory'],
        'contributionSide':c['contributionSide'],'pitIncomeTreatment':value})
def treatment(d,residence,kind,status=200):
    return api('PUT',f'employees/{EMP}/tax-declarations/{d}/treatment',{
        'residencyStatus':residence,'employmentTaxTreatment':kind,'remarks':PREFIX+'Synthetic reviewed contract'},status)
def declaration(year):
    d=api('POST',f'employees/{EMP}/tax-declarations',{'taxYear':year,'remarks':PREFIX+'Synthetic'},201)
    did=d['declaration']['employeeTaxDeclarationId'];fixtures['declarations'].append(did);return did
def verify(d,year):
    api('PUT',f'employees/{EMP}/tax-declarations/{d}/opening-balance',{'state':'Unknown','currency':'THB','asOfDate':str(year-1)+'-12-31'})
    return api('POST',f'employees/{EMP}/tax-declarations/{d}/verify',{})
def resolution(year): return api('GET',f'employees/{EMP}/tax-treatment?taxYear={year}')
baseline=snapshot(); original_components=rows('SELECT * FROM PayrollComponents'); original_employee=rows('SELECT * FROM Employees WHERE EmployeeId='+ident(EMP))[0]; profile_id=None; error=None
try:
    pit_opt_out()
    check(len(baseline)==57 and len(baseline['Employees'])==1 and len(baseline['PayrollComponents'])==17 and not baseline['EmployeePayrolls'],'57-table baseline checked before fixtures')
    check(all(c['PitIncomeTreatment']=='Unknown' for c in original_components),'all 17 existing components migrated conservatively Unknown')
    migration=rows("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddPitIncomeClassificationAndTaxTreatment'")
    check(len(migration)==1,'focused migration applied once')
    schema=rows("SELECT OBJECT_NAME(object_id) AS [Table],name,max_length,is_nullable FROM sys.columns WHERE (OBJECT_NAME(object_id)='PayrollComponents' AND name='PitIncomeTreatment') OR (OBJECT_NAME(object_id)='EmployeePayrollLines' AND name='PitIncomeTreatmentSnapshot') OR (OBJECT_NAME(object_id)='EmployeeTaxDeclarations' AND name IN ('ResidencyStatus','EmploymentTaxTreatment'))")
    check(len(schema)==4 and all(not c['is_nullable'] for c in schema),'four required classification/treatment columns exist')
    constraints=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name IN ('CK_PayrollComponents_PitIncomeTreatment','CK_EmployeePayrollLines_PitIncomeTreatmentSnapshot','CK_EmployeeTaxDeclarations_ResidencyStatus','CK_EmployeeTaxDeclarations_EmploymentTaxTreatment')")
    check(len(constraints)==4 and all(not c['is_disabled'] and not c['is_not_trusted'] for c in constraints),'four vocabulary checks enabled and trusted')
    components=[]
    for value in [None,'Unknown','Included','Excluded']:
        body={'code':PREFIX+str(uuid.uuid4()),'name':PREFIX+'Synthetic','componentType':'Earning','calculationMethod':'FixedAmount','isTaxable':True}
        if value is not None:body['pitIncomeTreatment']=value
        c=api('POST','payroll-components',body,201);fixtures['components'].append(c['payrollComponentId']);components.append(c)
        check(c['pitIncomeTreatment']==(value or 'Unknown'),'component create classification '+str(value))
        check(rows('SELECT PitIncomeTreatment FROM PayrollComponents WHERE Id='+ident(c['payrollComponentId']))[0]['PitIncomeTreatment']==(value or 'Unknown'),'SQL component classification '+str(value))
        check(api('GET','payroll-components/'+c['payrollComponentId'])['pitIncomeTreatment']==(value or 'Unknown'),'detail exposes classification '+str(value))
    e=components[2]; x=components[3]
    for invalid in ['invalid','','included']:
        api('POST','payroll-components',{'code':PREFIX+str(uuid.uuid4()),'name':PREFIX+'Invalid','componentType':'Earning','calculationMethod':'FixedAmount','pitIncomeTreatment':invalid},400)
        api('PUT','payroll-components/'+e['payrollComponentId'],{'code':e['code'],'name':e['name'],'componentType':'Earning','calculationMethod':'FixedAmount','pitIncomeTreatment':invalid},400)
        check(True,'invalid component create/update rejected '+repr(invalid))
    opt=api('POST','statutory-schemes',{'code':'TH-SSO-33','name':PREFIX+'Explicit opt-out','jurisdiction':'TH','schemeType':'SocialSecurity'},201);fixtures['schemes'].append(opt['statutorySchemeId'])
    en=api('POST',f'employees/{EMP}/statutory-enrollments',{'statutorySchemeId':opt['statutorySchemeId'],'effectiveFrom':'2026-09-01','applicability':'NotApplicable','remarks':PREFIX+'Synthetic opt-out'},201);fixtures['enrollments'].append(en['employeeStatutoryEnrollmentId'])
    comp=api('POST',f'employees/{EMP}/compensations',{'payTypeId':rows("SELECT Id FROM PayTypes WHERE Name='Monthly'")[0]['Id'],'basicSalary':30000,'currency':'THB','effectiveFrom':'2026-09-01','isCurrent':True,'remarks':PREFIX+'Synthetic compensation'},201);fixtures['compensations'].append(comp['compensationId'])
    ass=api('POST',f'employees/{EMP}/payroll-component-assignments',{'payrollComponentId':e['payrollComponentId'],'amount':1000,'effectiveFrom':'2026-09-01','remarks':PREFIX+'Synthetic assignment'},201);fixtures['assignments'].append(ass['employeePayrollComponentAssignmentId'])
    r=rule(e); p=period(10)
    basic=next(c for c in api('GET','payroll-components?includeInactive=true') if c['code']=='EARN-001')
    before=preview(p);basic=pit_update(basic,'Included');after=preview(p)
    check(totals(before)==totals(after),'classification alone does not change any payroll financial total')
    gen=generate(p);check(gen['status']=='Generated','classification foundation does not block legacy generation')
    pid=gen['payrollId']; initial=detail(pid)
    for source in ['BasicSalary','Assignment','PayrollRule']:
        l=next(l for l in initial['lines'] if l['sourceType']==source)
        check(l['pitIncomeTreatmentSnapshot']=='Included','generated '+source+' PIT snapshot')
        check(rows('SELECT PitIncomeTreatmentSnapshot FROM EmployeePayrollLines WHERE EmployeePayrollLineId='+ident(l['employeePayrollLineId']))[0]['PitIncomeTreatmentSnapshot']=='Included','SQL '+source+' PIT snapshot')
    def signature(lines):return sorted((l['sourceType'],l['sourceId'] or '',l['amount'],l['pitIncomeTreatmentSnapshot']) for l in lines)
    check(signature(after['lines'])==signature(initial['lines']),'Preview/Generation PIT snapshot parity')
    e=pit_update(e,'Excluded');basic=pit_update(basic,'Excluded')
    check(detail(pid)==initial,'live classification changes preserve entire historical payroll')
    gen=generate(p,True);pid=gen['payrollId'];regen=detail(pid)
    check(all(l['pitIncomeTreatmentSnapshot']=='Excluded' for l in regen['lines']),'regeneration uses current classification')
    check(totals(regen['payroll'])==totals(initial['payroll']),'regeneration classification leaves money unchanged')
    check(not any(l['sourceType']=='Statutory' for l in regen['lines']),'no PIT/statutory line created for explicit SSO opt-out')
    e=pit_update(e,'Included')
    body={'payrollComponentId':e['payrollComponentId'],'amount':10,'remarks':PREFIX+'Manual'}
    ml=api('POST',f'employee-payrolls/{pid}/lines',body,201)
    check(ml['pitIncomeTreatmentSnapshot']=='Included' and ml['sourceType']=='Manual' and ml['sourceId'] is None,'manual server-owned snapshot and provenance')
    e=pit_update(e,'Unknown')
    ml=api('PUT',f"employee-payrolls/{pid}/lines/{ml['employeePayrollLineId']}",body)
    check(ml['pitIncomeTreatmentSnapshot']=='Included','same-component manual update preserves PIT snapshot')
    body['payrollComponentId']=x['payrollComponentId']
    ml=api('PUT',f"employee-payrolls/{pid}/lines/{ml['employeePayrollLineId']}",body)
    check(ml['pitIncomeTreatmentSnapshot']=='Excluded','changed manual component refreshes PIT snapshot')
    api('POST',f'employee-payrolls/{pid}/lines',body|{'pitIncomeTreatmentSnapshot':'Included'},400);check(True,'manual create snapshot forgery rejected HTTP400')
    api('PUT',f"employee-payrolls/{pid}/lines/{ml['employeePayrollLineId']}",body|{'pitIncomeTreatmentSnapshot':'Included'},400);check(True,'manual update snapshot forgery rejected HTTP400')
    api('DELETE',f"employee-payrolls/{pid}/lines/{ml['employeePayrollLineId']}",status=204)
    check(totals(detail(pid)['payroll'])==totals(regen['payroll']),'manual cleanup restores payroll totals')

    profile=api('PUT',f'employees/{EMP}/tax-profile',{'taxpayerIdentificationNumber':PREFIX+'Sensitive identifier'});profile_id=profile['employeeTaxProfileId']
    d=declaration(2026)
    for residence,kind in [('Unknown','Unknown'),('NonResident','RequiresReview'),('Resident','StandardSection40_1')]:
        td=treatment(d,residence,kind)['treatment']
        check(td['residencyStatus']==residence and td['employmentTaxTreatment']==kind,'Draft treatment persists '+residence+'/'+kind)
        stored=rows('SELECT ResidencyStatus,EmploymentTaxTreatment FROM EmployeeTaxDeclarations WHERE EmployeeTaxDeclarationId='+ident(d))[0]
        check(stored['ResidencyStatus']==residence and stored['EmploymentTaxTreatment']==kind,'SQL treatment persists '+residence+'/'+kind)
    check(resolution(2026)['status']=='Unresolved','unverified treatment cannot authorize future PIT')
    api('POST',f'employees/{EMP}/tax-declarations',{'taxYear':2026},409);check(True,'duplicate same-year Draft prevented')
    treatment(d,'invalid','StandardSection40_1',400);treatment(d,'Resident','invalid',400);check(True,'invalid treatment states rejected HTTP400')
    verified=verify(d,2026);check(verified['treatment']['status']=='Verified' and verified['treatment']['verifiedAt'] is not None,'explicit verification timestamp captured')
    # Compare persisted GET snapshots; mutation responses still hold in-memory UTC DateTime kinds.
    verified=api('GET',f'employees/{EMP}/tax-declarations/{d}')
    check(resolution(2026)['status']=='Approved' and resolution(2026)['treatment']['employeeTaxDeclarationId']==d,'verified standard treatment resolves exact revision')
    check(resolution(2027)['status']=='Unresolved','treatment belongs only to its Gregorian tax year')
    treatment(d,'Resident','RequiresReview',409);check(True,'Verified treatment immutable HTTP409')
    original_verified_row=rows('SELECT * FROM EmployeeTaxDeclarations WHERE EmployeeTaxDeclarationId='+ident(d))
    replacement=declaration(2026);check(api('GET',f'employees/{EMP}/tax-declarations/{replacement}')['declaration']['replacesDeclarationId']==d,'correction uses existing revision chain')
    treatment(replacement,'Resident','RequiresReview');check(resolution(2026)['treatment']['employeeTaxDeclarationId']==d,'replacement Draft does not replace verified selection')
    verify(replacement,2026);check(resolution(2026)['status']=='Blocked' and resolution(2026)['treatment']['employeeTaxDeclarationId']==replacement,'verified RequiresReview switches selection and stays blocked')
    historical=api('GET',f'employees/{EMP}/tax-declarations/{d}')
    expected_historical=json.loads(json.dumps(verified));expected_historical['declaration']['isCurrentVerified']=False
    check(historical==expected_historical and rows('SELECT * FROM EmployeeTaxDeclarations WHERE EmployeeTaxDeclarationId='+ident(d))==original_verified_row,'correction preserves historical Verified row; only derived current-selection flag changes')
    other=declaration(2027);treatment(other,'NonResident','StandardSection40_1');verify(other,2027)
    check(resolution(2027)['status']=='Approved','explicit NonResident standard treatment accepted without nationality inference')
    unknown=declaration(2028);verify(unknown,2028);check(resolution(2028)['status']=='Unresolved','Verified Unknown never becomes standard approval')
    # Nationality changes are temporary input fixtures, not tax decisions; restore the exact employee row below.
    for nationality in ['Filipino','Thai']:
        nationality_id=rows('SELECT Id FROM Nationalities WHERE Name='+literal(nationality))[0]['Id']
        sql('UPDATE Employees SET NationalityId='+ident(nationality_id)+' WHERE EmployeeId='+ident(EMP))
        check(resolution(2027)['status']=='Approved','nationality-independent verified StandardSection40_1: '+nationality)
        check(resolution(2026)['status']=='Blocked','nationality-independent RequiresReview: '+nationality)
        check(resolution(2028)['status']=='Unresolved','nationality-independent Unknown blocked: '+nationality)
    restore('Employees','EmployeeId',original_employee)
    check(rows('SELECT COUNT(*) AS N FROM EmployeeTaxDeclarationSelections WHERE EmployeeId='+ident(EMP))[0]['N']==3,'one selected revision per configured tax year')
    broad=json.dumps(api('GET','employees?page=1&pageSize=20')).lower()
    check('residencystatus' not in broad and 'employmenttaxtreatment' not in broad and 'taxpayeridentification' not in broad and 'sensitive identifier' not in broad,'taxpayer/treatment fields absent from broad employee list')
    swagger=json.loads(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json').read())
    check('/api/employees/{employeeId}/tax-treatment' in swagger['paths'] and '/api/employees/{employeeId}/tax-declarations/{id}/treatment' in swagger['paths'],'focused treatment endpoints documented in Swagger')
    check('pitIncomeTreatment' in swagger['components']['schemas']['PayrollComponentRequest']['properties'],'Swagger writable component classification')
    check('pitIncomeTreatmentSnapshot' not in swagger['components']['schemas']['EmployeePayrollLineRequest']['properties'],'Swagger does not expose writable snapshot')
    for table,key,idvalue,field in [('PayrollComponents','Id',e['payrollComponentId'],'PitIncomeTreatment'),('EmployeePayrollLines','EmployeePayrollLineId',regen['lines'][0]['employeePayrollLineId'],'PitIncomeTreatmentSnapshot'),('EmployeeTaxDeclarations','EmployeeTaxDeclarationId',other,'ResidencyStatus'),('EmployeeTaxDeclarations','EmployeeTaxDeclarationId',other,'EmploymentTaxTreatment')]:
        try:sql(f"SET XACT_ABORT ON; BEGIN TRANSACTION; UPDATE [{table}] SET [{field}]='INVALID' WHERE [{key}]={ident(idvalue)}; ROLLBACK;")
        except AssertionError:check(True,'SQL rejects invalid '+field)
        else:raise AssertionError('SQL accepted invalid '+field)
except Exception as exc:
    error=str(exc);traceback.print_exc()
finally:
    try:
        cleanup()
        if profile_id:sql('DELETE EmployeeTaxProfiles WHERE EmployeeTaxProfileId='+ident(profile_id))
        restore('Employees','EmployeeId',original_employee)
        for row in original_components:restore('PayrollComponents','Id',row)
        final=snapshot();check(final==baseline,'exact contents/timestamps of all 57 tables restored')
        check(rows('SELECT IsActive FROM Employees WHERE EmployeeId='+ident(EMP))[0]['IsActive']==False,'TEST-EMP-001 remains inactive')
        check(all(c['PitIncomeTreatment']=='Unknown' for c in rows('SELECT PitIncomeTreatment FROM PayrollComponents')),'all baseline PIT classifications restored Unknown')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':results,'error':error,'migration':locals().get('migration'),'schema':locals().get('schema'),'constraints':locals().get('constraints'),'finalCounts':{t:len(v) for t,v in locals().get('final',{}).items()}},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} D6B live assertions; exact baseline restored.',flush=True)
