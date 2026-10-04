"""D14 startup configuration guards. No API requests, SQL writes, or storage writes."""
import pathlib, subprocess, json
ROOT=pathlib.Path(__file__).resolve().parents[1]
checks=[]
for configured,label in [(str(ROOT/'src/SIAMIS.Api/wwwroot/private'),'public-root'),('relative-private','relative-root')]:
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--environment','Production','--PrivateDocuments:Root',configured],cwd=ROOT/'src/SIAMIS.Api',capture_output=True,text=True,timeout=20)
    expected='isolated from the public web root' if label=='public-root' else 'must be an absolute private directory'
    assert p.returncode!=0 and expected in p.stdout+p.stderr,label
    checks.append(label);print('PASS: D14 rejects '+label,flush=True)
(ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d14-configuration-results.json').write_text(json.dumps({'checks':len(checks),'error':None,'results':checks},indent=2))
