using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Stores employee inputs only. No legal eligibility, allowance, contribution or withholding calculation.</summary>
public sealed class EmployeeStatutoryService(SIAMISDbContext db) : IEmployeeStatutoryService
{
    private const decimal Maximum = 999999999999999.9999m;

    public async Task<ServiceResult<IReadOnlyList<StatutoryEnrollmentSummaryDto>>> ListEnrollmentsAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return Missing<IReadOnlyList<StatutoryEnrollmentSummaryDto>>();
        return ServiceResult<IReadOnlyList<StatutoryEnrollmentSummaryDto>>.Success(await db.EmployeeStatutoryEnrollments.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId).OrderBy(x => x.StatutorySchemeId).ThenBy(x => x.EffectiveFrom)
            .Select(x => new StatutoryEnrollmentSummaryDto(x.EmployeeStatutoryEnrollmentId, x.EmployeeId,
                x.StatutorySchemeId, x.EffectiveFrom, x.EffectiveTo, x.Applicability, x.CreatedAt, x.UpdatedAt)).ToListAsync(ct));
    }

    public async Task<ServiceResult<StatutoryEnrollmentDto>> GetEnrollmentAsync(Guid employeeId, Guid id, CancellationToken ct)
    {
        var x = await db.EmployeeStatutoryEnrollments.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeStatutoryEnrollmentId == id, ct);
        return x is null ? Missing<StatutoryEnrollmentDto>() : ServiceResult<StatutoryEnrollmentDto>.Success(EnrollmentDto(x));
    }

    public async Task<ServiceResult<StatutoryEnrollmentDto>> CreateEnrollmentAsync(Guid employeeId, StatutoryEnrollmentRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Missing<StatutoryEnrollmentDto>();
        if (!r.StatutorySchemeId.HasValue || !r.EffectiveFrom.HasValue || r.EffectiveTo < r.EffectiveFrom)
            return Invalid<StatutoryEnrollmentDto>("Scheme and EffectiveFrom are required; inclusive EffectiveTo cannot precede EffectiveFrom.");
        var status = Canonical(r.Applicability, "Applicable", "NotApplicable");
        if (status is null) return Invalid<StatutoryEnrollmentDto>("Applicability must be Applicable or NotApplicable. Absence represents Unknown.");
        var scheme = await db.StatutorySchemes.AsNoTracking().SingleOrDefaultAsync(x => x.StatutorySchemeId == r.StatutorySchemeId, ct);
        if (scheme is null) return Missing<StatutoryEnrollmentDto>();
        if (!scheme.IsActive || scheme.Jurisdiction != "TH" || !(scheme.SchemeType == "SocialSecurity"
            || (scheme.SchemeType == "PersonalIncomeTax" && scheme.Code == "TH-PIT")))
            return Invalid<StatutoryEnrollmentDto>("Enrollment requires an active Thai SocialSecurity scheme or TH-PIT PersonalIncomeTax scheme.");
        if (await db.EmployeeStatutoryEnrollments.AnyAsync(x => x.EmployeeId == employeeId && x.StatutorySchemeId == r.StatutorySchemeId
            && (!x.EffectiveTo.HasValue || x.EffectiveTo >= r.EffectiveFrom) && (!r.EffectiveTo.HasValue || x.EffectiveFrom <= r.EffectiveTo), ct))
            return Conflict<StatutoryEnrollmentDto>("Enrollment intervals for the employee and scheme overlap. Inclusive boundaries cannot share a date.");
        var row = new EmployeeStatutoryEnrollment { EmployeeId = employeeId, StatutorySchemeId = scheme.StatutorySchemeId,
            EffectiveFrom = r.EffectiveFrom.Value, EffectiveTo = r.EffectiveTo, Applicability = status,
            MembershipNumber = Clean(r.MembershipNumber), Remarks = Clean(r.Remarks) };
        db.EmployeeStatutoryEnrollments.Add(row);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<StatutoryEnrollmentDto>.Success(EnrollmentDto(row));
    }

    public async Task<ServiceResult<StatutoryEnrollmentDto>> EndEnrollmentAsync(Guid employeeId, Guid id, StatutoryEnrollmentEndRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Missing<StatutoryEnrollmentDto>();
        var x = await db.EmployeeStatutoryEnrollments.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeStatutoryEnrollmentId == id, ct);
        if (x is null) return Missing<StatutoryEnrollmentDto>();
        if (x.EffectiveTo.HasValue) return Conflict<StatutoryEnrollmentDto>("A closed enrollment interval cannot be rewritten.");
        if (!r.EffectiveTo.HasValue || r.EffectiveTo < x.EffectiveFrom || r.EffectiveTo < DateOnly.FromDateTime(DateTime.UtcNow))
            return Invalid<StatutoryEnrollmentDto>("An open enrollment may be ended on today or a future date, at or after EffectiveFrom; historical backdating is not supported.");
        x.EffectiveTo = r.EffectiveTo;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<StatutoryEnrollmentDto>.Success(EnrollmentDto(x));
    }

    public async Task<ServiceResult<StatutoryEnrollmentResolution>> ResolveEnrollmentAsync(Guid employeeId, Guid schemeId, DateOnly date, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct) || !await db.StatutorySchemes.AnyAsync(x => x.StatutorySchemeId == schemeId, ct))
            return Missing<StatutoryEnrollmentResolution>();
        var rows = await db.EmployeeStatutoryEnrollments.AsNoTracking().Where(x => x.EmployeeId == employeeId && x.StatutorySchemeId == schemeId
            && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date)).Take(2).ToListAsync(ct);
        if (rows.Count > 1) return Conflict<StatutoryEnrollmentResolution>("Ambiguous enrollment coverage; no applicability was selected.");
        var x = rows.SingleOrDefault();
        return ServiceResult<StatutoryEnrollmentResolution>.Success(new(x?.Applicability ?? "Unknown",
            x is null ? "No enrollment covers the date. Unknown is not NotApplicable." : "Explicit employee applicability; no statutory calculation.",
            x is null ? null : new(x.EmployeeStatutoryEnrollmentId, x.EmployeeId, x.StatutorySchemeId, x.EffectiveFrom, x.EffectiveTo, x.Applicability, x.CreatedAt, x.UpdatedAt)));
    }

    public async Task<ServiceResult<EmployeeTaxProfileDto?>> GetProfileAsync(Guid employeeId, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return Missing<EmployeeTaxProfileDto?>();
        var x = await db.EmployeeTaxProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == employeeId, ct);
        return ServiceResult<EmployeeTaxProfileDto?>.Success(x is null ? null : ProfileDto(x));
    }

    public async Task<ServiceResult<EmployeeTaxProfileDto>> SetProfileAsync(Guid employeeId, EmployeeTaxProfileRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Missing<EmployeeTaxProfileDto>();
        var x = await db.EmployeeTaxProfiles.SingleOrDefaultAsync(x => x.EmployeeId == employeeId, ct);
        if (x is null) { x = new() { EmployeeId = employeeId }; db.EmployeeTaxProfiles.Add(x); }
        x.TaxpayerIdentificationNumber = Clean(r.TaxpayerIdentificationNumber);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<EmployeeTaxProfileDto>.Success(ProfileDto(x));
    }

    public async Task<ServiceResult<IReadOnlyList<EmployeeTaxDeclarationSummaryDto>>> ListDeclarationsAsync(Guid employeeId, int? year, CancellationToken ct)
    {
        if (!await EmployeeExists(employeeId, ct)) return Missing<IReadOnlyList<EmployeeTaxDeclarationSummaryDto>>();
        if (year is < 1 or > 9999) return Invalid<IReadOnlyList<EmployeeTaxDeclarationSummaryDto>>("TaxYear must be between 1 and 9999.");
        var rows = await db.EmployeeTaxDeclarations.AsNoTracking().Where(x => x.EmployeeId == employeeId && (!year.HasValue || x.TaxYear == year))
            .OrderBy(x => x.TaxYear).ThenBy(x => x.RevisionNumber).Select(x => new EmployeeTaxDeclarationSummaryDto(
                x.EmployeeTaxDeclarationId, x.EmployeeId, x.TaxYear, x.RevisionNumber, x.ReplacesDeclarationId, x.Status,
                db.EmployeeTaxDeclarationSelections.Any(s => s.EmployeeId == employeeId && s.TaxYear == x.TaxYear && s.CurrentDeclarationId == x.EmployeeTaxDeclarationId),
                x.VerifiedAt, x.Remarks, x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<EmployeeTaxDeclarationSummaryDto>>.Success(rows);
    }

    public async Task<ServiceResult<EmployeeTaxDeclarationDto>> GetDeclarationAsync(Guid employeeId, Guid id, CancellationToken ct)
    {
        var x = await Declarations(false).SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeTaxDeclarationId == id, ct);
        return x is null ? Missing<EmployeeTaxDeclarationDto>() : ServiceResult<EmployeeTaxDeclarationDto>.Success(await DeclarationDto(x, ct));
    }

    public async Task<ServiceResult<EmployeeTaxDeclarationDto>> CreateDeclarationAsync(Guid employeeId, EmployeeTaxDeclarationCreateRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Missing<EmployeeTaxDeclarationDto>();
        if (r.TaxYear is null or < 1 or > 9999 || r.TotalLivingLawfulChildren is < 0) return Invalid<EmployeeTaxDeclarationDto>("TaxYear must be between 1 and 9999; living lawful-child count must be nonnegative.");
        if (await db.EmployeeTaxDeclarations.AnyAsync(x => x.EmployeeId == employeeId && x.TaxYear == r.TaxYear && x.Status == "Draft", ct))
            return Conflict<EmployeeTaxDeclarationDto>("Only one Draft revision may exist for the employee and tax year.");
        var last = await db.EmployeeTaxDeclarations.Where(x => x.EmployeeId == employeeId && x.TaxYear == r.TaxYear).MaxAsync(x => (int?)x.RevisionNumber, ct) ?? 0;
        if (last == int.MaxValue) return Conflict<EmployeeTaxDeclarationDto>("Revision number range is exhausted.");
        var selection = await db.EmployeeTaxDeclarationSelections.SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.TaxYear == r.TaxYear, ct);
        var x = new EmployeeTaxDeclaration { EmployeeId = employeeId, TaxYear = r.TaxYear.Value, RevisionNumber = last + 1,
            ReplacesDeclarationId = selection?.CurrentDeclarationId, Remarks = Clean(r.Remarks), TotalLivingLawfulChildren = r.TotalLivingLawfulChildren };
        db.EmployeeTaxDeclarations.Add(x);
        await db.SaveChangesAsync(ct);
        var dto = await DeclarationDto(x, ct);
        await tx.CommitAsync(ct);
        // Replacement inputs are authored explicitly; no prior balances or claims are silently copied.
        return ServiceResult<EmployeeTaxDeclarationDto>.Success(dto);
    }

    public Task<ServiceResult<EmployeeTaxDeclarationDto>> UpdateDeclarationAsync(Guid employeeId, Guid id, EmployeeTaxDeclarationUpdateRequest r, CancellationToken ct)
        => DraftMutation(employeeId, id, (x, _) => {
            if (r.TotalLivingLawfulChildren is < 0) return Task.FromResult<ApiFailure?>(new("validation", "TotalLivingLawfulChildren must be nonnegative."));
            x.Remarks = Clean(r.Remarks); x.TotalLivingLawfulChildren = r.TotalLivingLawfulChildren;
            return Task.FromResult<ApiFailure?>(null);
        }, ct);

    public async Task<ServiceResult<bool>> DeleteDeclarationAsync(Guid employeeId, Guid id, CancellationToken ct)
        => BooleanResult(await DraftMutation(employeeId, id, (x, _) => {
            db.EmployeeTaxClaims.RemoveRange(x.Claims);
            if (x.OpeningBalance is not null) db.EmployeeTaxOpeningBalances.Remove(x.OpeningBalance);
            db.EmployeeTaxDeclarations.Remove(x);
            return Task.FromResult<ApiFailure?>(null);
        }, ct));

    public Task<ServiceResult<EmployeeTaxDeclarationDto>> SetClaimAsync(Guid employeeId, Guid id, Guid? claimId, EmployeeTaxClaimRequest r, CancellationToken ct)
        => DraftMutation(employeeId, id, (x, _) => {
            var type = Canonical(r.ClaimType, "Spouse", "Child", "Parent");
            var error = ClaimInputError(type, r.Amount, r.Quantity, r.ChildRelationshipType, r.AdditionalChildAllowanceEligible, r.Reference);
            if (error is not null) return Task.FromResult<ApiFailure?>(new("validation", error));
            if (type == "Spouse" && x.Claims.Any(c => c.ClaimType == "Spouse" && c.EmployeeTaxClaimId != claimId))
                return Task.FromResult<ApiFailure?>(new("validation", "Only one Spouse claim is allowed per declaration."));
            var c = claimId.HasValue ? x.Claims.SingleOrDefault(c => c.EmployeeTaxClaimId == claimId) : null;
            if (claimId.HasValue && c is null) return Task.FromResult<ApiFailure?>(new("not_found", "Claim was not found under this declaration."));
            if (c is null) { c = new() { EmployeeTaxDeclarationId = id }; x.Claims.Add(c); db.EmployeeTaxClaims.Add(c); }
            c.ClaimType = type!; c.Amount = r.Amount; c.Quantity = r.Quantity; c.Reference = Clean(r.Reference); c.Remarks = Clean(r.Remarks);
            c.Quantity = type == "Spouse" ? 1 : r.Quantity;
            c.ChildRelationshipType = r.ChildRelationshipType; c.AdditionalChildAllowanceEligible = r.AdditionalChildAllowanceEligible;
            return Task.FromResult<ApiFailure?>(null);
        }, ct);

    public async Task<ServiceResult<bool>> DeleteClaimAsync(Guid employeeId, Guid id, Guid claimId, CancellationToken ct)
        => BooleanResult(await DraftMutation(employeeId, id, (x, _) => {
            var c = x.Claims.SingleOrDefault(c => c.EmployeeTaxClaimId == claimId);
            if (c is null) return Task.FromResult<ApiFailure?>(new("not_found", "Claim was not found under this declaration."));
            db.EmployeeTaxClaims.Remove(c); x.Claims.Remove(c);
            return Task.FromResult<ApiFailure?>(null);
        }, ct));

    public Task<ServiceResult<EmployeeTaxDeclarationDto>> SetOpeningAsync(Guid employeeId, Guid id, EmployeeTaxOpeningBalanceRequest r, CancellationToken ct)
        => DraftMutation(employeeId, id, (x, _) => {
            var state = Canonical(r.State, "Unknown", "ConfirmedZero", "VerifiedAmount");
            var error = OpeningError(state, r.PriorTaxableEmploymentIncome, r.PriorTaxWithheld, r.PriorSocialSecurityContribution, Clean(r.Remarks));
            error ??= OpeningScopeError(state, r.OpeningBalanceScope, r.CompletenessAttested);
            if (error is not null || !r.AsOfDate.HasValue || r.Currency.Trim().ToUpperInvariant() != "THB")
                return Task.FromResult<ApiFailure?>(new("validation", error ?? "Inclusive AsOfDate and THB currency are required."));
            // Cutoff must describe this tax year, or its opening instant (previous Dec 31).
            var first = new DateOnly(x.TaxYear, 1, 1);
            var earliest = x.TaxYear == 1 ? first : first.AddDays(-1);
            if (r.AsOfDate < earliest || r.AsOfDate > new DateOnly(x.TaxYear, 12, 31))
                return Task.FromResult<ApiFailure?>(new("validation", "AsOfDate must fall in the declaration tax year or be December 31 immediately before it."));
            if (r.AsOfDate < first && new[] { r.PriorTaxableEmploymentIncome, r.PriorTaxWithheld, r.PriorSocialSecurityContribution }.Any(n => n > 0))
                return Task.FromResult<ApiFailure?>(new("validation", "A cutoff before this tax year's first day cannot carry positive year-to-date amounts."));
            var o = x.OpeningBalance;
            if (o is null) { o = new() { EmployeeTaxDeclarationId = id }; x.OpeningBalance = o; db.EmployeeTaxOpeningBalances.Add(o); }
            o.State = state!; o.Currency = "THB"; o.AsOfDate = r.AsOfDate.Value; o.Remarks = Clean(r.Remarks);
            o.OpeningBalanceScope = r.OpeningBalanceScope; o.CompletenessAttested = r.CompletenessAttested;
            o.InputContractVersion = state == "Unknown" ? null : "PIT-TH-V1";
            o.PriorTaxableEmploymentIncome = state == "ConfirmedZero" ? 0m : r.PriorTaxableEmploymentIncome;
            o.PriorTaxWithheld = state == "ConfirmedZero" ? 0m : r.PriorTaxWithheld;
            o.PriorSocialSecurityContribution = state == "ConfirmedZero" ? 0m : r.PriorSocialSecurityContribution;
            o.VerifiedAt = state == "Unknown" ? null : DateTime.UtcNow;
            return Task.FromResult<ApiFailure?>(null);
        }, ct);

    public async Task<ServiceResult<bool>> DeleteOpeningAsync(Guid employeeId, Guid id, CancellationToken ct)
        => BooleanResult(await DraftMutation(employeeId, id, (x, _) => {
            if (x.OpeningBalance is null) return Task.FromResult<ApiFailure?>(new("not_found", "Opening balance was not found."));
            db.EmployeeTaxOpeningBalances.Remove(x.OpeningBalance); x.OpeningBalance = null;
            return Task.FromResult<ApiFailure?>(null);
        }, ct));

    public Task<ServiceResult<EmployeeTaxDeclarationDto>> VerifyDeclarationAsync(Guid employeeId, Guid id, CancellationToken ct)
        => DraftMutation(employeeId, id, async (x, token) => {
            if (x.OpeningBalance is null) return new("validation", "Configure an explicit opening-balance state before verification. Unknown is allowed; absence is not silently zero.");
            var o = x.OpeningBalance;
            var error = OpeningError(o.State, o.PriorTaxableEmploymentIncome, o.PriorTaxWithheld, o.PriorSocialSecurityContribution, o.Remarks);
            error ??= OpeningScopeError(o.State, o.OpeningBalanceScope, o.CompletenessAttested);
            if (o.State != "Unknown" && o.InputContractVersion != "PIT-TH-V1") error = "Opening input meaning is unresolved; author a complete current-employer statement.";
            if (error is not null || (o.State == "Unknown" ? o.VerifiedAt.HasValue : !o.VerifiedAt.HasValue))
                return new("validation", error ?? "Opening verification metadata is inconsistent.");
            if (x.Claims.Any(c => ClaimInputError(c.ClaimType, c.Amount, c.Quantity, c.ChildRelationshipType, c.AdditionalChildAllowanceEligible, c.Reference) is not null)
                || x.Claims.Count(c => c.ClaimType == "Spouse") > 1)
                return new("validation", "Claim structure/evidence is incomplete or unsupported for PIT-TH-V1.");
            if (x.Claims.Any(c => c.ChildRelationshipType == "Adopted") && !x.TotalLivingLawfulChildren.HasValue)
                return new("validation", "Adopted claims require TotalLivingLawfulChildren including noneligible living lawful children.");
            if (x.TotalLivingLawfulChildren is < 0 || (x.TotalLivingLawfulChildren.HasValue &&
                x.Claims.Where(c => c.ChildRelationshipType == "Lawful").Sum(c => (long)(c.Quantity ?? 0)) > x.TotalLivingLawfulChildren))
                return new("validation", "Lawful-child counts are inconsistent.");
            var selection = await db.EmployeeTaxDeclarationSelections.SingleOrDefaultAsync(s => s.EmployeeId == employeeId && s.TaxYear == x.TaxYear, token);
            if (x.ReplacesDeclarationId != selection?.CurrentDeclarationId)
                return new("conflict", "Replacement no longer matches the current Verified declaration. Review the revision chain.");
            if (selection is not null && !await db.EmployeeTaxDeclarations.AnyAsync(d => d.EmployeeTaxDeclarationId == selection.CurrentDeclarationId && d.Status == "Verified", token))
                return new("conflict", "Current declaration selection is corrupt; verification was not performed.");
            x.TaxpayerIdentificationNumberSnapshot = await db.EmployeeTaxProfiles.Where(p => p.EmployeeId == employeeId)
                .Select(p => p.TaxpayerIdentificationNumber).SingleOrDefaultAsync(token);
            x.Status = "Verified"; x.VerifiedAt = DateTime.UtcNow;
            if (selection is null) {
                selection = new() { EmployeeId = employeeId, TaxYear = x.TaxYear, CurrentDeclarationId = id };
                db.EmployeeTaxDeclarationSelections.Add(selection);
            }
            else selection.CurrentDeclarationId = id;
            return null;
        }, ct);

    public Task<ServiceResult<EmployeeTaxDeclarationDto>> SetTaxTreatmentAsync(Guid employeeId, Guid id,
        EmployeeTaxTreatmentRequest request, CancellationToken ct)
        => DraftMutation(employeeId, id, (x, _) => {
            if (request.ResidencyStatus is not ("Unknown" or "Resident" or "NonResident")
                || request.EmploymentTaxTreatment is not ("Unknown" or "StandardSection40_1" or "RequiresReview")
                || request.Remarks?.Length > 2000)
                return Task.FromResult<ApiFailure?>(new("validation", "Use an approved ResidencyStatus and EmploymentTaxTreatment; Remarks may contain at most 2000 characters."));
            x.ResidencyStatus = request.ResidencyStatus;
            x.EmploymentTaxTreatment = request.EmploymentTaxTreatment;
            if (request.Remarks is not null) x.Remarks = Clean(request.Remarks);
            return Task.FromResult<ApiFailure?>(null);
        }, ct);

    public async Task<ServiceResult<EmployeeTaxTreatmentResolution>> ResolveTaxTreatmentAsync(Guid employeeId, int taxYear, CancellationToken ct)
    {
        if (taxYear is < 1 or > 9999) return Invalid<EmployeeTaxTreatmentResolution>("TaxYear must be a Gregorian calendar year from 1 to 9999; no Buddhist-year conversion is performed.");
        if (!await EmployeeExists(employeeId, ct)) return Missing<EmployeeTaxTreatmentResolution>();
        var selected = await db.EmployeeTaxDeclarationSelections.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && x.TaxYear == taxYear)
            .Select(x => x.Declaration).SingleOrDefaultAsync(ct);
        return ServiceResult<EmployeeTaxTreatmentResolution>.Success(
            EmployeeTaxTreatmentResolver.Resolve(selected is null ? null : TreatmentDto(selected)));
    }

    private static EmployeeTaxTreatmentDto TreatmentDto(EmployeeTaxDeclaration x)
        => new(x.EmployeeTaxDeclarationId, x.EmployeeId, x.TaxYear, x.RevisionNumber,
            x.ResidencyStatus, x.EmploymentTaxTreatment, x.Status, x.VerifiedAt, x.Remarks);

    private async Task<ServiceResult<EmployeeTaxDeclarationDto>> DraftMutation(Guid employeeId, Guid id,
        Func<EmployeeTaxDeclaration, CancellationToken, Task<ApiFailure?>> change, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await EmploymentIntegrity.LockAsync(db, employeeId, ct) is null) return Missing<EmployeeTaxDeclarationDto>();
        var x = await Declarations(true).SingleOrDefaultAsync(x => x.EmployeeId == employeeId && x.EmployeeTaxDeclarationId == id, ct);
        if (x is null) return Missing<EmployeeTaxDeclarationDto>();
        if (x.Status != "Draft") return Conflict<EmployeeTaxDeclarationDto>("Verified declarations, claims and opening balances are immutable. Create a replacement Draft revision.");
        try {
            var error = await change(x, ct);
            if (error is not null) return ServiceResult<EmployeeTaxDeclarationDto>.Fail(error.Code, error.Message);
            if (db.Entry(x).State != EntityState.Deleted) x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var dto = await DeclarationDto(x, ct);
            await tx.CommitAsync(ct);
            return ServiceResult<EmployeeTaxDeclarationDto>.Success(dto);
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 or 547 }) {
            return Conflict<EmployeeTaxDeclarationDto>("A declaration uniqueness or reference constraint prevents this change.");
        }
    }

    private IQueryable<EmployeeTaxDeclaration> Declarations(bool tracked)
    {
        var q = db.EmployeeTaxDeclarations.Include(x => x.Claims).Include(x => x.OpeningBalance).AsQueryable();
        return tracked ? q : q.AsNoTracking();
    }
    private Task<bool> EmployeeExists(Guid id, CancellationToken ct) => db.Employees.AnyAsync(x => x.EmployeeId == id, ct);
    private static string? OpeningError(string? state, decimal? income, decimal? tax, decimal? sso, string? remarks)
    {
        if (state is not ("Unknown" or "ConfirmedZero" or "VerifiedAmount")) return "Opening State must be Unknown, ConfirmedZero or VerifiedAmount.";
        var error = Numbers(income, tax, sso);
        if (error is not null) return error;
        if (state == "Unknown") return income.HasValue || tax.HasValue || sso.HasValue ? "Unknown opening balances must carry no monetary values." : null;
        if (string.IsNullOrWhiteSpace(remarks)) return "ConfirmedZero and VerifiedAmount require an audit explanation in Remarks.";
        if (state == "ConfirmedZero") return new[] { income, tax, sso }.Any(x => x.HasValue && x != 0m) ? "ConfirmedZero cannot carry nonzero amounts." : null;
        return !income.HasValue || !tax.HasValue || !sso.HasValue ? "VerifiedAmount requires all three approved amounts." : null;
    }
    private static string? Numbers(params decimal?[] values) => values.Any(n => n.HasValue && (n < 0 || n > Maximum || decimal.Round(n.Value, 4) != n))
        ? "Amounts must be nonnegative and fit decimal(19,4); Unknown is not zero." : null;
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? Canonical(string? s, params string[] choices) => choices.FirstOrDefault(x => x.Equals(s?.Trim(), StringComparison.OrdinalIgnoreCase));
    private static ServiceResult<T> Missing<T>() => ServiceResult<T>.Fail("not_found", "Employee, scheme or requested child resource was not found under its parent.");
    private static ServiceResult<T> Invalid<T>(string message) => ServiceResult<T>.Fail("validation", message);
    private static ServiceResult<T> Conflict<T>(string message) => ServiceResult<T>.Fail("conflict", message);
    private static ServiceResult<bool> BooleanResult(ServiceResult<EmployeeTaxDeclarationDto> r) => r.IsSuccess
        ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.Fail(r.Failure!.Code, r.Failure.Message);
    private static EmployeeTaxProfileDto ProfileDto(EmployeeTaxProfile x) => new(x.EmployeeTaxProfileId, x.EmployeeId, x.TaxpayerIdentificationNumber, x.CreatedAt, x.UpdatedAt);
    private static StatutoryEnrollmentDto EnrollmentDto(EmployeeStatutoryEnrollment x) => new(x.EmployeeStatutoryEnrollmentId, x.EmployeeId,
        x.StatutorySchemeId, x.EffectiveFrom, x.EffectiveTo, x.Applicability, x.MembershipNumber, x.Remarks, x.CreatedAt, x.UpdatedAt);
    private static string? ClaimInputError(string? type, decimal? amount, int? quantity, string? relationship, bool? additional, string? reference)
    {
        if (type is not ("Spouse" or "Child" or "Parent")) return "Unsupported claim requires review; only Spouse, Child and Parent are supported.";
        if (amount.HasValue) return "Amount is legacy history only; legal allowance amounts belong to Published PIT policy.";
        if (string.IsNullOrWhiteSpace(reference)) return "Reviewed eligibility evidence Reference is required.";
        if (type == "Spouse" && quantity is not (null or 1)) return "Spouse is one presence claim, not an allowance multiplier.";
        if (type != "Spouse" && quantity is null or <= 0) return "Child and Parent require an explicit positive eligible Quantity.";
        if (type != "Child") return relationship is not null || additional.HasValue ? "Child metadata is valid only on Child claims." : null;
        if (relationship is not ("Lawful" or "Adopted") || !additional.HasValue) return "Child requires Lawful/Adopted relationship and explicit additional-allowance eligibility.";
        return relationship == "Adopted" && additional == true ? "Adopted children cannot claim the additional lawful-child allowance." : null;
    }

    private static string? OpeningScopeError(string? state, string? scope, bool complete)
    {
        if (scope is not null && scope != "CurrentEmployer") return "Unsupported payer history requires review; V1 supports CurrentEmployer only.";
        if (state == "Unknown") return complete ? "Unknown cannot attest complete known opening history." : null;
        return scope != "CurrentEmployer" || !complete ? "Known opening history requires CurrentEmployer scope and explicit completeness attestation." : null;
    }

    private async Task<EmployeeTaxDeclarationDto> DeclarationDto(EmployeeTaxDeclaration x, CancellationToken ct)
    {
        var current = await db.EmployeeTaxDeclarationSelections.AsNoTracking().AnyAsync(s => s.EmployeeId == x.EmployeeId && s.TaxYear == x.TaxYear && s.CurrentDeclarationId == x.EmployeeTaxDeclarationId, ct);
        var o = x.OpeningBalance;
        return new(new(x.EmployeeTaxDeclarationId, x.EmployeeId, x.TaxYear, x.RevisionNumber, x.ReplacesDeclarationId,
            x.Status, current, x.VerifiedAt, x.Remarks, x.CreatedAt, x.UpdatedAt), x.TaxpayerIdentificationNumberSnapshot,
            x.Claims.OrderBy(c => c.ClaimType).ThenBy(c => c.EmployeeTaxClaimId).Select(c => new EmployeeTaxClaimDto(c.EmployeeTaxClaimId,
                c.ClaimType, c.Amount, c.Quantity, c.Reference, c.Remarks, c.CreatedAt, c.UpdatedAt, c.ChildRelationshipType, c.AdditionalChildAllowanceEligible)).ToArray(),
            o is null ? null : new(o.State, o.Currency, o.PriorTaxableEmploymentIncome, o.PriorTaxWithheld,
                o.PriorSocialSecurityContribution, o.AsOfDate, o.Remarks, o.VerifiedAt, o.CreatedAt, o.UpdatedAt,
                o.OpeningBalanceScope, o.CompletenessAttested, o.InputContractVersion),
            TreatmentDto(x), x.TotalLivingLawfulChildren);
    }
}
