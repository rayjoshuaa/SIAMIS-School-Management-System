"""Reuse existing D1/D8B/D8C/D8D/Payroll scenarios; only obsolete table counts are adapted.
No expected monetary, leave or lifecycle result is changed. localhost/SIAMIS only.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
baseline=snapshot();error=None;totals={}
def run(name,code):
    path=ROOT/'tests'/name; runner=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/('d9b-'+name)
    runner.write_text('__file__='+repr(str(path))+'\n'+code,encoding='utf-8')
    log=runner.with_suffix('.log')
    try:
        with log.open('w',encoding='utf-8') as output: p=subprocess.run(['python',str(runner)],cwd=ROOT,stdout=output,stderr=subprocess.STDOUT)
    finally:runner.unlink(missing_ok=True)
    if p.returncode:raise AssertionError(name+': '+log.read_text(encoding='utf-8')[-5000:])
    check(snapshot()==baseline,name+' exact baseline restoration')
try:
    check(len(baseline)==74 and not baseline['AttendanceEvents'],'D9B 74-table regression baseline')
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8d-before-migration.json').write_text(json.dumps(baseline),encoding='utf-8')
    for name in ['verify_d8d_regressions.py','verify_d8d_live.py','verify_d8d_capped_focus.py']:
        code=(ROOT/'tests'/name).read_text(encoding='utf-8').replace('len(baseline)==73','len(baseline)==74')
        run(name,code)
        result=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'/name.replace('verify_','').replace('.py','-results.json').replace('_','-')).read_text())
        totals[name]={'checks':result['checks'],'nestedTotals':result.get('totals',{})}
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:check(snapshot()==baseline,'exact baseline after D9B prior suites')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc)
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d9b-regressions-results.json').write_text(json.dumps({'totals':totals,'checks':len(results),'error':error},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS: D9B prior regression totals',totals,flush=True)
