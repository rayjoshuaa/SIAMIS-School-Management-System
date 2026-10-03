"""Prior D1/D8B/D8C/Payroll scenarios with D8D schema counts and explicitly approved prerequisite changes.
Original live scenario files remain unchanged. Monetary formulas/expectations are never adapted.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
baseline=snapshot();error=None;totals={}
def run(path,code,label):
    log=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/('d8d-regression-'+label+'.log')
    # A file avoids Windows' command-line size limit for the complete D8C suite.
    script=log.with_suffix('.runner.py');script.write_text('__file__='+repr(str(path))+'\n'+code,encoding='utf-8')
    try:
        with log.open('w',encoding='utf-8') as out:p=subprocess.run(['python',str(script)],cwd=ROOT,stdout=out,stderr=subprocess.STDOUT)
    finally:script.unlink(missing_ok=True)
    if p.returncode:raise AssertionError(label+': '+log.read_text(encoding='utf-8')[-5000:])
    check(snapshot()==baseline,label+' exact baseline restoration')
    print('PASS:',label,flush=True)
def cleanup_lines():
    return '''
            cs=f'SELECT Id FROM EmployeeLeaveSandwichCases WHERE EmployeeId IN ({ids})'
            statements += [f'DELETE EmployeeLeaveSandwichEvents WHERE CaseId IN ({cs})',f'DELETE EmployeeLeaveSandwichDates WHERE CaseId IN ({cs})',f'DELETE EmployeeLeaveSandwichAllocations WHERE CaseId IN ({cs})',f'DELETE EmployeeLeaveSandwichCases WHERE EmployeeId IN ({ids})',f'DELETE EmployeeLeaveApprovalEvidence WHERE EmployeeId IN ({ids})',f'DELETE EmployeeLeaveEvidenceEvents WHERE EvidenceId IN (SELECT Id FROM EmployeeLeaveEvidence WHERE EmployeeId IN ({ids}))']
            receipts=rows(f'SELECT Id FROM EmployeeLeaveEvidence WHERE EmployeeId IN ({ids}) ORDER BY RecordedAt DESC')
            statements += ['DELETE EmployeeLeaveEvidence WHERE Id='+ident(x['Id']) for x in receipts]
'''
try:
    check(len(baseline)==73 and not baseline['EmployeeLeave'],'D8D prior regression baseline')
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-before-migration.json').write_text(json.dumps(baseline),encoding='utf-8')
    path=ROOT/'tests/verify_d8b_live.py';code=path.read_text(encoding='utf-8').replace('len(baseline)==65','len(baseline)==73').replace('len(constraints)==16','len(constraints)==20')
    code=code.replace("'sandwichParticipation':True}","'sandwichParticipation':True,'sandwichEquivalentDayMinutes':300}")
    start=code.index("    l=api('POST',f'employees/{eid}/leave'");end=code.index('    # Swagger includes',start)
    code=code[:start]+"    # D8C authoritative suite replaces the intentionally removed legacy mutation routes.\n"+code[end:]
    run(path,code,'D8B-D1');totals['D8B/D1']=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-live-results.json').read_text())['checks']
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-before-migration.json').write_text(json.dumps(baseline),encoding='utf-8')
    path=ROOT/'tests/verify_d8c_live.py';code=path.read_text(encoding='utf-8').replace('len(baseline)==66','len(baseline)==73')
    code=code.replace('sandwichParticipation=True)', 'sandwichParticipation=True,sandwichEquivalentDayMinutes=300)')
    old="    command(e,sudden,'approve');check(True,'approval does not block for missing medical file');cancel(e,sudden,True)"
    new="""    command(e,sudden,'approve',status=409);check(True,'D8D blocks missing Accepted evidence without binary requirement')
    receipt=api('POST',f"employees/{e}/leave/{sudden['leaveId']}/evidence",{'documentTypeId':doc,'externalReference':'SYNTHETIC-D8C-REGRESSION'},201)
    api('POST',f"employees/{e}/leave/{sudden['leaveId']}/evidence/{receipt['id']}/accept",{'reviewRemarks':'Synthetic external review'})
    command(e,sudden,'approve');check(True,'approval with Accepted external evidence, no medical binary');cancel(e,sudden,True)"""
    assert old in code;code=code.replace(old,new)
    old="            statements += [f'DELETE EmployeeLeaveAllocations"
    assert old in code;code=code.replace(old,cleanup_lines()+old)
    run(path,code,'D8C');totals['D8C']=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-live-results.json').read_text())['checks']
    path=ROOT/'tests/verify_d8c_transition_race.py';code=path.read_text(encoding='utf-8').replace('len(baseline)==66','len(baseline)==73')
    run(path,code,'D8C-ExpectedStatus');totals['D8C ExpectedStatus']=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-transition-race-results.json').read_text())['checks']
    path=ROOT/'tests/verify_d8b_regressions.py';code=path.read_text(encoding='utf-8').replace('len(baseline)==65','len(baseline)==73')
    run(path,code,'Payroll');totals['Payroll']=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-regressions-results.json').read_text())['totals']
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:check(snapshot()==baseline,'Exact 73-table baseline after prior suites')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc)
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8d-regressions-results.json').write_text(json.dumps({'totals':totals,'checks':len(results),'error':error},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS: D8D prior regressions',totals)
