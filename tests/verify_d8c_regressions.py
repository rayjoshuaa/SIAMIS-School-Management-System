"""Run existing D8B/D1 and monetary/lifecycle regressions after the D8C schema addition.
Only table/check counts are adapted. Legacy leave mutation tests are superseded by
the authoritative D8C suite; all calendar/policy/entitlement and monetary assertions remain.
The original regression scenario files are unchanged.
"""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
baseline=snapshot();error=None;totals={}
try:
    check(len(baseline)==66 and not baseline['EmployeeLeave'],'D8C regression baseline')
    # D8B's data preservation assertion uses the current exact read-only baseline.
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-before-migration.json').write_text(json.dumps(baseline),encoding='utf-8')
    path=ROOT/'tests/verify_d8b_live.py'
    code=path.read_text(encoding='utf-8').replace('len(baseline)==65','len(baseline)==66').replace('len(constraints)==16','len(constraints)==19')
    start=code.index("    l=api('POST',f'employees/{eid}/leave'")
    end=code.index('    # Swagger includes',start)
    code=code[:start]+"    # Legacy mutations are intentionally removed; verify_d8c_live.py covers their replacement and the existing snapshot checks.\n"+code[end:]
    runner="__file__="+repr(str(path))+"; exec(compile("+repr(code)+",__file__,'exec'))"
    log=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-d8b-regression.log'
    with log.open('w',encoding='utf-8') as out:p=subprocess.run(['python','-c',runner],cwd=ROOT,stdout=out,stderr=subprocess.STDOUT)
    if p.returncode:raise AssertionError(log.read_text(encoding='utf-8')[-5000:])
    d8b=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-live-results.json').read_text(encoding='utf-8'));totals['D8B/D1']=d8b['checks'];check(snapshot()==baseline,'D8B/D1 exact cleanup')
    # Existing D5A-D7 wrapper adapts only obsolete table counts, not calculation/lifecycle expectations.
    path=ROOT/'tests/verify_d8b_regressions.py'
    code=path.read_text(encoding='utf-8').replace('len(baseline)==65','len(baseline)==66')
    runner="__file__="+repr(str(path))+"; exec(compile("+repr(code)+",__file__,'exec'))"
    log=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-payroll-regression.log'
    with log.open('w',encoding='utf-8') as out:p=subprocess.run(['python','-c',runner],cwd=ROOT,stdout=out,stderr=subprocess.STDOUT)
    if p.returncode:raise AssertionError(log.read_text(encoding='utf-8')[-5000:])
    payroll=json.loads((ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8b-regressions-results.json').read_text(encoding='utf-8'));totals|=payroll['totals']
    check(snapshot()==baseline,'D5A-D7 exact cleanup')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:check(snapshot()==baseline,'Exact original 66-table baseline after all regressions')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc)
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-regressions-results.json').write_text(json.dumps({'totals':totals,'error':error},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS: D8C prior-regression totals',totals,flush=True)
