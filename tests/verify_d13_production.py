"""D13 production fail-closed delivery, no tokens; ONLY localhost/SIAMIS."""
import pathlib,json,os,subprocess,traceback
ROOT=pathlib.Path(__file__).resolve().parents[1]
source=(ROOT/'tests/verify_d10_production.py').read_text().split('\ntry:\n')[0]
exec(source)
error=None
try:
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-production';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode==0,'isolated Production verification bootstrap')
    uid=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-production'")[0]['Id'];fixtures['users'].append(uid)
    admin=ProxyClient();admin.login(PREFIX+'-production');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    before=rows('SELECT Id FROM Users')
    response=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-pending','email':'synthetic@example.invalid','roles':['Management']},503)
    check(response['code']=='delivery_unavailable' and rows('SELECT Id FROM Users')==before,'Production unconfigured provisioning fails closed without partial account')
    admin.request('POST','/api/admin/users/'+uid+'/issue-credentials',{'version':admin.request('GET','/api/auth/me')['version']},503)
    admin.request('POST','/api/admin/users/'+uid+'/credential-delivery',status=404)
    check(True,'Production cannot issue or expose raw credential tokens')
    a=ProxyClient().request('POST','/api/auth/forgot-password',{'email':'synthetic@example.invalid'})
    b=ProxyClient().request('POST','/api/auth/forgot-password',{'email':None})
    check(a==b and set(a)=={'message'},'Production anonymous recovery remains generic when delivery unavailable')
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--environment','Production','--Security:EnableDevelopmentCredentialDelivery','true'],cwd=ROOT/'src/SIAMIS.Api',capture_output=True,text=True,timeout=30)
    check(p.returncode!=0 and 'Development credential delivery is forbidden outside Development' in p.stderr+p.stdout,'Production refuses accidental Development adapter activation')
    check('token' not in json.dumps(response).lower() and 'password' not in json.dumps(response).lower(),'Production failure response exposes no token/password')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:cleanup();check(snapshot()==baseline,'exact baseline after Production fixture cleanup')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d13-production-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2))
if error:raise SystemExit(error)
print('PASS:',len(results),'D13 Production checks; local baseline restored')
