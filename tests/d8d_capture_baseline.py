import pathlib
exec(pathlib.Path(__file__).with_name('verify_d5a_live.py').read_text().split('baseline=snapshot()')[0])
b=snapshot()
check(len(b)==66,'D8D initial 66-table Development baseline')
check(len(b['Employees'])==1 and json.loads(b['Employees'][0])['IsActive'] is False and len(b['EmploymentRecords'])==1,'original single inactive employee/employment')
check(all(not b[t] for t in b if t.startswith('EmployeeLeave') or t.startswith('WorkCalendar') or t=='LeavePolicies'),'empty operational leave baseline')
(ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8d-before-migration.json').write_text(json.dumps(b),encoding='utf-8')
print('Captured exact baseline,',len(b),'tables')
