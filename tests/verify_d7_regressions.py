"""Run existing live regressions with D7's required synthetic employer configuration."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
baseline=snapshot();error=None;completed=[]
try:
    if baseline['OrganizationProfiles']:raise AssertionError('Expected unconfigured Development employer baseline')
    api('PUT','organization-profile',{'displayName':'D7-REGRESSION Synthetic Employer'})
    # D6E's pre-migration baseline contract now includes the two D7 tables and employer fixture.
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6e-before-migration.json').write_text(json.dumps(snapshot()),encoding='utf-8')
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d6d-before-migration.json').write_text(json.dumps(snapshot()),encoding='utf-8')
    for name in ['verify_d5a_live.py','verify_d5c_live.py','verify_d6b_live.py','verify_d6c_live.py','verify_d6d_live.py','verify_d6e_live.py','verify_d6e_boundaries_live.py']:
        log=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/('d7-regression-'+name+'.log')
        with log.open('w',encoding='utf-8') as out:
            run=subprocess.run(['python',str(ROOT/'tests'/name)],stdout=out,stderr=subprocess.STDOUT,cwd=ROOT)
        if run.returncode:raise AssertionError(name+' failed: '+log.read_text(encoding='utf-8')[-4000:])
        completed.append(name);print('PASS:',name,flush=True)
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        sql("DELETE OrganizationProfiles WHERE OrganizationProfileId='00000000-0000-0000-0000-000000000001'")
        check(snapshot()==baseline,'Exact Development baseline after all regression suites')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc)
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d7-regressions-results.json').write_text(json.dumps({'completed':completed,'error':error},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
