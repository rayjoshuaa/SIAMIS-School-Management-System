"""Fresh D15 execution of established authenticated suites; never reuses prior results."""
import pathlib, subprocess, json
ROOT=pathlib.Path(__file__).resolve().parents[1]
directory=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin'
# Keep approved D10 adapters and unchanged financial expectations. Disable D14 resume logic.
source=(ROOT/'tests/verify_d14_regressions.py').read_text()
source=source.replace("previous=json.loads(previous_path.read_text()) if previous_path.exists() else {}",'previous={}')
source=source.replace('d14-regressions-results.json','d15-domain-regressions-results.json')
source=source.replace("('d14-'+name)","('d15-'+name)")
exec(compile(source,str(ROOT/'tests/verify_d14_regressions.py'),'exec'))
