"""Read-only exact Development rows/history and existing lifecycle constraints."""
import pathlib,json
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
original=json.loads((directory/'d14-baseline.json').read_text());current=snapshot()
check(len(current)==85 and current==original,'exact original 85-table rows and timestamps preserved')
employee=json.loads(current['Employees'][0])
check(len(current['Employees'])==1 and not employee['IsActive'] and employee['EmployeeNumber']=='TEST-EMP-001' and employee['EmployeeId'].lower()==EMP,'only original TEST-EMP-001 remains inactive')
check(len(current['EmploymentRecords'])==1,'single original EmploymentRecord preserved')
check(len(current['Roles'])==5,'five permanent security roles retained')
check(all(not current[t] for t in ['Users','UserRoles','UserClaims','UserLogins','UserTokens','RoleClaims','SecurityAuditEvents']),'all temporary Identity/audit fixtures removed')
master=[t for t,v in original.items() if v and t not in ['Employees','EmploymentRecords','Roles']]
check(sum(len(current[t]) for t in master)==172 and len(current['PayrollComponents'])==17,'172 master rows including 17 components retained')
check(all(not current[t] for t in current if t not in master+['Employees','EmploymentRecords','Roles']),'all operational tables empty after cleanup')
migrations=rows('SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId');before=json.loads((directory/'d14-migrations-before.json').read_text())
check(migrations==before+[{'MigrationId':'20261004115720_AddSecureHrDocumentFoundation'}] and len(migrations)==42,'41 original migrations retained; only focused D14 migration appended')
indexes=rows("SELECT name,is_unique,filter_definition FROM sys.indexes WHERE name IN ('UX_EmploymentRecords_CurrentPerEmployee','IX_Users_EmployeeId')")
check(len(indexes)==2 and all(i['is_unique'] and i['filter_definition'] for i in indexes),'current employment and optional user linkage filtered unique constraints retained')
fks=rows("SELECT name,delete_referential_action_desc AS DeleteBehavior FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('Users')")
check(len(fks)==1 and fks[0]['DeleteBehavior']=='NO_ACTION','retained Identity Employee linkage NoAction')
private=ROOT/'src/SIAMIS.Api/App_Data/hr-documents'
check(not private.exists() or not list(private.iterdir()),'no D14 test binaries, stages or orphan private files remain')
if private.exists():private.rmdir()
check(not private.exists(),'private-storage baseline restored to absent/empty initialization state')
checks=rows("SELECT name,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name LIKE 'CK_HrDocument_%'")
check(len(checks)==5 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in checks),'five D14 checks remain trusted')
check(all(x['DeleteBehavior']=='NO_ACTION' for x in rows("SELECT delete_referential_action_desc AS DeleteBehavior FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('EmployeeDocuments')")),'all historical document ownership FKs NoAction')
timestamps=rows("SELECT name,scale FROM sys.columns WHERE object_id=OBJECT_ID('EmployeeDocuments') AND name IN ('UploadedAt','LifecycleChangedAtUtc')")
check(len(timestamps)==2 and all(x['scale']==7 for x in timestamps),'document lifecycle timestamps retain SQL datetime2(7) precision')
actor_fks=rows("SELECT constraint_object_id FROM sys.foreign_key_columns WHERE parent_object_id=OBJECT_ID('EmployeeDocuments') AND COL_NAME(parent_object_id,parent_column_id) IN ('CreatedByUserId','LifecycleChangedByUserId')")
check(not actor_fks,'document actor snapshot IDs have no deletion-cascade FK')
successor=rows("SELECT is_unique,filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID('EmployeeDocuments') AND name='IX_EmployeeDocuments_SupersedesDocumentId'")
check(len(successor)==1 and successor[0]['is_unique'] and 'IS NOT NULL' in successor[0]['filter_definition'],'actual SQL filtered unique successor index retained')
(directory/'d14-final-baseline-results.json').write_text(json.dumps({'checks':len(results),'error':None,'counts':{t:len(v) for t,v in current.items()},'migrations':migrations,'indexes':indexes,'foreignKeys':fks,'results':results},indent=2))
print('PASS:',len(results),'D14 final SQL checks; exact Development baseline restored')

