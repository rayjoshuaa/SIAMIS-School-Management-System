"""Fresh D10/D11/D12 security and HR compatibility verification for D14."""
import pathlib,subprocess,json
ROOT=pathlib.Path(__file__).resolve().parents[1]
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
totals={}
for name,result in [('verify_d13_live.py','d13-live-results.json'),('verify_d13_production.py','d13-production-results.json'),('verify_d12_live.py','d12-live-results.json'),('verify_d11_live.py','d11-live-results.json'),('verify_d10_security.py','d10-security-results.json'),('verify_d10_production.py','d10-production-results.json'),('verify_d10_advanced.py','d10-advanced-results.json'),('verify_d10_route_security.py','d10-route-security-results.json'),('verify_d13_rate_limit.py','d13-rate-limit-results.json')]:
    with (directory/('d14-'+name+'.log')).open('w',encoding='utf-8') as log:
        p=subprocess.run(['python',str(ROOT/'tests'/name)],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT)
    if p.returncode:raise SystemExit(name+' failed; inspect its D14 log')
    data=json.loads((directory/result).read_text());assert not data.get('error'),name
    totals[name]=data['checks'];print('PASS:',name,data['checks'],flush=True)
(directory/'d14-security-regressions-results.json').write_text(json.dumps({'totals':totals,'error':None},indent=2))
