"""D15 read-only chain, schema, temporal precision and tracked source hygiene audit."""
import pathlib,json,re,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
inventory=json.loads((directory/'d15-inventory.json').read_text());results=[]
def check(value,label):
    assert value,label
    results.append(label)
migrations=sorted(p.stem for p in (ROOT/'src/SIAMIS.Infrastructure/Migrations').glob('*.cs') if re.match(r'^\d{14}_',p.stem) and not p.stem.endswith('.Designer'))
check(migrations==[x['MigrationId'] for x in inventory['migrations']],'all 42 migration IDs equal the ordered applied chain')
check(len(migrations)==len(set(migrations)),'no duplicate migration identifiers')
check(all(not x['is_disabled'] and not x['is_not_trusted'] for x in inventory['fks']+inventory['checks']),'all 112 foreign keys and 131 check constraints enabled and trusted')
cascades=[f for f in inventory['fks'] if f['delete_referential_action_desc']=='CASCADE']
check(all(f['TableName'] in ['PayrollRuleTargets','UserClaims','UserLogins','UserRoles','UserTokens','RoleClaims'] for f in cascades),'cascade deletion limited to mutable rule targets and Identity-owned children')
for name in ['UX_EmploymentRecords_CurrentPerEmployee','IX_Users_EmployeeId','IX_EmployeeDocuments_SupersedesDocumentId']:
    check(any(x['name']==name and x['is_unique'] and x.get('filter_definition') for x in inventory['indexes']),name+' filtered unique index')
for table,columns in [('AttendanceEvents',['OccurredAtUtc','ReceivedAtUtc']),('EmployeeDocuments',['UploadedAt','LifecycleChangedAtUtc'])]:
    check(all(any(c['TableName']==table and c['name']==column and c['TypeName']=='datetime2' and c['scale']==7 for c in inventory['columns']) for column in columns),table+' exact datetime2(7) instants')
check(any(i['TableName']=='EmployeePayrolls' and i['is_unique'] and set(i['Columns'].split(','))=={'EmployeeId','PayrollPeriodId'} for i in inventory['indexes']),'one payroll header per employee/period')
check(not any(c['TypeName'] in ['float','real','money','smallmoney'] for c in inventory['columns']),'no floating point or implicit money precision in database')
check(all(c['precision']==19 and c['scale']==4 for c in inventory['columns'] if c['TableName'] in ['EmployeePayrolls','EmployeePayrollLines','EmployeeCompensations'] and c['TypeName']=='decimal'),'salary/header/line amounts retain decimal(19,4)')
check(not any(f['ReferencedTable']=='Employees' and f['delete_referential_action_desc']!='NO_ACTION' for f in inventory['fks']),'no personnel child cascade')
files=subprocess.check_output(['git','ls-files'],cwd=ROOT,text=True).splitlines()
suspects=[]
patterns=[r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',r'\bgh[pousr]_[A-Za-z0-9]{30,}\b',r'\bAKIA[A-Z0-9]{16}\b',r'(?i)(?:password|pwd|client_secret|api_key)\s*[:=]\s*[\"\'][^\"\']{8,}[\"\']']
for file in files:
    path=ROOT/file
    if path.suffix.lower() not in ['.cs','.json','.md','.py','.ps1','.yml','.yaml','.xml','.config']:continue
    text=path.read_text(encoding='utf-8-sig')
    for n,line in enumerate(text.splitlines(),1):
        if file.endswith('verify_d10_repository.py'):continue
        if any(re.search(p,line) for p in patterns):suspects.append({'file':file,'line':n})
    if file.startswith('src/'):
        check(not re.search(r'[A-Z]:\\(?:Users|private|document)',text,re.I),'no tracked private absolute path: '+file)
check(not suspects,'no credential/token/private-key pattern in tracked files (synthetic setup reviewed separately)')
controllers=list((ROOT/'src/SIAMIS.Api/Controllers').glob('*.cs'))
check(all('DbContext' not in p.read_text() for p in controllers),'no controller accesses DbContext')
check(not re.search(r'Attendance|EmployeeLeave', (ROOT/'src/SIAMIS.Infrastructure/Services/PayrollCalculationService.cs').read_text()),'payroll monetary calculator has no Attendance/Leave deduction integration')
assets=json.loads((ROOT/'src/SIAMIS.Infrastructure/obj/project.assets.json').read_text())
packages=[n for n in assets['libraries'] if n.startswith(('Microsoft.EntityFrameworkCore/','Microsoft.EntityFrameworkCore.SqlServer/','Microsoft.Data.SqlClient/'))]
(directory/'d15-repository-results.json').write_text(json.dumps({'checks':len(results),'error':None,'packages':packages,'cascades':cascades,'results':results},indent=2))
print('PASS:',len(results),'repository/schema checks;',packages)
