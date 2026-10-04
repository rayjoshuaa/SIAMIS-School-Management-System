"""Read-only changed-source hygiene and targeted secret scan; never prints suspected secrets."""
import pathlib,subprocess,re,json
ROOT=pathlib.Path(__file__).resolve().parents[1]
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT,text=True).splitlines()
files=sorted(set(git('diff','--name-only')+git('ls-files','--others','--exclude-standard')))
failures=[]
patterns=[r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',r'(?i)(?:password|pwd|client_secret|api_key)\s*[:=]\s*[\"\'][^\"\']{8,}[\"\']',r'\bgh[pousr]_[A-Za-z0-9]{30,}\b',r'\bAKIA[A-Z0-9]{16}\b']
for name in files:
    path=ROOT/name
    if not path.is_file():continue
    for number,line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(),1):
        if line.rstrip()!=line:failures.append(f'{name}:{number} trailing whitespace')
        # This scanner's own regex definitions are patterns, not credentials.
        if name=='tests/verify_d10_repository.py':continue
        if any(re.search(p,line) for p in patterns):failures.append(f'{name}:{number} possible secret')
protected=['PayrollCalculationService','PayrollGenerationService','PayrollPreviewService','EmployeeLeaveService','AttendanceDayService','AttendanceReviewService','AttendanceReportingService','BasicSalaryEntitlementService','PitCalculator','Section33Calculator']
for name in files:
    if any(name.endswith('/'+x+'.cs') for x in protected):failures.append('Out-of-scope calculation/lifecycle source change: '+name)
result={'files':files,'error':failures or None,'targetedSecretScan':'passed' if not failures else 'failed','whitespaceScan':'passed' if not failures else 'failed'}
(ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d10-repository-results.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
if failures:raise SystemExit('\n'.join(failures))
print('PASS:',len(files),'changed source files: targeted secret/whitespace scan; existing domain calculation and lifecycle services unchanged')
