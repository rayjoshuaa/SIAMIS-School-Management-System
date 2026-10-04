"""Fail-closed D9D mutations against a locally started Production-mode API (not a production database)."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
baseline=snapshot();BASE='http://localhost:5156';error=None
try:
    body={'expectedVersion':0,'expectedSourceFingerprint':'0'*64,'reason':'D9D production guard verification'}
    for route,extra in [('corrections',{'occurredAt':'2030-01-07T07:30:00+07:00','direction':'In','manualRequestKey':str(uuid.uuid4())}),('adjudications',{'attendanceEventId':str(uuid.uuid4()),'included':False}),('confirm-absence',{}),('finalize',{}),('reopen',{})]:
        api('POST',f'employees/{EMP}/attendance-days/2030-01-07/{route}',body|extra,404)
        check(True,'Production fails closed '+route)
    check(snapshot()==baseline,'Production guard calls have no database mutations')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d9d-production-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2))
if error:raise SystemExit(error)
print('PASS:',len(results),'Production guard checks')
