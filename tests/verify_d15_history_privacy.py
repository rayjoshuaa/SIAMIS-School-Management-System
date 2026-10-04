"""Approved D15-06 history category boundaries using real Identity/SQL fixtures."""
import pathlib, os, subprocess, json, traceback
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
PREFIX='D15-PRIVACY-'+uuid.uuid4().hex[:6]
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')};error=None
try:
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-admin';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode==0,'isolated privacy bootstrap')
    fixtures['users'].append(rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-admin'")[0]['Id'])
    admin=Client();admin.login(PREFIX+'-admin');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    employee=admin.request('POST','/api/employees',{'employeeNumber':PREFIX,'firstName':'Synthetic','lastName':'Privacy','departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001','hireDate':'2026-09-01'},201)
    eid=employee['employeeId'];fixtures['employees'].append(eid);path=f'/api/employees/{eid}/history'
    clients={};users={}
    for name,roles in [('hr',['HRAdmin']),('payroll',['PayrollAdmin']),('combined',['HRAdmin','PayrollAdmin']),('management',['Management']),('employee',['Employee'])]:
        u=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-'+name,'temporaryPassword':password,'roles':roles,'employeeId':eid if name=='employee' else None},201);fixtures['users'].append(u['userId']);users[name]=u
        c=Client();c.login(u['userName']);clients[name]=c
    hr=clients['hr'];pay=clients['payroll'];both=clients['combined'];anonymous=Client()
    ordinary={'eventType':'Other','eventDate':'2026-09-20','description':'Synthetic HR history','changedBy':'Descriptive administrator'}
    financial={'eventType':'Salary Change','eventDate':'2026-09-29','previousValue':'30000 THB','newValue':'35000 THB'}
    check('Payroll.Read' not in hr.request('GET','/api/auth/me')['capabilities'],'HRAdmin has no Payroll.Read')
    check('Employee.Read' not in pay.request('GET','/api/auth/me')['capabilities'],'PayrollAdmin has no Employee.Read')
    first=hr.request('POST',path,ordinary,201);oid=first['employeeHistoryId']
    second=hr.request('POST',path,ordinary|{'eventDate':'2026-09-30'},201)
    check(hr.request('GET',path+'/'+oid)==first,'A ordinary history detail remains readable')
    before_visible=hr.request('GET',path)
    hr.request('POST',path,financial,403);check(True,'D Employee.Manage cannot create financial history')
    hr.request('POST',path,financial|{'eventType':'  salary change  '},403);check(True,'canonicalization cannot bypass financial create gate')
    forged='00000000-0000-0000-0000-000000000099'
    salary=pay.request('POST',path,financial|{'actorUserId':forged,'createdByUserId':forged,'changedBy':'Descriptive payroll operator'},201);sid=salary['employeeHistoryId']
    check(salary['eventType']=='Salary Change' and salary['newValue']=='35000 THB','G Payroll.Manage creates financial event independently')
    check(hr.request('GET',path)==before_visible,'C/M hidden event adds no placeholder, count, ordering or response gap')
    check(isinstance(before_visible,list) and [x['employeeHistoryId'] for x in before_visible]==[second['employeeHistoryId'],oid],'M unchanged array contract and authorized date ordering')
    hr.request('GET',path+'/'+sid,status=404);check(True,'B/L financial direct ID hidden from HR-only caller')
    hr.request('DELETE',path+'/'+sid,status=404);check(True,'E financial delete hidden/denied to Employee.Manage')
    check(pay.request('GET',path+'/'+sid)==salary,'F Payroll.Read reads financial detail independently')
    check(pay.request('GET',path)==[salary],'I payroll-only list excludes all ordinary events')
    pay.request('GET',path+'/'+oid,status=404);check(True,'I/L ordinary direct ID hidden from payroll-only caller')
    pay.request('POST',path,ordinary,403);pay.request('DELETE',path+'/'+oid,status=404);check(True,'J Payroll.Manage cannot create/delete ordinary events')
    all_events=both.request('GET',path)
    check([x['employeeHistoryId'] for x in all_events]==[second['employeeHistoryId'],sid,oid],'K combined capabilities read both categories in unchanged order')
    check(admin.request('GET',path)==all_events,'SystemAdmin follows existing grants')
    check(hr.request('GET',path+'?page=1&pageSize=1')==before_visible,'M unsupported paging parameters cannot reveal hidden events; array contract unchanged')
    absent='00000000-0000-0000-0000-000000000001'
    for c,record in [(hr,oid),(pay,sid),(both,sid)]:
        c.request('GET',f'/api/employees/{absent}/history/'+record,status=404)
        c.request('DELETE',f'/api/employees/{absent}/history/'+record,status=404)
    check(True,'P parent ownership and missing-resource integrity enforced')
    pay.request('POST',f'/api/employees/{absent}/history',financial,404);check(True,'G authorized financial create still requires Employee existence')
    hr.request('POST',path,ordinary|{'eventType':'Not Approved'},400);hr.request('POST',path,ordinary|{'eventDate':None},400);check(True,'P type/date validation unchanged')
    for method,body in [('GET',None),('POST',ordinary),('DELETE',None)]:
        anonymous.request(method,path if method!='DELETE' else path+'/'+sid,body,401)
    check(True,'N anonymous list/create/delete denied')
    for name in ['management','employee']:
        clients[name].request('GET',path,status=403);clients[name].request('GET',path+'/'+sid,status=403)
    check(True,'no Management/Employee or self-service expansion')
    hr.request('GET',f'/api/employees/{eid}/compensations',status=403)
    check(pay.request('GET',f'/api/employees/{eid}/compensations')==[],'Q Compensation retains independent Payroll.Read gate')
    events=rows("SELECT * FROM SecurityAuditEvents WHERE ResourceType='EmployeeHistory' AND ResourceId="+ident(sid))
    check(events and all(x['ActorUserId'].lower()==users['payroll']['userId'].lower() for x in events),'O spoofed actor fields ignored; real payroll creator audited')
    pay.request('DELETE',path+'/'+sid,status=204);check(pay.request('GET',path)==[],'H Payroll.Manage deletes financial event')
    hr.request('DELETE',path+'/'+oid,status=204);hr.request('DELETE',path+'/'+second['employeeHistoryId'],status=204);check(both.request('GET',path)==[],'P ordinary administrative correction and empty list unchanged')
    swagger=anonymous.request('GET','/swagger/v1/swagger.json');ops=swagger['paths']['/api/employees/{employeeId}/history']
    check('Payroll.Read' in ops['get'].get('description','') and 'Payroll.Manage' in ops['post'].get('description','') and {'401','403'}<=set(ops['post']['responses']),'Swagger documents event capabilities/security responses')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        for eid in fixtures['employees']:sql('DELETE EmployeeHistory WHERE EmployeeId='+ident(eid))
        cleanup();check(snapshot()==baseline,'privacy repair exact SQL baseline restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d15-history-privacy-results.json').write_text(json.dumps({'error':error,'checks':len(results),'results':results},indent=2))
if error:raise SystemExit(error)
print('PASS:',len(results),'approved D15-06 history privacy checks; exact cleanup.')
