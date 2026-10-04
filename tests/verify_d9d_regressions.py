"""D9D prior regression verification. Only obsolete baseline table counts are adapted.
Original scenario files and monetary/Leave expectations remain unchanged. localhost/SIAMIS only.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
baseline=snapshot();error=None;totals={}
def run(name,code):
    path=ROOT/'tests'/name;runner=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/('d9d-'+name)
    runner.write_text('__file__='+repr(str(path))+'\n'+code,encoding='utf-8');log=runner.with_suffix('.log')
    try:
        with log.open('w',encoding='utf-8') as output:p=subprocess.run(['python',str(runner)],cwd=ROOT,stdout=output,stderr=subprocess.STDOUT)
    finally:runner.unlink(missing_ok=True)
    if p.returncode:raise AssertionError(name+': '+log.read_text(encoding='utf-8')[-5000:])
    check(snapshot()==baseline,name+' exact baseline restoration')
try:
    check(len(baseline)==77 and not baseline['AttendanceEvents'],'D9D 77-table prior regression baseline')
    for name in ['verify_d9b_live.py','verify_d9c_live.py','verify_d9b_regressions.py']:
        code=(ROOT/'tests'/name).read_text().replace('len(baseline)==74','len(baseline)==77')
        # Nested D8 wrapper adapts original count 73; preserve its existing scenario adaptations.
        if name=='verify_d9b_regressions.py':code=code.replace(".replace('len(baseline)==73','len(baseline)==74')",".replace('len(baseline)==73','len(baseline)==77')")
        run(name,code)
        result=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/name.replace('verify_','').replace('.py','-results.json').replace('_','-')).read_text())
        totals[name]={'checks':result['checks'],'nestedTotals':result.get('totals',{})}
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:check(snapshot()==baseline,'exact baseline after all D9D prior suites')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc)
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d9d-regressions-results.json').write_text(json.dumps({'totals':totals,'checks':len(results),'error':error},indent=2))
if error:raise SystemExit(error)
print('PASS: D9D prior regressions',totals,flush=True)
