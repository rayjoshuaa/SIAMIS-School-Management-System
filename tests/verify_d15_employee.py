"""Focused core Employee input boundaries and supporting-record ownership/primary regressions."""
import pathlib,os,subprocess,json,traceback,sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
PREFIX='D15-EMP-'+uuid.uuid4().hex[:6];baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')};error=None;observing='--observe' in sys.argv;observations=[]
context={'departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001','hireDate':'2026-09-01','firstName':'Synthetic','lastName':'D15'}
def raw(method,path,body=None):
    token=admin.request('GET','/api/auth/csrf')['token']
    req=urllib.request.Request(BASE+path,method=method,data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json','X-CSRF-TOKEN':token})
    try:
        with admin.opener.open(req,timeout=30) as r:status=r.status;data=r.read()
    except urllib.error.HTTPError as e:status=e.code;data=e.read()
    try:value=json.loads(data) if data else None
    except json.JSONDecodeError:value=None
    if method=='POST' and path=='/api/employees' and status==201:fixtures['employees'].append(value['employeeId'])
    return status,value
try:
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-admin';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode==0,'Employee closure bootstrap')
    uid=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-admin'")[0]['Id'];fixtures['users'].append(uid)
    admin=Client();admin.login(PREFIX+'-admin');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    status,value=raw('GET','/api/employees?page=2147483647&pageSize=20');observations.append({'case':'overflow pagination','status':status})
    if not observing:check(status==400,'overflow Employee page offset rejected as 400')
    for field in ['contacts','addresses','emergencyContacts']:
        status,value=raw('POST','/api/employees',context|{'employeeNumber':PREFIX+'-'+field,field:[None]})
        observations.append({'case':'null '+field+' element','status':status})
        if not observing:check(status==400,'null '+field+' element rejected before mutation')
    status,value=raw('POST','/api/employees',context|{'employeeNumber':PREFIX+'-phone','emergencyContacts':[{'name':'Synthetic','relationship':'Sibling'}]})
    observations.append({'case':'embedded emergency missing Phone','status':status})
    if not observing:check(status==400,'core create cannot bypass approved emergency Phone requirement')
    if not observing:
        teacher={'teacherCode':PREFIX+'-teacher','teachingStatus':'Active','specialization':'Synthetic subject'}
        status,e=raw('POST','/api/employees',context|{'employeeNumber':PREFIX+'-valid','teacherProfile':teacher});check(status==201,'Employee create omitted manager valid')
        eid=e['employeeId'];other='00000000-0000-0000-0000-000000000001'
        check(e['teacherProfile']['teacherCode']==teacher['teacherCode'] and len(rows('SELECT * FROM TeacherProfiles WHERE EmployeeId='+ident(eid)))==1,'one owned TeacherProfile is readable through employee detail')
        admin.request('POST','/api/employees',context|{'employeeNumber':PREFIX+'-dupteacher','teacherProfile':teacher},409);check(True,'duplicate TeacherCode rejected')
        admin.request('POST','/api/employees',context|{'employeeNumber':PREFIX+'-valid'},409);check(True,'duplicate employee number blocked')
        body=context|{'employeeNumber':PREFIX+'-valid','preferredName':'Closure','reportingToEmployeeId':eid}
        admin.request('PUT','/api/employees/'+eid,body,400);check(True,'self-reporting update rejected')
        body.pop('reportingToEmployeeId');admin.request('PUT','/api/employees/'+eid,body)
        for field in ['contacts','addresses','emergencyContacts']:
            admin.request('PUT','/api/employees/'+eid,body|{field:[None]},400);check(True,'PUT null '+field+' invalid')
        admin.request('PUT','/api/employees/'+eid,body|{'emergencyContacts':[{'name':'Synthetic','relationship':'Sibling','phone':' '}]},400)
        check(True,'PUT missing emergency phone rejected')
        types=admin.request('GET','/api/master-data/address-types');countries=admin.request('GET','/api/master-data/countries')
        groups=[('contacts','employeeContactId',{'phone':'0812345678'}, {'phone':'0898765432'}),
            ('addresses','employeeAddressId',{'addressTypeId':types[0]['id'],'countryId':countries[0]['id'],'addressLine1':'Synthetic A'}, {'addressTypeId':types[0]['id'],'countryId':countries[0]['id'],'addressLine1':'Synthetic B'}),
            ('emergency-contacts','emergencyContactId',{'name':'Synthetic A','relationship':'Sibling','phone':'0812345678'},{'name':'Synthetic B','relationship':'Sibling','phone':'0898765432'})]
        for route,key,a,b in groups:
            path=f'/api/employees/{eid}/{route}'
            one=admin.request('POST',path,a|{'isPrimary':False},201);two=admin.request('POST',path,b|{'isPrimary':False},201)
            check(one['isPrimary'] and not two['isPrimary'],route+' first primary and second nonprimary')
            admin.request('PUT',path+'/'+two[key],b|{'isPrimary':True});current=admin.request('GET',path)
            check(sum(x['isPrimary'] for x in current)==1 and next(x for x in current if x[key]==two[key])['isPrimary'],route+' promote second uniquely')
            admin.request('PUT',f'/api/employees/{other}/{route}/'+two[key],b|{'isPrimary':True},404)
            admin.request('DELETE',f'/api/employees/{other}/{route}/'+two[key],status=404);check(True,route+' wrong-parent mutation denied')
            admin.request('DELETE',path+'/'+two[key],status=204);check(admin.request('GET',path)[0]['isPrimary'],route+' deleting primary promotes remaining')
            admin.request('DELETE',path+'/'+one[key],status=204);check(admin.request('GET',path)==[],route+' final deletion leaves empty list')
        check(admin.request('GET','/api/employees/'+eid)['preferredName']=='Closure','child workflows preserve core employee')
        check(admin.request('GET','/api/employees/'+eid)['teacherProfile']['teacherCode']==teacher['teacherCode'],'profile PUT omission preserves TeacherProfile')
        history_path=f'/api/employees/{eid}/history'
        history=admin.request('POST',history_path,{'eventType':'Other','eventDate':'2026-09-29','description':'Synthetic administrative history'},201)
        admin.request('GET',f'/api/employees/{other}/history/'+history['employeeHistoryId'],status=404)
        admin.request('DELETE',f'/api/employees/{other}/history/'+history['employeeHistoryId'],status=404)
        check(len(admin.request('GET',history_path))==1,'independent history list and ownership remain correct')
        admin.request('DELETE',history_path+'/'+history['employeeHistoryId'],status=204)
        check(admin.request('GET',history_path)==[],'approved administrative history correction deletion remains controlled')
        check(len(rows('SELECT * FROM EmploymentRecords WHERE EmployeeId='+ident(eid)))==1,'only one initial current employment created')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        if fixtures['employees']:
            ids=','.join(map(ident,fixtures['employees']))
            for table in ['EmployeeContacts','EmployeeAddresses','EmergencyContacts','TeacherProfiles','EmployeeHistory']:sql(f'DELETE {table} WHERE EmployeeId IN ({ids})')
        cleanup();check(snapshot()==baseline,'Employee closure exact baseline restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/('d15-employee-observation.json' if observing else 'd15-employee-results.json')).write_text(json.dumps({'checks':len(results),'error':error,'observations':observations,'results':results},indent=2))
if error:raise SystemExit(error)
print(json.dumps(observations));print('PASS:',len(results),'Employee closure checks')
