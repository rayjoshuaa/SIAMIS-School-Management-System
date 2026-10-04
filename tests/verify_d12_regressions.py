"""Authenticated D12 domain regressions, with isolated temporary SystemAdmin and exact 85-table cleanup."""
import pathlib, json, os, subprocess, secrets, traceback
ROOT=pathlib.Path(__file__).resolve().parents[1]
source=ROOT/'tests/verify_d10_security.py'
exec(source.read_text().split('baseline=snapshot();')[0])
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')};error=None;totals={}
previous_path=directory/'d12-regressions-results.json'
previous=json.loads(previous_path.read_text()) if previous_path.exists() else {}
hook=directory/'d10-hook';hook.mkdir(exist_ok=True)
(hook/'sitecustomize.py').write_text((ROOT/'tests/d10_regression_hook.py').read_text(),encoding='utf-8')
try:
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-regression';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    if p.returncode:raise AssertionError('Temporary regression bootstrap failed')
    user=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-regression'")[0]['Id'];fixtures['users'].append(user)
    client=Client();client.login(PREFIX+'-regression');client.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    env['SIAMIS_TEST_ROOT']=str(ROOT);env['SIAMIS_TEST_USERNAME']=PREFIX+'-regression';env['SIAMIS_TEST_PASSWORD']=new_password;env['PYTHONPATH']=str(hook)
    # Existing wrapper performs D8B/D1, D8C, concurrency, D8D/sandwich and all payroll scenarios.
    for name in ['verify_d9d_live.py','verify_d9b_live.py','verify_d9c_live.py','verify_d9e_live.py','verify_d8d_regressions.py','verify_d8d_live.py','verify_d8d_capped_focus.py']:
        if name in previous.get('totals',{}):
            totals[name]=previous['totals'][name]
            print('PASS: previously completed authenticated suite',name,flush=True)
            continue
        path=ROOT/'tests'/name
        runner=directory/('d12-'+name)
        runner.write_text("__file__="+repr(str(path))+"\nimport pathlib\nexec(compile(pathlib.Path(__file__).read_text(),__file__,'exec'))",encoding='utf-8')
        try:
            with runner.with_suffix('.log').open('w',encoding='utf-8') as log:p=subprocess.run(['python',str(runner)],cwd=ROOT,env=env,stdout=log,stderr=subprocess.STDOUT)
        finally:runner.unlink(missing_ok=True)
        if p.returncode:raise AssertionError(name+' failed: '+runner.with_suffix('.log').read_text(encoding='utf-8')[-3000:])
        result_name=name.replace('verify_','').replace('.py','-results.json').replace('_','-')
        data=json.loads((directory/result_name).read_text());totals[name]={'checks':data['checks'],'nestedTotals':data.get('totals',{})}
        current=snapshot()
        check(all(current[t]==baseline[t] for t in baseline if t not in ['Users','UserRoles','SecurityAuditEvents']),'exact HR baseline after '+name)
        print('PASS:',name,totals[name],flush=True)
    events=rows('SELECT * FROM SecurityAuditEvents WHERE ActorUserId='+ident(user))
    if 'verify_d9d_live.py' not in previous.get('totals',{}):
        check(any(e['ResourceType']=='FinalizedAttendanceRevision' for e in events),'authenticated Attendance finalization audit in regression')
    if 'verify_d8d_live.py' not in previous.get('totals',{}):
        check(any(e['ResourceType']=='EmployeeLeave' for e in events),'authenticated Leave lifecycle audit in regression')
    if 'verify_d8d_live.py' not in previous.get('totals',{}):
        check(any(e['ResourceType']=='EmployeeLeaveSandwichEvent' for e in events),'authenticated sandwich review audit in regression')
    if 'verify_d8d_regressions.py' not in previous.get('totals',{}):
        check(any(e['ResourceType']=='EmployeePayroll' for e in events),'authenticated Payroll lifecycle audit in regression')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:cleanup();check(snapshot()==baseline,'exact 85-table baseline after all authenticated regressions')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d12-regressions-results.json').write_text(json.dumps({'checks':len(results),'error':error,'totals':totals},indent=2),encoding='utf-8')
    (hook/'sitecustomize.py').unlink(missing_ok=True)
if error:raise SystemExit(error)
print('PASS: all D12 authenticated regressions; exact cleanup',flush=True)
