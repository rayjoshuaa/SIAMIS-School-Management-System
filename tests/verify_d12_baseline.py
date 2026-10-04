"""Read-only exact Development rows/history and existing lifecycle constraints."""
import pathlib,json
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
original=json.loads((directory/'d12-baseline.json').read_text());current=snapshot()
check(len(current)==85 and current==original,'exact original 85-table rows and timestamps preserved')
employee=json.loads(current['Employees'][0])
check(len(current['Employees'])==1 and not employee['IsActive'] and employee['EmployeeNumber']=='TEST-EMP-001' and employee['EmployeeId'].lower()==EMP,'only original TEST-EMP-001 remains inactive')
check(len(current['EmploymentRecords'])==1,'single original EmploymentRecord preserved')
check(len(current['Roles'])==5,'five permanent security roles retained')
check(all(not current[t] for t in ['Users','UserRoles','UserClaims','UserLogins','UserTokens','RoleClaims','SecurityAuditEvents']),'all temporary Identity/audit fixtures removed')
master=[t for t,v in original.items() if v and t not in ['Employees','EmploymentRecords','Roles']]
check(sum(len(current[t]) for t in master)==172 and len(current['PayrollComponents'])==17,'172 master rows including 17 components retained')
check(all(not current[t] for t in current if t not in master+['Employees','EmploymentRecords','Roles']),'all operational tables empty after cleanup')
migrations=rows('SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId');before=json.loads((directory/'d12-migrations-before.json').read_text())
check(migrations==before and len(migrations)==41,'all 41 original migration entries unchanged; no D12 migration')
indexes=rows("SELECT name,is_unique,filter_definition FROM sys.indexes WHERE name IN ('UX_EmploymentRecords_CurrentPerEmployee','IX_Users_EmployeeId')")
check(len(indexes)==2 and all(i['is_unique'] and i['filter_definition'] for i in indexes),'current employment and optional user linkage filtered unique constraints retained')
fks=rows("SELECT name,delete_referential_action_desc AS DeleteBehavior FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('Users')")
check(len(fks)==1 and fks[0]['DeleteBehavior']=='NO_ACTION','retained Identity Employee linkage NoAction')
(directory/'d12-final-baseline-results.json').write_text(json.dumps({'checks':len(results),'error':None,'counts':{t:len(v) for t,v in current.items()},'migrations':migrations,'indexes':indexes,'foreignKeys':fks,'results':results},indent=2))
print('PASS:',len(results),'D12 final SQL checks; exact Development baseline restored')
