"""No fixture writes: standard anonymous credential rate limiter and generic responses."""
import pathlib,json,urllib.request,urllib.error
ROOT=pathlib.Path(__file__).resolve().parents[1]
exec((ROOT/'tests/verify_d10_security.py').read_text().split('baseline=snapshot();')[0])
c=Client();token=c.request('GET','/api/auth/csrf')['token'];limited=False
for _ in range(125):
    req=urllib.request.Request(BASE+'/api/auth/forgot-password',data=json.dumps({'email':'missing-rate@example.invalid'}).encode(),headers={'Content-Type':'application/json','X-CSRF-TOKEN':token})
    try:
        with c.opener.open(req,timeout=30) as response:
            assert response.status==200 and set(json.load(response))=={'message'}
    except urllib.error.HTTPError as e:
        assert e.code==429
        limited=True;break
assert limited,'standard credential rate limit must reject excess requests'
(directory/'d13-rate-limit-results.json').write_text(json.dumps({'checks':2,'error':None,'genericResponses':True,'rateLimit429':True},indent=2))
print('PASS: generic anonymous recovery and credential rate limit 429; no fixtures')
