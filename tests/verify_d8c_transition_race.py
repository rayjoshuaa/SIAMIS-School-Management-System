"""Regression for the approved cancellation ExpectedStatus contract, including the original WITH-remarks race."""
import pathlib
exec(pathlib.Path(__file__).with_name('verify_d8c_live.py').read_text(encoding='utf-8').split('\ntry:\n')[0])
PREFIX='D8C-RACE-';cases=[];error=None
try:
    check(len(baseline)==66 and len(baseline['Employees'])==1 and not baseline['EmployeeLeave'],'isolated race baseline')
    e=employee('EMP');c=calendar('CALENDAR',[('08:00','12:00'),('13:00','16:00')]);assign(e,c)
    p,_=policy(annual,'POLICY',True);entitlement(e,minutes=420)
    for i in range(20):
        leave=create(e,request(),420,'race regression '+str(i));original=frozen(leave);header_before=complete(leave)[0][0]
        calls=[('POST',f"employees/{e}/leave/{leave['leaveId']}/approve",{'reviewRemarks':'Synthetic race approval'}),('POST',f"employees/{e}/leave/{leave['leaveId']}/cancel",{'expectedStatus':'Pending','cancellationRemarks':'Synthetic reason supplied while request is Pending'})]
        outcomes=race(calls if i%2==0 else list(reversed(calls)))
        if i%2:outcomes.reverse()
        detail=api('GET',f"employees/{e}/leave/{leave['leaveId']}")
        cases.append({'iteration':i,'approveStatus':outcomes[0][0],'cancelStatus':outcomes[1][0],'finalStatus':detail['status'],'reviewedAt':detail['reviewedAt'],'cancelledAt':detail['cancelledAt']})
        check(sorted(code for code,_ in outcomes)==[200,409],'exactly one success and one conflict '+str(i))
        loser=next(body for code,body in outcomes if code==409)
        check(loser['detail'] in ['Leave status no longer matches ExpectedStatus.','This lifecycle transition is not permitted.'],'loser is state conflict, no deadlock '+str(i))
        expected='Approved' if outcomes[0][0]==200 else 'Cancelled'
        check(detail['status']==expected,'final state matches winning command '+str(i))
        check(frozen(leave)==original,'race never rewrites snapshot or allocations '+str(i))
        b=balance(e)
        check(b['pendingMinutes']==0 and b['usedMinutes']==(420 if expected=='Approved' else 0) and b['availableMinutes']==(0 if expected=='Approved' else 420),'balance exactly matches winner '+str(i))
        header_after=complete(leave)[0][0]
        changes={k for k in header_before.keys()|header_after.keys() if header_before.get(k)!=header_after.get(k)}
        allowed={'Status','ReviewedAt','ReviewRemarks'} if expected=='Approved' else {'Status','CancelledAt','CancellationRemarks'}
        check(changes==allowed,'only successful transition audit fields changed '+str(i))
        check((detail['reviewedAt'] is not None and detail['cancelledAt'] is None and detail['cancellationRemarks'] is None) if expected=='Approved' else (detail['reviewedAt'] is None and detail['reviewRemarks'] is None and detail['cancelledAt'] is not None),'losing command writes no timestamp/remarks '+str(i))
        if detail['status']=='Approved':cancel(e,leave,True)
    later=create(e,request(),420,'deliberate approved cancellation fixture');review=command(e,later,'approve',{'reviewRemarks':'Deliberate review'})
    before=complete(later);before_balance=balance(e)
    command(e,later,'cancel',{'expectedStatus':'Pending','cancellationRemarks':'Stale intent'},409)
    check(complete(later)==before and balance(e)==before_balance,'stale Pending intent on Approved changes nothing')
    command(e,later,'cancel',{'expectedStatus':'Approved'},400)
    check(complete(later)==before and balance(e)==before_balance,'Approved cancellation still requires reason and changes nothing on validation failure')
    result=cancel(e,later,True)
    check(result['status']=='Cancelled' and result['reviewedAt']==review['reviewedAt'] and balance(e)['usedMinutes']==0, 'deliberate Approved cancellation releases usage and preserves review')
    check(complete(later)[1]==before[1] and complete(later)[0][0]['CalculationSnapshotJson']==before[0][0]['CalculationSnapshotJson'],'deliberate cancellation preserves snapshot and allocations')
except Exception as exc:error=str(exc);traceback.print_exc()
finally:
    try:
        statements=[]
        if employee_ids:
            ids=','.join(map(ident,employee_ids));statements += [f'DELETE EmployeeLeaveAllocations WHERE EmployeeLeaveId IN (SELECT LeaveId FROM EmployeeLeave WHERE EmployeeId IN ({ids}))',f'DELETE EmployeeLeave WHERE EmployeeId IN ({ids})',f'DELETE EmployeeLeaveEntitlementAdjustments WHERE EmployeeLeaveEntitlementId IN (SELECT Id FROM EmployeeLeaveEntitlements WHERE EmployeeId IN ({ids}))',f'DELETE EmployeeLeaveEntitlements WHERE EmployeeId IN ({ids})',f'DELETE EmployeeWorkCalendarAssignments WHERE EmployeeId IN ({ids})',f'DELETE EmploymentRecords WHERE EmployeeId IN ({ids})',f'DELETE Employees WHERE EmployeeId IN ({ids})']
        if policy_ids:statements.append('DELETE LeavePolicies WHERE Id IN ('+','.join(map(ident,policy_ids))+')')
        if calendar_ids:
            ids=','.join(map(ident,calendar_ids));statements += [f'DELETE WorkCalendarWeeklyIntervals WHERE WorkCalendarId IN ({ids})',f'DELETE WorkCalendars WHERE Id IN ({ids})']
        if statements:sql('SET XACT_ABORT ON; BEGIN TRAN; '+';'.join(statements)+';COMMIT;')
        check(snapshot()==baseline,'exact race probe baseline restored')
    except Exception as exc:error=(error or '')+' CLEANUP: '+str(exc)
    (ROOT/'tests/SIAMIS.Payroll.RegressionTests/bin/d8c-transition-race-results.json').write_text(json.dumps({'contractStop':False,'cases':cases,'checks':len(results),'error':error},indent=2),encoding='utf-8')
if error:raise SystemExit(error)
print(f'PASS: {len(results)} assertions; all 20 races one success/one 409; exact baseline restored.',flush=True)
