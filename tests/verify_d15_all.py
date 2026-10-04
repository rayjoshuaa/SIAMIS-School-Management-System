"""Run all established D15 closure regressions fresh, sequentially, on local SIAMIS only."""
import pathlib, subprocess, json, time
ROOT=pathlib.Path(__file__).resolve().parents[1]
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
commands=[
 ('pure',['dotnet','run','--project','tests/SIAMIS.Payroll.RegressionTests','-c','Release','--no-build']),
 ('domains',['python','tests/verify_d15_regressions.py']),
 ('security',['python','tests/verify_d14_security_regressions.py']),
 ('documents',['python','tests/verify_d14_live.py']),
 ('storage-faults',['dotnet','run','--project','tests/SIAMIS.Payroll.RegressionTests','-c','Release','--no-build','--','--d14-database-tests']),
 ('storage-configuration',['python','tests/verify_d14_configuration.py']),
 ('employee-boundaries',['python','tests/verify_d15_employee.py']),
 ('cross-domain',['python','tests/verify_d15_cross_domain.py']),
 ('role-matrix',['python','tests/verify_d15_routes.py']),
 ('safe-errors',['python','tests/verify_d15_failures.py']),
 ('history-privacy',['python','tests/verify_d15_history_privacy.py']),
 ('final-baseline',['python','tests/verify_d14_baseline.py']),
]
completed=[]
for label,command in commands:
    if label=='documents':
        print('Wait for the unchanged one-minute credential/login rate windows after security probes.',flush=True)
        time.sleep(60)
    with (directory/('d15-suite-'+label+'.log')).open('w',encoding='utf-8') as log:
        p=subprocess.run(command,cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
    if p.returncode:
        (directory/'d15-all-results.json').write_text(json.dumps({'completed':completed,'error':label},indent=2))
        raise SystemExit('FAILED '+label+'; inspect ignored D15 suite log')
    completed.append(label)
    print('PASS: fresh D15 suite '+label,flush=True)
(directory/'d15-all-results.json').write_text(json.dumps({'completed':completed,'error':None},indent=2))
