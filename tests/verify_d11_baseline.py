"""Read-only D11 schema/history and exact pre-migration Development baseline verification."""
import pathlib,json
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
original=json.loads((directory/'d11-baseline.json').read_text());current=snapshot()
check(len(current)==85 and current==original,'exact original 85-table rows and timestamps preserved')
check(len(current['Employees'])==1 and not json.loads(current['Employees'][0])['IsActive'] and json.loads(current['Employees'][0])['EmployeeNumber']=='TEST-EMP-001','only TEST-EMP-001 remains inactive')
check(len(current['EmploymentRecords'])==1,'single original EmploymentRecord preserved')
check(len(current['Roles'])==5 and not current['Users'] and not current['SecurityAuditEvents'],'five permanent roles; no temporary users/audits')
master=[t for t,v in original.items() if v and t not in ['Employees','EmploymentRecords','Roles']]
check(sum(len(current[t]) for t in master)==172,'172 original master rows retained')
check(all(not current[t] for t in current if t.startswith('EmployeeLeave') or t.startswith('Attendance') and t!='AttendanceStatuses' or t.startswith('Payroll') and t!='PayrollComponents' or t.startswith('EmployeePayroll')),'temporary Leave Attendance and Payroll fixtures removed')
migrations=rows('SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId');before=json.loads((directory/'d11-migrations-before.json').read_text())
check(migrations[:-1]==before and len(migrations)==41 and migrations[-1]['MigrationId']=='20261004072245_AddLeavePaidUnpaidAllocation','all 40 prior migrations plus only D11 migration')
columns=rows("SELECT name,TYPE_NAME(user_type_id) AS TypeName,is_nullable FROM sys.columns WHERE object_id=OBJECT_ID('EmployeeLeaveAllocations') AND name IN ('PaidMinutes','UnpaidMinutes')")
check(len(columns)==2 and all(c['TypeName']=='int' and c['is_nullable'] for c in columns),'two nullable int classification columns; no backfill')
constraints=rows("SELECT name,definition,is_disabled,is_not_trusted FROM sys.check_constraints WHERE name IN ('CK_LeaveAllocation_Payment','CK_EmployeeLeave_Authoritative')")
check(len(constraints)==2 and all(not c['is_disabled'] and not c['is_not_trusted'] for c in constraints),'focused checks enabled and trusted')
check(any(c['name']=='CK_LeaveAllocation_Payment' and 'CONVERT' in c['definition'] and '[PaidMinutes] IS NULL' in c['definition'] for c in constraints),'payment consistency check preserves legacy null pair')
check(any(c['name']=='CK_EmployeeLeave_Authoritative' and 'CalculationSnapshotVersion]=(2)' in c['definition'] and 'CalculationSnapshotVersion]=(1)' in c['definition'] for c in constraints),'snapshot authority accepts V1 and V2')
fks=rows("SELECT name,delete_referential_action_desc AS DeleteBehavior FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('EmployeeLeaveAllocations')")
check(len(fks)==1 and fks[0]['DeleteBehavior']=='NO_ACTION','allocation ownership FK remains NoAction')
indexes=rows("SELECT name,is_unique FROM sys.indexes WHERE object_id=OBJECT_ID('EmployeeLeaveAllocations') AND name='IX_EmployeeLeaveAllocations_EmployeeLeaveId_LeaveYear'")
check(len(indexes)==1 and indexes[0]['is_unique'],'unique request/year index retained')
(directory/'d11-final-baseline-results.json').write_text(json.dumps({'checks':len(results),'error':None,'counts':{t:len(v) for t,v in current.items()},'migrations':migrations,'columns':columns,'constraints':constraints,'foreignKeys':fks,'indexes':indexes,'results':results},indent=2))
print('PASS:',len(results),'final D11 SQL checks; exact Development baseline restored')
