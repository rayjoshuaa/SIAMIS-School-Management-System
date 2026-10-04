"""D12 changed-file hygiene; protect calculation code and scan without printing secret values."""
import pathlib
ROOT=pathlib.Path(__file__).resolve().parents[1]
source=(ROOT/'tests/verify_d10_repository.py').read_text()
source=source.replace('d10-repository-results.json','d12-repository-results.json')
exec(compile(source,str(ROOT/'tests/verify_d10_repository.py'),'exec'))
