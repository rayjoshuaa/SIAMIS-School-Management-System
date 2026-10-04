"""Read-only D15 repository/SQL inventory. Never creates database fixtures."""
import pathlib, json, subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
baseline=snapshot()
inventory={
 'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
 'status':subprocess.check_output(['git','status','--short'],cwd=ROOT,text=True),
 'sdk':subprocess.check_output(['dotnet','--version'],cwd=ROOT,text=True).strip(),
 'migrations':rows('SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId'),
 'counts':{t:len(v) for t,v in sorted(baseline.items())},
 'columns':rows("SELECT OBJECT_NAME(c.object_id) AS TableName,c.name,TYPE_NAME(c.user_type_id) AS TypeName,c.precision,c.scale,c.max_length,c.is_nullable FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id ORDER BY TableName,c.column_id"),
 'fks':rows("SELECT name,OBJECT_NAME(parent_object_id) AS TableName,OBJECT_NAME(referenced_object_id) AS ReferencedTable,delete_referential_action_desc,is_disabled,is_not_trusted FROM sys.foreign_keys ORDER BY TableName,name"),
 'indexes':rows("SELECT OBJECT_NAME(i.object_id) AS TableName,i.name,i.is_unique,i.filter_definition,STRING_AGG(COL_NAME(ic.object_id,ic.column_id),',') WITHIN GROUP(ORDER BY ic.key_ordinal) AS Columns FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.tables t ON t.object_id=i.object_id WHERE ic.key_ordinal>0 GROUP BY i.object_id,i.name,i.is_unique,i.filter_definition ORDER BY TableName,i.name"),
 'checks':rows("SELECT OBJECT_NAME(parent_object_id) AS TableName,name,definition,is_disabled,is_not_trusted FROM sys.check_constraints ORDER BY TableName,name"),
 'storageExists':(ROOT/'src/SIAMIS.Api/App_Data/hr-documents').exists(),
}
(directory/'d15-baseline.json').write_text(json.dumps(baseline,indent=2))
(directory/'d15-inventory.json').write_text(json.dumps(inventory,indent=2))
assert len(baseline)==85
assert inventory['migrations'][-1]['MigrationId']=='20261004115720_AddSecureHrDocumentFoundation'
assert len(inventory['migrations'])==42
print(json.dumps({k:inventory[k] for k in ['head','sdk','counts','storageExists']},indent=2))
print('Migration count',len(inventory['migrations']),'FK count',len(inventory['fks']),'check count',len(inventory['checks']))
print('Cascade FKs:',[f for f in inventory['fks'] if f['delete_referential_action_desc']!='NO_ACTION'])
