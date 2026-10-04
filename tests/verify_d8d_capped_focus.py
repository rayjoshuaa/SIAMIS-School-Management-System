"""Focused capped entitlement and concurrency contract; only recorded synthetic Development fixtures."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d8d_live.py').read_text().split('\ntry:\n')[0])
OUT=ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8d-capped-focus-results.json'
observations=[]
try:
    policy(annual,'CAP-2030',True,sandwichParticipation=True,sandwichEquivalentDayMinutes=300)
    policy(annual,'CAP-2031',True,start='2031-01-01',end='2031-12-31',sandwichParticipation=True,sandwichEquivalentDayMinutes=90)
    policy(personal,'CAP-UNTRACKED',False,sandwichParticipation=True,sandwichEquivalentDayMinutes=75)
    for budget in [10000,1000,840]:
        for outcome in ['ReasonAccepted','ReasonNotAccepted']:
            e,c=new('CAP-'+str(budget)+('-A' if outcome=='ReasonAccepted' else '-N'),budget);l,r=pair(e);cc=cases(e)[0]
            check(balance(e)['sandwichPendingMinutes']==balance(e)['sandwichUsedMinutes']==0,'no automatic sandwich consumption')
            command(e,l,'approve');before=complete(r);command(e,r,'approve',status=409);check(complete(r)==before,'final approval pending review unchanged')
            expected=0 if outcome=='ReasonAccepted' else min(600,budget-840)
            result=sandwich_review(e,cc,outcome=outcome)
            check(result['potentialDebitMinutes']==600 and result['appliedDebitMinutes']==expected and result['unabsorbedDebitMinutes']==600-expected,'correct capped review facts '+str(budget)+outcome)
            check(result['committedDebitMinutes']==expected and balance(e)['sandwichPendingMinutes']==expected and balance(e)['availableMinutes']>=0,'capped reservation nonnegative')
            command(e,r,'approve');check(balance(e)['sandwichUsedMinutes']==expected and balance(e)['usedMinutes']==840 and balance(e)['availableMinutes']==budget-840-expected,'final approval charges applied only')
            cancel(e,r,True);released=cases(e)[0]
            check(released['appliedDebitMinutes']==expected and released['committedDebitMinutes']==0 and balance(e)['availableMinutes']==budget-420,'release retains actual applied audit and reverses only that amount')
            check(all(d['scheduledMinutes']==0 for d in result['dates']) and complete(l)[0][0]['Days']==1,'no fictional work or normal Days change')
            sandwich_review(e,cc,409,outcome=outcome)
    e,c=new('ZERO-ACTUAL',840);l,r=pair(e);sandwich_review(e,cases(e)[0],outcome='ReasonNotAccepted')
    exhausted=expect('POST',f'employees/{e}/leave',request('2030-01-15'),201,'D11 exhausted normal Leave becomes unpaid')
    check(exhausted['paidMinutes']==0 and exhausted['unpaidMinutes']==420 and balance(e)['availableMinutes']==0,'unpaid excess does not duplicate sandwich consumption')
    e,c=new('CAP-YEARS',500);entitlement(e,year=2031,minutes=420)
    for d in ['2030-12-31','2031-01-01']:api('POST',f'work-calendars/{c}/overrides',{'date':d,'overrideType':'SchoolHoliday'},201)
    l,r=pair(e,date='2030-12-30',end='2031-01-02');result=sandwich_review(e,cases(e)[0],outcome='ReasonNotAccepted')
    check([(x['potentialDebitMinutes'],x['appliedDebitMinutes'],x['unabsorbedDebitMinutes']) for x in result['allocations']]==[(300,80,220),(90,0,90)],'independent cross-year caps without borrowing')
    command(e,l,'approve');command(e,r,'approve');check(balance(e)['availableMinutes']==balance(e,year=2031)['availableMinutes']==0,'both years bottom at zero')
    for i in range(20):
        e,c=new('CAP-RACE-'+str(i));l,r=pair(e);cc=cases(e)[0];ent=rows('SELECT Id FROM EmployeeLeaveEntitlements WHERE EmployeeId='+ident(e))[0]['Id']
        outcomes=race([('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/review",{'expectedStatus':'ReviewPending','outcome':'ReasonNotAccepted','reason':'Synthetic capped review'}),('POST',f'employees/{e}/leave-entitlements/{ent}/adjustments',{'adjustmentMinutes':-9000,'reason':'Synthetic competing adjustment'})])
        b=balance(e);current=cases(e)[0]
        observations.append({'responses':outcomes,'case':current,'balance':b,'adjustments':rows('SELECT * FROM EmployeeLeaveEntitlementAdjustments WHERE EmployeeLeaveEntitlementId='+ident(ent)),'events':rows('SELECT * FROM EmployeeLeaveSandwichEvents WHERE CaseId='+ident(cc['id']))})
        check((outcomes[0][0],outcomes[1][0]) in [(200,409),(200,201)],'adjustment/review ordered responses '+str(i))
        expected=600 if outcomes[1][0]==409 else 160
        check(current['appliedDebitMinutes']==expected and current['unabsorbedDebitMinutes']==600-expected and b['availableMinutes']>=0 and b['sandwichPendingMinutes']==expected,'adjustment/review no overcommit '+str(i))
        check(len([x for x in current['history'] if x['state']=='ReasonNotAccepted'])==1,'one persisted review event '+str(i))
    for action in ['approve','reject','cancel']:
        e,c=new('CAP-'+action.upper());l,r=pair(e);cc=cases(e)[0];command(e,l,'approve')
        outcomes=race([('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/review",{'expectedStatus':'ReviewPending','outcome':'ReasonNotAccepted','reason':'Synthetic review'}),('POST',f"employees/{e}/leave/{r['leaveId']}/{action}",{'expectedStatus':'Pending'} if action=='cancel' else {})])
        check(all(x[0] in [200,409] for x in outcomes),'review vs '+action+' controlled responses')
        if action=='approve':
            check(outcomes[0][0]==200,'review survives pending-final gate')
            if outcomes[1][0]==409:command(e,r,'approve')
            check(balance(e)['sandwichUsedMinutes']==600,'review/final approval charges once')
        else:check(cases(e)[0]['state']=='Released' and balance(e)['sandwichPendingMinutes']==balance(e)['sandwichUsedMinutes']==0,'review vs '+action+' releases once')
    e,c=new('CAP-DECISIONS');l,r=pair(e);cc=cases(e)[0]
    outcomes=race([('POST',f"employees/{e}/leave-sandwich-cases/{cc['id']}/review",{'expectedStatus':'ReviewPending','outcome':o,'reason':'Synthetic review'}) for o in ['ReasonAccepted','ReasonNotAccepted']])
    check(sorted(x[0] for x in outcomes)==[200,409],'competing outcomes one success one stale state')
    policy(personal,'CAP-DOC',False,start='2031-01-01',end='2031-12-31',supportingDocumentPolicy='AlwaysRequired',documentTypeId=doc)
    e,c=new('CAP-EVIDENCE',None);l=create(e,request('2031-01-06',t=personal),420,'evidence unchanged');command(e,l,'approve',status=409);rr=receipt(e,l);review(e,l,rr);command(e,l,'approve')
    check(api('GET',f"employees/{e}/leave/{l['leaveId']}")['evidence']['approvedEvidenceIds']==[rr['id']],'Accepted evidence preserved')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:cleanup_d8d();check(snapshot()==baseline,'focused exact baseline restoration')
    except Exception as exc:error=(error or '')+' CLEANUP '+str(exc)
    OUT.write_text(json.dumps({'checks':len(results),'error':error,'races':observations},indent=2))
if error:raise SystemExit(error)
print('PASS:',len(results),'focused capped assertions')
