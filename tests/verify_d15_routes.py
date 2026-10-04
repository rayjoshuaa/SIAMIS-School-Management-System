"""Authoritative current role/capability/route inventory with real cookie/CSRF authorization probes."""
import pathlib, re, json, os, subprocess, traceback
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
PREFIX='D15-ROUTES-'+uuid.uuid4().hex[:6]
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')};error=None;matrix=[]
role_source=(ROOT/'src/SIAMIS.Application/Security/SecurityContracts.cs').read_text()
grants={r:set(re.findall(r'"([^"\n]+)"',caps))|{'MasterData.Read'} for r,caps in re.findall(r'\["(\w+)"\]\s*=\s*\[([^\]]+)\]',role_source)}

def capability(name,action,read):
    if name=='EmployeeHistory':return 'Employee.Read|Payroll.Read' if read else 'Employee.Manage|Payroll.Manage'
    if name in ['AdminUsers','DevelopmentCredentialDelivery']:return 'Security.Manage'
    if name in ['EmployeeDocuments','HrDocuments']:return 'HRDocuments.Read' if read else 'HRDocuments.Manage'
    if name=='AttendanceReporting':return 'Attendance.Read' if action=='Queue' else 'Reporting.Read'
    if name=='HrOverview':return 'Reporting.Read'
    if name=='SelfService':return 'SelfService'
    if name=='AttendanceReview':return 'Attendance.Read' if read else 'Attendance.Finalize' if action in ['FinalizeDay','Reopen','Confirm'] else 'Attendance.Manage'
    if name in ['AttendanceFoundation','AttendanceDays','EmployeeAttendance']:return 'Attendance.Read' if read else 'Attendance.Manage'
    if name=='LeaveEvidenceSandwich':return 'Leave.Evidence'
    if name=='EmployeeLeave':return 'Leave.Read' if read else 'Leave.Review' if action in ['Approve','Reject'] else 'Leave.Manage'
    if name=='LeaveOperations':return 'Leave.Read'
    if name=='LeaveFoundation':return 'Leave.Read' if read else 'Leave.Manage'
    if name in ['MasterData','Status']:return 'MasterData.Read' if read else 'Unmapped'
    if any(x in name for x in ['Payroll','Statutory','Tax','Pit']) or name in ['EmployeeCompensations','OrganizationProfile']:return 'Payroll.Read' if read else 'Payroll.Manage'
    if name in ['Employees','EmployeeContacts','EmployeeAddresses','EmployeeEmergencyContacts','EmployeeContracts','EmployeeHistory','EmployeePerformance','EmploymentLifecycle','EmploymentStatuses']:return 'Employee.Read' if read else 'Employee.Manage'
    return 'Unmapped'

routes={}
for file in (ROOT/'src/SIAMIS.Api/Controllers').glob('*Controller.cs'):
    text=file.read_text();name=file.stem.removesuffix('Controller')
    if name=='Auth':continue
    root_match=re.search(r'Route\("([^"]+)"\)',text)
    root=root_match.group(1) if root_match else ''
    for match in re.finditer(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?[^\]]*\]',text):
        action=re.search(r'public\s+(?:async\s+)?[^\r\n]*?\b(\w+)\(',text[match.end():]).group(1)
        path='/'+ '/'.join(p for p in [root,match.group(2)] if p)
        path=re.sub(r'\{(\w+):[^}]+\}',r'{\1}',path)
        routes[(match.group(1).lower(),path)]=(name,action)
try:
    swagger=Client().request('GET','/swagger/v1/swagger.json')
    documented={(m,p) for p,ops in swagger['paths'].items() if p.startswith('/api/') and not p.startswith('/api/auth/') for m in ops if m in ['get','post','put','patch','delete']}
    check(set(routes)==documented,'all current documented routes map to actual controller actions')
    # Never reuse prior authorization results. Real provision/activation through D13 helper.
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-admin';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode==0,'isolated route-matrix bootstrap')
    uid=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-admin'")[0]['Id'];fixtures['users'].append(uid)
    admin=Client();admin.login(PREFIX+'-admin');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    clients={'SystemAdmin':admin}
    role_sets={r:[r] for r in grants};role_sets['SystemAdmin+HRAdmin']=['SystemAdmin','HRAdmin']
    for label,roles in role_sets.items():
        if label=='SystemAdmin':continue
        u=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-'+label.replace('+','-'),'temporaryPassword':password,'employeeId':EMP if label=='Employee' else None,'roles':roles},201)
        fixtures['users'].append(u['userId']);c=Client();c.login(u['userName']);clients[label]=c
    probes=0
    for (method,template),(name,action) in sorted(routes.items()):
        cap=capability(name,action,method=='get')
        assert cap!='Unmapped',(name,action)
        path=re.sub(r'\{([^}]+)\}',lambda m:'2026-09-29' if m.group(1).lower() in ['date','businessdate'] else '2026' if 'year' in m.group(1).lower() else '00000000-0000-0000-0000-000000000001',template)
        allowed=[]
        for role,c in clients.items():
            can=any(c in set().union(*(grants[r] for r in role_sets[role])) for c in cap.split('|'))
            if name=='SelfService' and role=='Employee':can=True
            # fake nonowned IDs deliberately exercise denial of Employee self-service escape paths.
            if method!='get':c.token=c.request('GET','/api/auth/csrf')['token']
            headers={'Content-Type':'application/json'}
            if method!='get':headers['X-CSRF-TOKEN']=c.token
            payload=None if method=='get' else b'{}'
            if name in ['EmployeeDocuments','HrDocuments'] and method=='post' and action in ['CreateDocument','Replace']:
                boundary='D15RouteBoundary'
                headers['Content-Type']='multipart/form-data; boundary='+boundary
                payload=(f'--{boundary}\r\nContent-Disposition: form-data; name="version"\r\n\r\n00000000-0000-0000-0000-000000000001\r\n'
                         f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="probe.pdf"\r\nContent-Type: application/pdf\r\n\r\n%PDF-1.7\nprobe\r\n--{boundary}--\r\n').encode()
            req=urllib.request.Request(BASE+path,data=payload,method=method.upper(),headers=headers)
            try:
                with c.opener.open(req,timeout=40) as r:status=r.status;r.read()
            except urllib.error.HTTPError as e:status=e.code;e.read()
            if can:
                assert status in [200,204,400,404,409,410,415],(role,method,template,cap,status)
                allowed.append(role)
            else:assert status==403,(role,method,template,cap,status)
            probes+=1
        matrix.append({'method':method.upper(),'route':template,'controller':name,'action':action,'capability':cap,'roles':allowed})
    check(probes==len(routes)*6,'six real role combinations probed for every route')
    check(all('SystemAdmin' not in row['roles'] and 'HRAdmin' in row['roles'] and 'SystemAdmin+HRAdmin' in row['roles'] for row in matrix if row['capability'].startswith('HRDocuments.')),'document isolation across all metadata/direct-ID/binary/lifecycle routes')
    for schema in swagger['components']['schemas'].values():
        assert not {'passwordHash','securityStamp','concurrencyStamp','storageKey','physicalPath'}&set(schema.get('properties',{})),'internal DTO exposure'
    check(True,'all Swagger schemas exclude private storage and Identity internals')
    check(not any('must be added when SIAMIS authentication' in str(op) or 'will be added when SIAMIS authentication' in str(op) for ops in swagger['paths'].values() for op in ops.values()),'obsolete payroll authentication documentation removed')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:cleanup();check(snapshot()==baseline,'exact baseline after full role/route matrix')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d15-routes-results.json').write_text(json.dumps({'checks':len(results),'probes':locals().get('probes',0),'error':error,'matrix':matrix,'roles':{r:sorted(c) for r,c in grants.items()}},indent=2))
if error:raise SystemExit(error)
markdown='# D15 role → capability → route matrix\n\nGenerated from current controller actions and Swagger; six role combinations tested with real Identity cookies and CSRF. Allowed probes use invalid/nonowned IDs or invalid shapes, so they exercise gates without changing HR data. Successful business behavior is verified separately.\n\n'
markdown+='| Role | Capabilities |\n|---|---|\n'+''.join('| '+r+' | '+', '.join(sorted(c))+' |\n' for r,c in grants.items())
markdown+='\nEmployee own-record exceptions: own Leave list/detail/create/cancel and balances; own Attendance history/summary; own Approved/Paid payroll, lines/PIT/payslip through server User→Employee linkage. No Employee document access; self-approval prohibited. Linked evidence content also requires Leave.Evidence.\n\n'
markdown+='EmployeeHistory route admission uses either capability (`|` means OR). Service-level EventType gates: ordinary events require Employee.Read/Manage; Salary Change requires Payroll.Read/Manage independently. Lists filter in SQL before sorting/projection, with no placeholders or total. Unauthorized/nonowned direct IDs return 404; unauthorized creates return 403. Payroll-only callers cannot access ordinary events.\n\n'
markdown+='| Method | Route | Capability | Default roles / combined role |\n|---|---|---|---|\n'+''.join('| '+r['method']+' | `'+r['route']+'` | '+r['capability'].replace('|',' or ')+' | '+', '.join(r['roles'])+' |\n' for r in matrix)
(directory/'D15-AUTHORIZATION-MATRIX.md').write_text(markdown,encoding='utf-8')
print('PASS:',probes,'authenticated role/route probes;',len(routes),'routes',flush=True)
