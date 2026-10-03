"""Run unchanged payroll scenarios with D8B's 65-table baseline precondition.
Only obsolete table-count expectations are adapted in memory; original regression files are unchanged.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
baseline=snapshot(); error=None; completed=[]; totals={}
try:
    check(len(baseline)==65 and not baseline['OrganizationProfiles'],'D8B full regression baseline')
    api('PUT','organization-profile',{'displayName':'D8B-REGRESSION Synthetic Employer'})
    for tag in ['d6d','d6e']:
        (ROOT/f'tests/SIAMIS.Payroll.RegressionTests/bin/{tag}-before-migration.json').write_text(json.dumps(snapshot()),encoding='utf-8')
    for name in ['verify_d5a_live.py','verify_d5c_live.py','verify_d6b_live.py','verify_d6c_live.py','verify_d6d_live.py','verify_d6e_live.py','verify_d6e_boundaries_live.py']:
        path=ROOT/'tests'/name
        log=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/('d8b-regression-'+name+'.log')
        runner="import pathlib; __file__="+repr(str(path))+"; code=pathlib.Path(__file__).read_text().replace('len(baseline)==57','len(baseline)==65'); exec(compile(code,__file__,'exec'))"
        with log.open('w',encoding='utf-8') as out:run=subprocess.run(['python','-c',runner],stdout=out,stderr=subprocess.STDOUT,cwd=ROOT)
        if run.returncode:raise AssertionError(name+' failed: '+log.read_text(encoding='utf-8')[-4500:])
        completed.append(name);print('PASS:',name,flush=True)
        tag=name.replace('verify_','').replace('_live.py','').replace('_','-')
        filename = 'd6e-boundaries-results.json' if tag == 'd6e-boundaries' else f'{tag}-live-results.json'
        resultfile=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/filename
        if resultfile.exists():
            data=json.loads(resultfile.read_text());totals[name]=len(data.get('checks',data.get('results',[])))
    sql("DELETE OrganizationProfiles WHERE OrganizationProfileId='00000000-0000-0000-0000-000000000001'")
    check(snapshot()==baseline,'Exact baseline after seven monetary suites')
    # Existing D7 operations/lifecycle/payslip regression, with a fresh read-only baseline.
    for args in [['--capture-before'],[]]:
        log=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/('d8b-d7-'+('capture' if args else 'live')+'.log')
        with log.open('w',encoding='utf-8') as out:run=subprocess.run(['python',str(ROOT/'tests/verify_d7_live.py'),*args],stdout=out,stderr=subprocess.STDOUT,cwd=ROOT)
        if run.returncode:raise AssertionError('D7 regression failed: '+log.read_text(encoding='utf-8')[-4500:])
    completed.append('verify_d7_live.py');totals['verify_d7_live.py']=len(json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d7-live-results.json').read_text())['checks'])
    print('PASS: verify_d7_live.py',flush=True)
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        sql("DELETE OrganizationProfiles WHERE OrganizationProfileId='00000000-0000-0000-0000-000000000001'")
        check(snapshot()==baseline,'Exact original 65-table baseline after all payroll regressions')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc)
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-regressions-results.json').write_text(json.dumps({'completed':completed,'totals':totals,'error':error},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
