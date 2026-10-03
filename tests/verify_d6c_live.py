"""D6C safe-foundation live checks. Synthetic values are not legal policy values.
Every fixture is tracked; all 57 application tables are compared after cleanup.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6c-live-results.json'
PREFIX='D6C-VERIFY-'
baseline=snapshot(); policies=[]; error=None
def decl(year=2026, count=None):
    body={'taxYear':year,'remarks':PREFIX+'Complete reviewed employee facts'}
    if count is not None: body['totalLivingLawfulChildren']=count
    d=api('POST',f'employees/{EMP}/tax-declarations',body,201)['declaration']['employeeTaxDeclarationId']
    fixtures['declarations'].append(d);return d
def path(d): return f'employees/{EMP}/tax-declarations/{d}'
def opening(d,state='Unknown',scope=None,complete=False,date='2025-12-31',value=0,status=200):
    b={'state':state,'currency':'THB','asOfDate':date,'completenessAttested':complete}
    if scope is not None:b['openingBalanceScope']=scope
    if state!='Unknown':b.update(priorTaxableEmploymentIncome=value,priorTaxWithheld=0,priorSocialSecurityContribution=0,remarks=PREFIX+'Current employer complete history')
    return api('PUT',path(d)+'/opening-balance',b,status)
try:
    check(len(baseline)==57 and len(baseline['Employees'])==1 and len(baseline['PayrollComponents'])==17,'baseline checked')
    checks=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name IN ('CK_PitPolicyConfigurations_ClaimValues','CK_EmployeeTaxOpeningBalances_Scope','CK_EmployeeTaxDeclarations_LivingChildren','CK_EmployeeTaxClaims_ChildMetadata')")
    check(len(checks)==4 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in checks),'four new checks enabled/trusted')
    scheme=api('POST','statutory-schemes',{'code':'TH-PIT','name':PREFIX+'Synthetic','jurisdiction':'TH','schemeType':'PersonalIncomeTax'},201)['statutorySchemeId'];fixtures['schemes'].append(scheme)
    p=api('POST','statutory-policy-versions',{'statutorySchemeId':scheme,'version':PREFIX,'effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','currency':'THB','officialReference':'Synthetic test only','calculationMethodVersion':'PIT-TH-V1'},201)['statutoryPolicyVersionId'];policies.append(p)
    pp='statutory-policy-versions/'+p
    config={'taxYear':2026,'employmentExpenseDeductionRate':10,'employmentExpenseDeductionCap':100,'personalAllowanceAmount':1,'spouseAllowanceAmount':2,'childAllowanceAmount':3,'additionalChildAllowanceAmount':4,'parentAllowanceAmount':5,'adoptedChildCombinedCountLimit':2,'maximumEligibleParentCount':2,'withholdingMethodIdentifier':'Synthetic','brackets':[{'sortOrder':1,'lowerBoundInclusive':0,'upperBoundExclusive':10,'rate':0},{'sortOrder':2,'lowerBoundInclusive':10,'rate':10}]}
    saved=api('PUT',pp+'/personal-income-tax',config)['personalIncomeTax']
    check(all(saved[k]==v for k,v in config.items() if k!='brackets') and saved['rateUnit']=='PercentagePoints','all independent policy fields persist/response units explicit')
    stored=rows('SELECT * FROM PitPolicyConfigurations WHERE StatutoryPolicyVersionId='+ident(p))[0]
    check(stored['ChildAllowanceAmount']==3 and stored['AdditionalChildAllowanceAmount']==4 and stored['AdoptedChildCombinedCountLimit']==2,'SQL typed policy amounts/count limits')
    for key,value in [('spouseAllowanceAmount',-1),('parentAllowanceAmount',-1),('childAllowanceAmount',-1),('employmentExpenseDeductionRate',101),('employmentExpenseDeductionCap',0),('maximumEligibleParentCount',0),('adoptedChildCombinedCountLimit',0)]:
        api('PUT',pp+'/personal-income-tax',{**config,key:value},400);check(True,'reject invalid '+key)
    bad={**config,'brackets':[{'sortOrder':1,'lowerBoundInclusive':0,'upperBoundExclusive':10,'rate':0},{'sortOrder':2,'lowerBoundInclusive':9,'rate':10}]}
    api('PUT',pp+'/personal-income-tax',bad,400);check(True,'Draft overlap rejected')
    api('PUT',pp+'/personal-income-tax',{**config,'spouseAllowanceAmount':None})
    check('policy-owned' in json.dumps(api('POST',pp+'/publish',{},400)),'missing V1 parameter blocks publication')
    api('PUT',pp+'/personal-income-tax',config)
    published=api('POST',pp+'/publish',{})
    check(published['status']=='Published' and published['pitSsoRecognition']=={'mode':'ActualCumulative','historicalPayrollStatus':'Paid','includesCurrentPayrollEmployeeAmount':True,'projectsFutureContributions':False,'hasIndependentPitCap':False},'complete synthetic policy publishes with approved recognition metadata')
    api('PUT',pp+'/personal-income-tax',config,409);check(True,'Published PIT parameters/brackets immutable')
    api('DELETE',pp,status=409);check(True,'Published PIT hard deletion prohibited')
    api('PUT',pp,{'version':'changed','effectiveFrom':'2026-01-01','effectiveTo':'2026-12-31','officialReference':'changed','currency':'THB'},409);check(True,'Published PIT metadata/reference immutable')
    d=decl(count=2)
    def claim(body,status=201):return api('POST',path(d)+'/claims',body,status)
    for kind in ['Spouse','Child','Parent']:
        claim({'claimType':kind,'amount':1,'quantity':1,'reference':'Synthetic'},400);check(True,kind+' client amount rejected')
    spouse=claim({'claimType':'Spouse','reference':PREFIX+'Spouse attestation'})
    check(spouse['claims'][0]['quantity']==1 and spouse['claims'][0]['amount'] is None,'Spouse presence normalized; no legal amount')
    claim({'claimType':'Spouse','reference':'Synthetic'},400);check(True,'duplicate spouse rejected')
    claim({'claimType':'Child','quantity':1,'reference':'Synthetic'},400);check(True,'untyped child rejected')
    claim({'claimType':'Child','quantity':1,'reference':'Synthetic','childRelationshipType':'Adopted','additionalChildAllowanceEligible':True},400);check(True,'adopted additional allowance rejected')
    for relation,additional in [('Lawful',False),('Lawful',True),('Adopted',False)]:
        response=claim({'claimType':'Child','quantity':1,'reference':PREFIX+relation,'childRelationshipType':relation,'additionalChildAllowanceEligible':additional})
        check(any(c['childRelationshipType']==relation and c['additionalChildAllowanceEligible']==additional for c in response['claims']),'typed child persists '+relation+str(additional))
    claim({'claimType':'Parent','quantity':2,'reference':PREFIX+'Reviewed eligible parents'})
    claim({'claimType':'Parent','quantity':0,'reference':'Synthetic'},400);check(True,'invalid parent quantity rejected')
    claim({'claimType':'Donation','quantity':1,'reference':'Synthetic'},400);check(True,'unsupported deduction rejected')
    claim({'claimType':'Parent','quantity':1},400);check(True,'evidence required')
    opening(d,'ConfirmedZero',complete=True,status=400);check(True,'known opening scope cannot be inferred')
    opening(d,'ConfirmedZero','PreviousEmployer',True,status=400);check(True,'previous employer rejected for review')
    opening(d,'ConfirmedZero','CurrentEmployer',False,status=400);check(True,'completeness explicit')
    opening(d,'VerifiedAmount','CurrentEmployer',True,value=1,status=400);check(True,'prior December31 cannot carry positive new-year amounts')
    known=opening(d,'VerifiedAmount','CurrentEmployer',True,date='2026-03-31',value=123)
    check(known['openingBalance']['priorTaxableEmploymentIncome']==123 and known['openingBalance']['inputContractVersion']=='PIT-TH-V1','pre-expense contract version/scoped amount persists')
    api('POST',path(d)+'/verify',{})
    persisted=api('GET',path(d))
    check(persisted['declaration']['isCurrentVerified'] and len(persisted['claims'])==5,'complete reviewed selected revision retains all categories')
    claim({'claimType':'Parent','quantity':1,'reference':'Synthetic'},409);check(True,'Verified claims immutable')
    opening(d,'Unknown',status=409);check(True,'Verified opening immutable')
    replacement=decl(count=2)
    check(api('GET',path(replacement))['openingBalance'] is None,'replacement never inherits opening')
    api('POST',path(replacement)+'/verify',{},400);check(True,'missing opening prevents replacement selection')
    check(api('GET',path(d))==persisted,'failed verification preserves exact old revision/selection')
    opening(replacement)
    api('POST',path(replacement)+'/verify',{})
    new=api('GET',path(replacement))
    check(new['claims']==[] and new['openingBalance']['state']=='Unknown' and new['openingBalance']['priorTaxableEmploymentIncome'] is None,'absent claims explicit none; Unknown never implicit zero')
    old=api('GET',path(d));expected=json.loads(json.dumps(persisted));expected['declaration']['isCurrentVerified']=False
    check(old==expected,'correction changes selection only; verified historical facts unchanged')
    sqlcheck=rows('SELECT ChildRelationshipType,AdditionalChildAllowanceEligible,Amount FROM EmployeeTaxClaims WHERE EmployeeTaxDeclarationId='+ident(d))
    check(len(sqlcheck)==5 and all(c.get('Amount') is None for c in sqlcheck),'SQL claims have no client legal amounts')
    swagger=json.loads(urllib.request.urlopen(BASE+'/swagger/v1/swagger.json').read())['components']['schemas']
    check('childRelationshipType' in swagger['EmployeeTaxClaimRequest']['properties'] and 'openingBalanceScope' in swagger['EmployeeTaxOpeningBalanceRequest']['properties'],'Swagger exposes typed writable facts')
    check('inputContractVersion' not in swagger['EmployeeTaxOpeningBalanceRequest']['properties'],'meaning version is server-owned')
    check(not rows('SELECT EmployeePayrollId FROM EmployeePayrolls') and not rows('SELECT EmployeePayrollLineId FROM EmployeePayrollLines'),'no payroll header/line/PIT money produced')
except Exception as exc:
    error=str(exc);traceback.print_exc()
finally:
    try:
        for p in policies:
            sql('DELETE PitTaxBrackets WHERE StatutoryPolicyVersionId='+ident(p)+'; DELETE PitPolicyConfigurations WHERE StatutoryPolicyVersionId='+ident(p))
        cleanup()
        final=snapshot();check(final==baseline,'exact contents/timestamps of all 57 application tables restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    OUT.write_text(json.dumps({'checks':results,'error':error,'finalCounts':{t:len(v) for t,v in locals().get('final',{}).items()}},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} D6C live assertions; exact baseline restored.',flush=True)
