"""D14 real authenticated multipart/private-storage verification; fixtures are local and explicitly cleaned."""
import pathlib, hashlib, re, datetime, threading
from concurrent.futures import ThreadPoolExecutor
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
PREFIX='D14-'+uuid.uuid4().hex[:8]
baseline=snapshot();baseline_audit_ids={r['Id'] for r in rows('SELECT Id FROM SecurityAuditEvents')}
private=ROOT/'src/SIAMIS.Api/App_Data/hr-documents'
storage_before={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in private.glob('*') if p.is_file()}
error=None;documents=[];leave=None
PDF=b'%PDF-1.7\nSynthetic confidential D14 fixture'
PNG=bytes([137,80,78,71,13,10,26,10])+b'synthetic D14'
JPEG=bytes([255,216,255])+b'synthetic D14'
context={'departmentId':'10000000-0000-0000-0000-000000000004','designationId':'20000000-0000-0000-0000-000000000006','employmentTypeId':'30000000-0000-0000-0000-000000000001','employmentStatusId':'40000000-0000-0000-0000-000000000001'}

def raw(c,method,path,data=None,headers=None,production=False):
    base='http://localhost:5156' if production else BASE
    hdr=dict(headers or {})
    if production:hdr['X-Forwarded-Proto']='https'
    if method not in ['GET','HEAD','OPTIONS']:
        # Production cookie relay is explicit only for local trusted-proxy test transport.
        if production:
            code,b,_=raw(c,'GET','/api/auth/csrf',production=True);hdr['X-CSRF-TOKEN']=json.loads(b)['token']
        else:hdr['X-CSRF-TOKEN']=c.request('GET','/api/auth/csrf')['token']
    if production:hdr['Cookie']='; '.join(x.name+'='+x.value for x in c.jar)
    req=urllib.request.Request(base+path,data=data,method=method,headers=hdr)
    try:
        with c.opener.open(req,timeout=60) as r:return r.status,r.read(),r.headers
    except urllib.error.HTTPError as e:return e.code,e.read(),e.headers

def multipart(c,path,payload=PDF,name='safe.pdf',mime='application/pdf',fields=None,expected=201,production=False):
    boundary='D14'+uuid.uuid4().hex
    parts=[]
    for k,v in (fields or {}).items():parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\nContent-Type: {mime}\r\n\r\n'.encode()+payload+b'\r\n')
    parts.append(f'--{boundary}--\r\n'.encode())
    code,data,h=raw(c,'POST',path,b''.join(parts),{'Content-Type':'multipart/form-data; boundary='+boundary},production)
    if code!=expected:raise AssertionError(f'D14 multipart expected {expected}, got {code}: '+data.decode(errors='replace')[:400])
    value=json.loads(data) if data and 'json' in h.get('Content-Type','') else None
    if code==201:documents.append(value['employeeDocumentId'])
    return value

def upload(c,e,**kw):return multipart(c,f'/api/employees/{e}/documents',**kw)
def owned(e,d):return f'/api/employees/{e}/documents/{d}'
def direct(d):return '/api/hr-documents/'+d
def dbdoc(d):return rows('SELECT * FROM EmployeeDocuments WHERE EmployeeDocumentId='+ident(d))[0]
def safe_path(key):
    assert re.fullmatch('[a-f0-9]{32}\\.blob',key)
    p=(private/key).resolve();assert p.parent==private.resolve();return p

def account(label,roles,employee=None):
    u=admin.request('POST','/api/admin/users',{'userName':PREFIX+'-'+label,'temporaryPassword':password,'employeeId':employee,'roles':roles},201)
    fixtures['users'].append(u['userId']);c=Client();c.login(u['userName']);return u,c

def employee(label):
    e=admin.request('POST','/api/employees',context|{'employeeNumber':PREFIX+'-'+label,'firstName':'Synthetic','lastName':'D14','hireDate':str(datetime.date.today()-datetime.timedelta(days=60))},201)['employeeId']
    fixtures['employees'].append(e);return e

def race(calls):
    barrier=threading.Barrier(len(calls))
    def f(fn):barrier.wait();return fn()
    with ThreadPoolExecutor(max_workers=len(calls)) as pool:return list(pool.map(f,calls))

def replace_raw(c,d,version):
    b='D14'+uuid.uuid4().hex
    data=f'--{b}\r\nContent-Disposition: form-data; name="version"\r\n\r\n{version}\r\n--{b}\r\nContent-Disposition: form-data; name="file"; filename="replacement.pdf"\r\nContent-Type: application/pdf\r\n\r\n'.encode()+PDF+f'\r\n--{b}--\r\n'.encode()
    code,data,_=raw(c,'POST',direct(d)+'/replace',data,{'Content-Type':'multipart/form-data; boundary='+b})
    if code==201:documents.append(json.loads(data)['employeeDocumentId'])
    return code

try:
    check(snapshot()==json.loads((directory/'d14-baseline.json').read_text()),'D14 original 85-table rows preserved by migration')
    columns=rows("SELECT name,TYPE_NAME(user_type_id) AS TypeName,max_length,is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('EmployeeDocuments')")
    check(all(any(c['name']==n for c in columns) for n in ['Category','ContentType','ContentSha256','SizeBytes','CreatedByUserId','Version','EmploymentRecordId','LeaveId','LeaveEvidenceId','LifecycleStatus','SupersedesDocumentId','LifecycleChangedAtUtc','LifecycleChangedByUserId']),'D14 13 additive columns exist in actual SQL')
    constraints=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name LIKE 'CK_HrDocument_%'")
    check(len(constraints)==5 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in constraints),'D14 five SQL checks enabled/trusted')
    fks=rows("SELECT name,delete_referential_action_desc AS DeleteBehavior FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('EmployeeDocuments')")
    check(all(x['DeleteBehavior']=='NO_ACTION' for x in fks),'D14 every document FK NoAction')
    check(rows("SELECT name FROM sys.key_constraints WHERE name='AK_EmploymentRecords_EmployeeId_EmploymentRecordId'")!=[],'D14 employment ownership alternate key exists')
    env=os.environ.copy();env['Bootstrap__UserName']=PREFIX+'-admin';env['Bootstrap__Password']=password
    p=subprocess.run(['dotnet','bin/Release/net10.0/SIAMIS.Api.dll','--bootstrap-admin','true','--environment','Development','--Logging:LogLevel:Default','Warning'],cwd=ROOT/'src/SIAMIS.Api',env=env,capture_output=True,text=True)
    check(p.returncode==0,'D14 isolated SystemAdmin bootstrap')
    uid=rows("SELECT Id FROM Users WHERE UserName='"+PREFIX+"-admin'")[0]['Id'];fixtures['users'].append(uid)
    admin=Client();admin.login(PREFIX+'-admin');admin.request('POST','/api/auth/change-password',{'currentPassword':password,'newPassword':new_password},204)
    e=employee('A');other=employee('B')
    hu,hr=account('hr',['HRAdmin']);_,pay=account('pay',['PayrollAdmin']);_,management=account('management',['Management']);_,ownclient=account('employee',['Employee'],e);_,otherclient=account('other',['Employee'],other)
    anonymous=Client()
    for c,name,status in [(anonymous,'anonymous',401),(admin,'SystemAdmin only',403),(pay,'PayrollAdmin',403),(management,'Management',403),(ownclient,'Employee owner',403),(otherclient,'other Employee',403)]:
        c.request('GET',f'/api/employees/{e}/documents',status=status)
        upload(c,e,expected=status)
        check(True,'D14 '+name+' denied metadata/upload')
    er=rows('SELECT EmploymentRecordId FROM EmploymentRecords WHERE EmployeeId='+ident(e))[0]['EmploymentRecordId']
    d=upload(hr,e,fields={'category':'EmploymentContract','employmentRecordId':er,'remarks':'Synthetic immutable fixture'})
    id=d['employeeDocumentId'];stored=dbdoc(id);key=stored['StorageKey'];path=safe_path(key)
    check(d['uploadedAt'].endswith('Z') and hr.request('GET',direct(id))['uploadedAt'].endswith('Z'),'D14 creation/read timestamps explicitly serialize UTC')
    check(d['createdByUserId'].lower()==hu['userId'].lower() and d['employeeId'].lower()==e.lower(),'D14 authoritative authenticated uploader and owner')
    check(d['contentSha256']==hashlib.sha256(PDF).hexdigest() and d['sizeBytes']==len(PDF) and path.read_bytes()==PDF,'D14 server hash/size match actual private bytes')
    check('storageKey' not in d and not any(x in json.dumps(d) for x in [str(private),key]),'D14 DTO contains no key or physical path')
    check(re.fullmatch('[a-f0-9]{32}\\.blob',key) is not None and 'safe' not in key,'D14 opaque server key excludes filename')
    def constraint(query,label,number=547):
        text=sql("BEGIN TRAN; BEGIN TRY "+query+"; ROLLBACK; THROW 51000,'Invalid accepted',1; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; IF ERROR_NUMBER()<>"+str(number)+" THROW; SELECT 'Rejected'; END CATCH;")
        check('Rejected' in text,label)
    constraint("UPDATE EmployeeDocuments SET Category='Invalid' WHERE EmployeeDocumentId="+ident(id),'D14 SQL category rejects invalid value')
    constraint('UPDATE EmployeeDocuments SET SizeBytes=NULL WHERE EmployeeDocumentId='+ident(id),'D14 SQL binary integrity tuple rejects null size')
    constraint('UPDATE EmployeeDocuments SET EmployeeId='+ident(other)+' WHERE EmployeeDocumentId='+ident(id),'D14 SQL enforces employment ownership')
    for c,name,status in [(anonymous,'anonymous',401),(admin,'SystemAdmin only',403),(pay,'PayrollAdmin',403),(management,'Management',403),(ownclient,'Employee owner',403),(otherclient,'other Employee',403)]:
        for route in [direct(id),direct(id)+'/content',owned(e,id),owned(e,id)+'/content']:c.request('GET',route,status=status)
        multipart(c,direct(id)+'/replace',fields={'version':d['version']},expected=status)
        multipart(c,owned(e,id)+'/replace',fields={'version':d['version']},expected=status)
        c.request('POST',owned(e,id)+'/archive',{'version':d['version']},status)
        c.request('POST',direct(id)+'/archive',{'version':d['version']},status)
        c.request('PUT',owned(e,id),{},status);c.request('DELETE',owned(e,id),{'version':d['version']},status)
        check(True,'D14 '+name+' denied direct-ID/content/replace/archive/legacy routes')
    for route in [direct(id)+'/content',owned(e,id)+'/content']:
        code,b,h=raw(hr,'GET',route);check(code==200 and b==PDF and h.get('Content-Disposition','').startswith('attachment') and h.get('Cache-Control')=='no-store' and h.get('X-Content-Type-Options')=='nosniff','D14 exact attachment download '+route)
    for route in [owned(other,id),owned(other,id)+'/content']:hr.request('GET',route,status=404)
    multipart(hr,owned(other,id)+'/replace',fields={'version':d['version']},expected=404)
    hr.request('DELETE',owned(other,id),{'version':d['version']},404);check(True,'D14 authorized wrong-parent access remains 404')
    hr.request('GET',direct(str(uuid.uuid4())),status=404);check(True,'D14 unknown direct ID is 404')
    for payload,name,mime in [(JPEG,'photo.jpg','image/jpeg'),(PNG,'image.png','image/png')]:
        v=upload(hr,e,payload=payload,name=name,mime=mime);check(v['contentType']==mime,'D14 accepted '+mime)
    for name,mime,payload in [('bad.exe','application/octet-stream',b'MZ'),('bad.js','text/javascript',b'alert(1)'),('bad.svg','image/svg+xml',b'<svg/>'),('bad.pdf','image/png',PDF),('bad.pdf','application/pdf',PNG)]:upload(hr,e,payload=payload,name=name,mime=mime,expected=415);check(True,'D14 unsupported/spoofed file rejected '+name+' '+mime)
    for name in ['../escape.pdf','C:\\escape.pdf','x/y.pdf','CON.pdf','a%.pdf']:
        upload(hr,e,name=name,expected=400);check(True,'D14 unsafe filename rejected')
    upload(hr,e,payload=b'',expected=400);check(True,'D14 empty file rejected')
    upload(hr,e,fields={'category':'Unknown'},expected=400);check(True,'D14 category validation')
    upload(hr,e,fields={'employmentRecordId':rows('SELECT EmploymentRecordId FROM EmploymentRecords WHERE EmployeeId='+ident(other))[0]['EmploymentRecordId']},expected=400);check(True,'D14 wrong-owner employment association rejected')
    for k in ['storageKey','createdByUserId','contentSha256','lifecycleStatus','version','employeeDocumentId']:
        upload(hr,e,fields={k:str(uuid.uuid4())},expected=400);check(True,'D14 forged authoritative multipart field rejected '+k)
    exact=upload(hr,e,payload=PDF+b'0'*(20*1024*1024-len(PDF)))
    check(exact['sizeBytes']==20*1024*1024 and safe_path(dbdoc(exact['employeeDocumentId'])['StorageKey']).stat().st_size==20*1024*1024,'D14 exact 20 MiB boundary accepted and stored')
    upload(hr,e,payload=PDF+b'0'*(20*1024*1024-len(PDF)+1),expected=413);check(True,'D14 20 MiB plus one rejected')
    code,b,_=raw(hr,'POST',f'/api/employees/{e}/documents',b'malformed',{'Content-Type':'multipart/form-data; boundary=missing'});check(code==400,'D14 malformed multipart safely rejected')
    code,b,_=raw(hr,'GET','/App_Data/hr-documents/'+key);check(code==404,'D14 private content not statically served')
    code,b,_=raw(hr,'GET','/hr-documents/'+key);check(code==404,'D14 guessed storage key cannot access bytes')
    hr.request('PUT',owned(e,id),{},409);check(True,'D14 legacy metadata overwrite retired')
    # Production uses the same real HR user; no provider is configured.
    prod=Client()
    code,_,_=raw(prod,'POST','/api/auth/login',json.dumps({'userName':hu['userName'],'password':password}).encode(),{'Content-Type':'application/json'},True);check(code==204,'D14 Production HR login')
    upload(prod,e,expected=503,production=True);check(True,'D14 Production unconfigured storage fails closed')
    # Existing immutable content cannot be silently repaired on read.
    original=path.read_bytes();path.unlink()
    hr.request('GET',direct(id)+'/content',status=503);path.write_bytes(original);check(True,'D14 missing binary returns safe 503')
    path.write_bytes(b'corrupt');hr.request('GET',direct(id)+'/content',status=503);path.write_bytes(original)
    check(dbdoc(id)['ContentSha256']==stored['ContentSha256'],'D14 tampered binary rejected without hash mutation')
    replacement=multipart(hr,direct(id)+'/replace',payload=PDF+b'new',fields={'version':d['version']})
    rid=replacement['employeeDocumentId'];prior=hr.request('GET',direct(id))
    check(prior['lifecycleStatus']=='Superseded' and replacement['supersedesDocumentId']==id and replacement['employeeDocumentId']!=id,'D14 replacement creates stable immutable successor')
    check(path.read_bytes()==original and dbdoc(id)['ContentSha256']==stored['ContentSha256'],'D14 predecessor content/hash retained')
    multipart(hr,direct(id)+'/replace',fields={'version':d['version']},expected=409)
    hr.request('POST',direct(rid)+'/archive',{'version':d['version']},409);check(True,'D14 stale replacement/archive conflict')
    archived=hr.request('POST',direct(rid)+'/archive',{'version':replacement['version']})
    check(archived['lifecycleChangedAtUtc'].endswith('Z') and hr.request('GET',direct(rid))['lifecycleChangedAtUtc'].endswith('Z'),'D14 lifecycle/read timestamps explicitly serialize UTC')
    check(archived['lifecycleStatus']=='Archived' and safe_path(dbdoc(rid)['StorageKey']).exists(),'D14 archive retains metadata/binary')
    listed=hr.request('GET',f'/api/employees/{e}/documents');history=hr.request('GET',f'/api/employees/{e}/documents?includeHistory=true')
    check(id not in [x['employeeDocumentId'] for x in listed] and rid not in [x['employeeDocumentId'] for x in listed] and all(x in [y['employeeDocumentId'] for y in history] for x in [id,rid]),'D14 active vs history filtering')
    code,b,_=raw(hr,'GET',direct(rid)+'/content');check(code==200 and b==PDF+b'new','D14 archived historical download')
    x=upload(hr,e);v=x['version'];xid=x['employeeDocumentId']
    outcomes=race([lambda:replace_raw(hr,xid,v),lambda:replace_raw(hr,xid,v)])
    check(sorted(outcomes)==[201,409] and len(rows('SELECT * FROM EmployeeDocuments WHERE SupersedesDocumentId='+ident(xid)))==1,'D14 concurrent replacements produce exactly one successor')
    x=upload(hr,e);v=x['version'];xid=x['employeeDocumentId']
    outcomes=race([lambda:replace_raw(hr,xid,v),lambda:raw(hr,'POST',direct(xid)+'/archive',json.dumps({'version':v}).encode(),{'Content-Type':'application/json'})[0]])
    check(sorted(outcomes) in [[200,409],[201,409]],'D14 archive vs replacement single winning lifecycle')
    outcomes=race([lambda:upload(hr,e),lambda:upload(hr,e)])
    check(len({x['employeeDocumentId'] for x in outcomes})==2,'D14 simultaneous uploads retain independent identities')
    x=upload(hr,e);xid=x['employeeDocumentId'];v=x['version']
    outcomes=race([lambda:raw(hr,'GET',direct(xid)+'/content')[0],lambda:raw(hr,'POST',direct(xid)+'/archive',json.dumps({'version':v}).encode(),{'Content-Type':'application/json'})[0]])
    check(outcomes==[200,200],'D14 download vs archive retains readable immutable content')
    # Employee-first locks serialize offboarding and upload without deleting history.
    readiness=admin.request('GET',f'/api/employees/{other}/account-lifecycle');end={'expectedEmploymentRecordId':readiness['currentEmploymentRecordId'],'endDate':str(datetime.date.today()-datetime.timedelta(days=1)),'employmentStatusId':'40000000-0000-0000-0000-000000000005','disableLinkedAccount':True,'expectedLinkedAccountVersion':readiness['linkedAccountVersion']}
    outcomes=race([lambda:upload(hr,other),lambda:raw(admin,'POST',f'/api/employees/{other}/end-employment',json.dumps(end).encode(),{'Content-Type':'application/json'})[0]])
    check(outcomes[1]==200 and outcomes[0]['employeeId']==other,'D14 offboarding vs upload succeeds with historical retention')
    old_ids=[x['employeeDocumentId'] for x in hr.request('GET',f'/api/employees/{other}/documents?includeHistory=true')]
    admin.request('POST',f'/api/employees/{other}/rehire',context|{'hireDate':str(datetime.date.today())},201)
    check(old_ids==[x['employeeDocumentId'] for x in hr.request('GET',f'/api/employees/{other}/documents?includeHistory=true')],'D14 rehire retains same historical document IDs')
    # Explicit combined-role grant; session is revoked and then freshly authenticated.
    u=admin.request('GET','/api/admin/users/'+uid);admin.request('PUT','/api/admin/users/'+uid+'/roles',{'roles':['SystemAdmin','HRAdmin'],'version':u['version']})
    admin=Client();admin.login(PREFIX+'-admin',new_password);admin.request('GET',direct(id));check(True,'D14 SystemAdmin plus explicitly assigned HRAdmin grants document access')
    swagger=anonymous.request('GET','/swagger/v1/swagger.json')
    for route in ['/api/employees/{employeeId}/documents','/api/hr-documents/{documentId}','/api/hr-documents/{documentId}/content','/api/hr-documents/{documentId}/replace','/api/hr-documents/{documentId}/archive']:
        check(route in swagger['paths'] and all('401' in x['responses'] and '403' in x['responses'] for k,x in swagger['paths'][route].items() if k in ['get','post','put','delete']),'D14 Swagger explicit route/codes '+route)
    schema=swagger['components']['schemas']['HrDocumentDto'];check('storageKey' not in schema['properties'],'D14 Swagger metadata hides key')
    operations=swagger['paths']['/api/employees/{employeeId}/documents']['post']['requestBody']['content'];check('multipart/form-data' in operations,'D14 Swagger multipart contract')
    content=swagger['paths']['/api/hr-documents/{documentId}/content']['get']['responses']['200']['content']
    check(set(content)=={'application/pdf','image/jpeg','image/png'} and all(x['schema'].get('format')=='binary' for x in content.values()),'D14 Swagger documents binary attachment types')
    # Shared binary linkage references an existing receipt; its D8 lifecycle remains independently authoritative.
    leave={'__file__':str(ROOT/'tests/verify_d8d_live.py')}
    exec((ROOT/'tests/verify_d8d_live.py').read_text().split('\ntry:\n')[0],leave)
    leave['PREFIX']=PREFIX+'-LEAVE-';leave['api']=lambda method,path,body=None,status=200:hr.request(method,'/api/'+path,body,status)
    leave['policy'](leave['sick'],'EVIDENCE',False,supportingDocumentPolicy='AlwaysRequired',documentTypeId=leave['doc'])
    le,lc=leave['new']('EVIDENCE',None)
    lr=leave['api']('POST',f'employees/{le}/leave',leave['request']('2030-01-07',t=leave['sick']),201)
    receipt=leave['receipt'](le,lr)
    before={t:rows(f'SELECT * FROM {t}') for t in ['EmployeeLeave','EmployeeLeaveAllocations','EmployeeLeaveEvidence','EmployeeLeaveEvidenceEvents','EmployeeLeaveApprovalEvidence']}
    linked=upload(hr,le,fields={'leaveId':lr['leaveId'],'leaveEvidenceId':receipt['id'],'documentTypeId':leave['doc']})
    constraint('UPDATE EmployeeDocuments SET EmployeeId='+ident(e)+' WHERE EmployeeDocumentId='+ident(linked['employeeDocumentId']),'D14 SQL enforces existing Leave receipt ownership')
    check(linked['leaveEvidenceId']==receipt['id'] and all(rows(f'SELECT * FROM {t}')==v for t,v in before.items()),'D14 binary link preserves exact D8 receipt/event/leave/allocation facts')
    leave['api']('POST',f"employees/{le}/leave/{lr['leaveId']}/approve",{},409)
    check(True,'D14 uploading supporting bytes never satisfies acceptance prerequisite')
    leave['review'](le,lr,receipt);approved=leave['command'](le,lr,'approve')
    frozen={t:rows(f'SELECT * FROM {t}') for t in before}
    newer=multipart(hr,direct(linked['employeeDocumentId'])+'/replace',fields={'version':linked['version']})
    hr.request('POST',direct(newer['employeeDocumentId'])+'/archive',{'version':newer['version']})
    check(all(rows(f'SELECT * FROM {t}')==v for t,v in frozen.items()),'D14 replace/archive preserves frozen Approved Leave/evidence/history')
    check(rows('SELECT EvidenceId FROM EmployeeLeaveApprovalEvidence WHERE LeaveId='+ident(lr['leaveId']))[0]['EvidenceId'].lower()==receipt['id'].lower(),'D14 frozen approval still references original D8 receipt')
    upload(hr,e,fields={'leaveId':lr['leaveId'],'leaveEvidenceId':receipt['id'],'documentTypeId':leave['doc']},expected=400)
    check(True,'D14 cross-employee Leave receipt association rejected')
    audit=rows("SELECT * FROM SecurityAuditEvents WHERE ResourceType='EmployeeDocument'")
    semantic=[x for x in audit if x['Operation'].startswith('Document')]
    check(semantic and all(x.get('ActorUserId') and x.get('ResourceId') and 'EmployeeId=' in x['Operation'] for x in semantic) and all(x.get('ActorUserId') for x in audit),'D14 explicit document audit authenticated actor/context/time; generic D10 audit retained')
    check(all(not any(value in x['Operation'] for value in ['safe.pdf','Synthetic confidential',key]) for x in audit),'D14 audit excludes filenames/content/storage keys')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        # Metadata versions are removed newest first solely for explicitly recorded test IDs.
        for d in reversed(documents):
            found=rows('SELECT StorageKey FROM EmployeeDocuments WHERE EmployeeDocumentId='+ident(d))
            sql('DELETE EmployeeDocuments WHERE EmployeeDocumentId='+ident(d))
            if found:safe_path(found[0]['StorageKey']).unlink(missing_ok=True)
        if leave is not None:leave['cleanup_d8d']()
        cleanup()
        check(snapshot()==baseline,'D14 exact 85-table database baseline restored')
        after={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in private.glob('*') if p.is_file()}
        check(after==storage_before,'D14 exact private-storage baseline restored; no staged/orphan files')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc);traceback.print_exc()
    (directory/'d14-live-results.json').write_text(json.dumps({'checks':len(results),'error':error,'results':results},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print('PASS:',len(results),'D14 live checks; exact cleanup',flush=True)
