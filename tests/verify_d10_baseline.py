"""Read-only final D10 SQL schema/seed verification and exact pre-migration HR row comparison."""
import pathlib,json
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
original=json.loads((directory/'d10-baseline.json').read_text())
current=snapshot()
check(len(current)==85 and len(original)==77,'eight focused new tables; 85 application tables')
check(all(current[t]==v for t,v in original.items()),'exact original 77-table rows/timestamps preserved')
security=['Users','Roles','UserRoles','UserClaims','UserLogins','UserTokens','RoleClaims','SecurityAuditEvents']
check(len(current['Roles'])==5 and all(not current[t] for t in security if t!='Roles'),'five permanent roles; all temporary security fixtures removed')
check(len(current['Employees'])==1 and not json.loads(current['Employees'][0])['IsActive'],'TEST-EMP-001 only and inactive')
migrations=rows('SELECT MigrationId,ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId')
check(len(migrations)==40 and migrations[-1]['MigrationId']=='20261004053737_AddHrIdentityAndSecurityFoundation','one D10 migration applied; 40 total')
schema=rows("SELECT OBJECT_NAME(c.object_id) AS TableName,c.name AS ColumnName,TYPE_NAME(c.user_type_id) AS TypeName,c.max_length,c.scale,c.is_nullable FROM sys.columns c WHERE OBJECT_NAME(c.object_id) IN ("+','.join("'"+x+"'" for x in security)+')')
check(all(x['TypeName']=='datetime2' and x['scale']==7 for x in schema if x['ColumnName'] in ['CreatedAtUtc','OccurredAtUtc']),'security UTC datetime2(7) columns')
check(all(x['max_length']==256 for x in schema if x['TableName'] in ['UserLogins','UserTokens'] and x['ColumnName'] in ['LoginProvider','ProviderKey','Name']),'bounded 128-character composite Identity key columns')
fks=rows("SELECT name,delete_referential_action_desc AS DeleteBehavior FROM sys.foreign_keys WHERE OBJECT_NAME(parent_object_id) IN ("+','.join("'"+x+"'" for x in security)+')')
check(any(x['name']=='FK_Users_Employees_EmployeeId' and x['DeleteBehavior']=='NO_ACTION' for x in fks),'User employee linkage cannot cascade HR deletion')
constraints=rows("SELECT name,is_disabled,is_not_trusted,definition FROM sys.check_constraints WHERE name IN ('CK_AttendanceEvent_SourceFields','CK_AttendanceReviewAction_Shape')")
check(len(constraints)==2 and all(not x['is_disabled'] and not x['is_not_trusted'] for x in constraints),'updated actor checks enabled and trusted')
check(any('Authenticated' in x['definition'] and 'DevelopmentUnattributed' in x['definition'] for x in constraints),'authenticated actors plus legacy review origin retained')
out={'checks':len(results),'error':None,'counts':{t:len(v) for t,v in current.items()},'migrations':migrations,'securitySchema':schema,'securityForeignKeys':fks,'actorChecks':constraints,'results':results}
(directory/'d10-final-baseline-results.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
print('PASS:',len(results),'final D10 SQL assertions; exact original 77-table baseline plus five seeded roles')
