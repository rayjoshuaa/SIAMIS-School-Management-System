using System.Text.Json;
using System.Data;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Employees;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Builds immutable payroll snapshots, committing each employee independently.</summary>
public sealed class PayrollGenerationService(SIAMISDbContext db, IPayrollCalculationService calculator,
    IPayrollRuleEvaluator ruleEvaluator, IPayrollEmploymentContextService employmentContexts,
    IBasicSalaryEntitlementService salaryEntitlements, ISection33PayrollService section33) : IPayrollGenerationService
{
    public async Task<ServiceResult<PayrollGenerationSummary>> GenerateAsync(
        Guid payrollPeriodId, PayrollGenerationRequest request, CancellationToken cancellationToken)
    {
        var period = await db.PayrollPeriods.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PayrollPeriodId == payrollPeriodId, cancellationToken);
        if (period is null) return NotFound("Payroll period was not found.");
        if (period.Status is not ("Open" or "Processing"))
            return Conflict($"Payroll generation is allowed only for Open or Processing periods. Current status: {period.Status}.");

        if (!BasicSalaryPeriod.IsSupported(period.StartDate, period.EndDate))
            return Invalid(BasicSalaryPeriod.ValidationMessage);

        var requestedIds = request.EmployeeIds?.Distinct().ToArray();
        if (requestedIds is { Length: > 0 })
        {
            var existingIds = await db.Employees.AsNoTracking().Where(item => requestedIds.Contains(item.EmployeeId))
                .Select(item => item.EmployeeId).ToListAsync(cancellationToken);
            var missingIds = requestedIds.Except(existingIds).ToArray();
            if (missingIds.Length > 0) return Invalid($"Every EmployeeId must exist. Not found: {string.Join(", ", missingIds)}.");
        }

        var eligibleEmployeeIds = db.EmploymentRecords.Where(EmploymentIntegrity.Overlapping(period.StartDate, period.EndDate))
            .Select(record => record.EmployeeId);
        var candidatesQuery = db.Employees.AsNoTracking().AsQueryable();
        candidatesQuery = requestedIds is { Length: > 0 }
            ? candidatesQuery.Where(item => requestedIds.Contains(item.EmployeeId))
            : candidatesQuery.Where(item => eligibleEmployeeIds.Contains(item.EmployeeId));
        var candidates = await candidatesQuery.OrderBy(item => item.EmployeeNumber).ThenBy(item => item.EmployeeId)
            .Select(item => new EmployeeCandidate(item.EmployeeId, item.EmployeeNumber)).ToListAsync(cancellationToken);

        var basicSalaryComponent = await GetBasicSalaryComponentAsync(cancellationToken);
        if (basicSalaryComponent is null)
        {
            var count = await db.PayrollComponents.AsNoTracking()
                .CountAsync(item => item.Name.Trim().ToLower() == "basic salary", cancellationToken);
            return Configuration(count != 1
                ? $"Payroll generation requires exactly one PayrollComponent named 'Basic Salary'; found {count}."
                : "The existing Basic Salary component must be active, have a code, and be an Earning component.");
        }

        var results = new List<PayrollGenerationEmployeeResult>(candidates.Count);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await GenerateEmployeeAsync(period, candidate, basicSalaryComponent, request.ForceRegenerate, cancellationToken));
        }

        return ServiceResult<PayrollGenerationSummary>.Success(new PayrollGenerationSummary(payrollPeriodId, results.Count,
            results.Count(item => item.Status == "Generated"), results.Count(item => item.Status == "Skipped"),
            results.Count(item => item.Status == "Failed"), results));
    }

    private async Task<PayrollGenerationEmployeeResult> GenerateEmployeeAsync(PayrollPeriod period, EmployeeCandidate candidate,
        Domain.Entities.MasterData.PayrollComponent basicSalaryComponent, bool forceRegenerate, CancellationToken cancellationToken)
    {
        EmployeePayroll? existingPayroll = null;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            // Reload under the shared parent lock: the batch's earlier period read cannot authorize a later mutation.
            var lockedPeriod = await PayrollPeriodLock.GetAsync(db, period.PayrollPeriodId, cancellationToken);
            if (lockedPeriod is null) return Skipped(candidate, "Payroll period no longer exists.");
            if (!PayrollPeriodLock.AllowsMutation(lockedPeriod.Status))
                return Skipped(candidate, PayrollPeriodLock.ConflictMessage(lockedPeriod.Status));
            period = lockedPeriod;
            // D1 lifecycle writers use the same employee lock. Hold it through eligibility,
            // targeting and snapshot commit so employment cannot change between these steps.
            var employee = await EmploymentIntegrity.LockAsync(db, candidate.EmployeeId, cancellationToken);
            if (employee is null) return Skipped(candidate, "Employee no longer exists.");

            existingPayroll = await db.EmployeePayrolls.Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.PayrollPeriodId == period.PayrollPeriodId && item.EmployeeId == employee.EmployeeId, cancellationToken);
            var existing = existingPayroll;
            if (existing is not null)
            {
                if (existing.Status is "Approved" or "Paid" or "Cancelled") return Skipped(candidate, $"Existing {existing.Status} payroll is protected and cannot be regenerated.", existing);
                if (!forceRegenerate && existing.Status == "Calculated") return Skipped(candidate, $"Existing {existing.Status} payroll was skipped because forceRegenerate is false.", existing);
                if (existing.Status is not ("Draft" or "Calculated"))
                    return Failed(candidate, $"Existing payroll status '{existing.Status}' cannot be regenerated.", existing);
            }

            // Refresh classification under the transaction; the earlier batch read is not a historical snapshot.
            var currentBasicSalaryComponent = await GetBasicSalaryComponentAsync(cancellationToken);
            if (currentBasicSalaryComponent is null)
                return Failed(candidate, WithExistingPayroll("Basic Salary component configuration changed or is invalid.", existing), existing);
            basicSalaryComponent = currentBasicSalaryComponent;

            var contexts = await employmentContexts.ResolveAsync([employee.EmployeeId], period.StartDate, period.EndDate, cancellationToken);
            var employment = contexts[employee.EmployeeId];
            if (!employment.IsSuccess) return Failed(candidate, WithExistingPayroll(employment.Failure!.Message, existing), existing);
            if (employment.Value is null) return Skipped(candidate, "Employee has no employment overlapping this payroll period and is not eligible.", existing);

            var entitlement = await salaryEntitlements.CalculateAsync(employee.EmployeeId, period.StartDate, period.EndDate, cancellationToken);
            if (entitlement.Status == "Skipped") return Skipped(candidate, WithExistingPayroll(entitlement.Message, existing), existing);
            if (entitlement.Status != "Calculated") return Failed(candidate, WithExistingPayroll(entitlement.Message, existing), existing);

            var assignments = await db.EmployeePayrollComponentAssignments.AsNoTracking().Include(item => item.PayrollComponent)
                .Where(item => item.EmployeeId == employee.EmployeeId && item.EffectiveFrom <= period.StartDate
                    && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= period.StartDate))
                .OrderBy(item => item.PayrollComponent.Category).ThenBy(item => item.PayrollComponent.Code).ThenBy(item => item.PayrollComponent.Id)
                .ToListAsync(cancellationToken);
            var inactive = assignments.FirstOrDefault(item => !item.PayrollComponent.IsActive);
            if (inactive is not null) return Failed(candidate, WithExistingPayroll($"Assignment references inactive payroll component '{inactive.PayrollComponent.Name}'.", existing), existing);
            if (assignments.Any(item => item.PayrollComponentId == basicSalaryComponent.Id))
                return Failed(candidate, WithExistingPayroll("Basic Salary is generated from EmployeeCompensation and must not also have an employee component assignment.", existing), existing);

            var evaluation = await ruleEvaluator.EvaluateApplicableRulesAsync(period.PayrollPeriodId, employee.EmployeeId, employment.Value, cancellationToken);
            if (evaluation.Failure is not null)
                return Failed(candidate, WithExistingPayroll($"Payroll rule evaluation failed: {evaluation.Failure.Message}", existing), existing);
            var calculated = calculator.Calculate(ToCalculationEmployee(employee), entitlement.Snapshot!.Amount, assignments,
                basicSalaryComponent, evaluation.Value!.ApplicableRules);
            if (calculated.Status != "Calculated") return Failed(candidate, WithExistingPayroll(calculated.Message, existing), existing);

            var statutory = await section33.CalculateAsync(employee.EmployeeId, period, entitlement.Snapshot.Currency, calculated, cancellationToken);
            if (!statutory.IsSuccess) return Failed(candidate, WithExistingPayroll(statutory.Failure!.Message, existing), existing);
            calculated = statutory.Value!.Calculation;

            if (existing is not null)
            {
                var previousResults = await db.EmployeePayrollStatutoryResults.Include(x => x.SocialSecurity)
                    .Where(x => x.EmployeePayrollId == existing.EmployeePayrollId).ToListAsync(cancellationToken);
                db.EmployeePayrollSocialSecurityResults.RemoveRange(previousResults.Select(x => x.SocialSecurity));
                db.EmployeePayrollStatutoryResults.RemoveRange(previousResults);
                db.EmployeePayrollLines.RemoveRange(existing.Lines);
                db.EmployeePayrolls.Remove(existing);
                await db.SaveChangesAsync(cancellationToken);
            }

            var payroll = new EmployeePayroll
            {
                PayrollPeriodId = period.PayrollPeriodId,
                EmployeeId = employee.EmployeeId,
                BasicSalary = calculated.BasicSalary,
                GrossPay = calculated.GrossPay,
                TotalDeductions = calculated.TotalDeductions,
                NetPay = calculated.NetPay,
                TaxableEarnings = calculated.TaxableEarnings,
                Status = "Calculated",
                ApprovedAt = null,
                PaidAt = null,
                CancelledAt = null,
                CancellationReason = null,
                Lines = calculated.Lines.Select(line => new EmployeePayrollLine
                {
                    PayrollComponentId = line.PayrollComponentId,
                    SourceType = line.SourceType,
                    SourceId = line.SourceId,
                    BasicSalaryCalculationSnapshotJson = line.SourceType == "BasicSalary"
                        ? JsonSerializer.Serialize(entitlement.Snapshot!, SnapshotJsonOptions) : null,
                    ComponentCode = line.ComponentCode,
                    ComponentName = line.ComponentName,
                    ComponentType = line.ComponentType,
                    IsTaxableSnapshot = line.IsTaxableSnapshot,
                    IsStatutorySnapshot = line.IsStatutorySnapshot,
                    ContributionSideSnapshot = line.ContributionSideSnapshot,
                    SsoWageTreatmentSnapshot = line.SsoWageTreatmentSnapshot,
                    PitIncomeTreatmentSnapshot = line.PitIncomeTreatmentSnapshot,
                    PitPaymentTreatmentSnapshot = line.PitPaymentTreatmentSnapshot,
                    Amount = line.Amount,
                    Quantity = line.Quantity,
                    Rate = line.Rate,
                    CalculationMethodSnapshot = line.CalculationMethod,
                    RuleCode = line.RuleCode,
                    RuleName = line.RuleName,
                    ApplicationMode = line.ApplicationMode,
                    BaseType = line.BaseType,
                    BaseAmount = line.BaseAmount,
                    MinimumBase = line.MinimumBase,
                    MaximumBase = line.MaximumBase,
                    CalculationRate = line.PayrollRuleId.HasValue ? line.Rate : null,
                    Remarks = line.Remarks
                }).ToList()
            };
            db.EmployeePayrolls.Add(payroll);
            if (statutory.Value.Result is { } statutoryResult)
            {
                statutoryResult.EmployeePayrollId = payroll.EmployeePayrollId;
                statutoryResult.SocialSecurity.EmployeePayrollStatutoryResultId = statutoryResult.EmployeePayrollStatutoryResultId;
                db.EmployeePayrollStatutoryResults.Add(statutoryResult);
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PayrollGenerationEmployeeResult(employee.EmployeeId, employee.EmployeeNumber, "Generated",
                payroll.EmployeePayrollId, payroll.GrossPay, payroll.TotalDeductions, payroll.NetPay,
                existing is null ? "Payroll generated." : "Payroll regenerated.", payroll.TaxableEarnings);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            var message = "Payroll generation failed while saving this employee; no partial payroll was retained.";
            if (existingPayroll is not null) message += " The previous payroll snapshot was left unchanged.";
            return Failed(candidate, message, existingPayroll);
        }
        finally { db.ChangeTracker.Clear(); }
    }

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<Domain.Entities.MasterData.PayrollComponent?> GetBasicSalaryComponentAsync(CancellationToken ct)
    {
        var matches = await db.PayrollComponents.AsNoTracking()
            .Where(item => item.Name.Trim().ToLower() == "basic salary").Take(2).ToListAsync(ct);
        return matches.Count == 1 && matches[0].IsActive && matches[0].Category == "Earning" && !string.IsNullOrWhiteSpace(matches[0].Code)
            ? matches[0] : null;
    }

    private static PayrollGenerationEmployeeResult Skipped(EmployeeCandidate e, string m, EmployeePayroll? p = null)
        => new(e.EmployeeId, e.EmployeeNumber, "Skipped", p?.EmployeePayrollId, p?.GrossPay, p?.TotalDeductions, p?.NetPay, m, p?.TaxableEarnings);
    private static PayrollGenerationEmployeeResult Failed(EmployeeCandidate e, string m, EmployeePayroll? p = null)
        => new(e.EmployeeId, e.EmployeeNumber, "Failed", p?.EmployeePayrollId, p?.GrossPay, p?.TotalDeductions, p?.NetPay, m, p?.TaxableEarnings);
    private static string WithExistingPayroll(string m, EmployeePayroll? p) => p is null ? m : $"{m} The previous payroll snapshot was left unchanged.";
    private static PayrollCalculationEmployee ToCalculationEmployee(Domain.Entities.Employees.Employee employee)
        => new(employee.EmployeeId, employee.EmployeeNumber, string.Join(' ', new[] { string.IsNullOrWhiteSpace(employee.PreferredName) ? employee.FirstName : employee.PreferredName, employee.MiddleName, employee.LastName }.Where(item => !string.IsNullOrWhiteSpace(item))));
    private static ServiceResult<PayrollGenerationSummary> Invalid(string m) => ServiceResult<PayrollGenerationSummary>.Fail("validation", m);
    private static ServiceResult<PayrollGenerationSummary> NotFound(string m) => ServiceResult<PayrollGenerationSummary>.Fail("not_found", m);
    private static ServiceResult<PayrollGenerationSummary> Conflict(string m) => ServiceResult<PayrollGenerationSummary>.Fail("conflict", m);
    private static ServiceResult<PayrollGenerationSummary> Configuration(string m) => ServiceResult<PayrollGenerationSummary>.Fail("configuration", m);
    private sealed record EmployeeCandidate(Guid EmployeeId, string EmployeeNumber);
}
