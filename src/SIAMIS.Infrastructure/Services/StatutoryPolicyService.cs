using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIAMIS.Application.Employees;
using SIAMIS.Application.Payroll;
using SIAMIS.Domain.Entities.Payroll;
using SIAMIS.Infrastructure.Data;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Configuration only. Publication validates structure, never legal truth or runtime calculator compatibility.</summary>
public sealed class StatutoryPolicyService(SIAMISDbContext db) : IStatutoryPolicyService, IStatutoryPolicyResolver
{
    private const decimal Maximum = 999999999999999.9999m;
    public async Task<IReadOnlyList<StatutorySchemeDto>> GetSchemesAsync(bool includeInactive, CancellationToken ct)
        => await db.StatutorySchemes.AsNoTracking().Where(x => includeInactive || x.IsActive)
            .OrderBy(x => x.Code).Select(x => new StatutorySchemeDto(x.StatutorySchemeId, x.Code, x.Name,
                x.Jurisdiction, x.SchemeType, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToListAsync(ct);
    public async Task<StatutorySchemeDto?> GetSchemeAsync(Guid id, CancellationToken ct)
    {
        var x = await db.StatutorySchemes.AsNoTracking().SingleOrDefaultAsync(x => x.StatutorySchemeId == id, ct);
        return x is null ? null : SchemeDto(x);
    }
    public async Task<ServiceResult<StatutorySchemeDto>> CreateSchemeAsync(StatutorySchemeRequest r, CancellationToken ct)
    {
        var type = CanonicalType(r.SchemeType);
        if (type is null || r.Jurisdiction.Trim().ToUpperInvariant() != "TH" || string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name))
            return Fail<StatutorySchemeDto>("validation", "Code and Name are required; Jurisdiction must be TH; SchemeType must be SocialSecurity or PersonalIncomeTax.");
        var x = new StatutoryScheme { Code = r.Code.Trim().ToUpperInvariant(), Name = r.Name.Trim(),
            Jurisdiction = "TH", SchemeType = type, IsActive = r.IsActive ?? true };
        db.StatutorySchemes.Add(x);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (Unique(e)) { return Fail<StatutorySchemeDto>("conflict", "Scheme Code already exists."); }
        return ServiceResult<StatutorySchemeDto>.Success(SchemeDto(x));
    }
    public async Task<ServiceResult<StatutorySchemeDto>> UpdateSchemeAsync(Guid id, StatutorySchemeRequest r, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var x = await LockScheme(id, ct);
        if (x is null) return Fail<StatutorySchemeDto>("not_found", "Scheme was not found.");
        if (x.Code != r.Code.Trim().ToUpperInvariant() || x.SchemeType != CanonicalType(r.SchemeType)
            || r.Jurisdiction.Trim().ToUpperInvariant() != "TH")
            return Fail<StatutorySchemeDto>("conflict", "Scheme Code, SchemeType and Jurisdiction are stable identities and cannot change.");
        if (string.IsNullOrWhiteSpace(r.Name)) return Fail<StatutorySchemeDto>("validation", "Name is required.");
        if (x.Name != r.Name.Trim() && await db.StatutoryPolicyVersions.AnyAsync(p => p.StatutorySchemeId == id && p.Status == "Published", ct))
            return Fail<StatutorySchemeDto>("conflict", "Scheme identity metadata with Published policies is immutable.");
        x.Name = r.Name.Trim(); x.IsActive = r.IsActive ?? x.IsActive;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<StatutorySchemeDto>.Success(SchemeDto(x));
    }
    public async Task<ServiceResult<bool>> DeleteSchemeAsync(Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var x = await LockScheme(id, ct);
        if (x is null) return Fail<bool>("not_found", "Scheme was not found.");
        if (await db.StatutoryPolicyVersions.AnyAsync(p => p.StatutorySchemeId == id, ct))
            return Fail<bool>("conflict", "Scheme cannot be deleted while policy versions exist.");
        db.StatutorySchemes.Remove(x);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (Referenced(e)) { return Fail<bool>("conflict", "Scheme is referenced and cannot be deleted."); }
        return ServiceResult<bool>.Success(true);
    }
    public async Task<IReadOnlyList<StatutoryPolicyDto>> GetPoliciesAsync(Guid? schemeId, CancellationToken ct)
        => (await PolicyQuery().Where(x => !schemeId.HasValue || x.StatutorySchemeId == schemeId)
            .OrderBy(x => x.StatutorySchemeId).ThenBy(x => x.EffectiveFrom).ThenBy(x => x.Version).ToListAsync(ct)).Select(PolicyDto).ToArray();
    public async Task<StatutoryPolicyDto?> GetPolicyAsync(Guid id, CancellationToken ct)
    {
        var x = await PolicyQuery().SingleOrDefaultAsync(x => x.StatutoryPolicyVersionId == id, ct);
        return x is null ? null : PolicyDto(x);
    }
    public async Task<ServiceResult<StatutoryPolicyDto>> CreatePolicyAsync(StatutoryPolicyCreateRequest r, CancellationToken ct)
    {
        var error = ValidateHeader(r);
        if (error is not null) return Fail<StatutoryPolicyDto>("validation", error);
        if (!r.StatutorySchemeId.HasValue || r.StatutorySchemeId == Guid.Empty) return Fail<StatutoryPolicyDto>("validation", "StatutorySchemeId is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var scheme = await LockScheme(r.StatutorySchemeId.Value, ct);
        if (scheme is null) return Fail<StatutoryPolicyDto>("not_found", "Scheme was not found.");
        if (!scheme.IsActive) return Fail<StatutoryPolicyDto>("conflict", "Inactive schemes cannot receive new Draft policies.");
        var x = new StatutoryPolicyVersion { StatutorySchemeId = scheme.StatutorySchemeId, SchemeType = scheme.SchemeType };
        ApplyHeader(x, r); db.StatutoryPolicyVersions.Add(x);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (Unique(e)) { return Fail<StatutoryPolicyDto>("conflict", "Scheme/version already exists."); }
        return ServiceResult<StatutoryPolicyDto>.Success(PolicyDto(x));
    }
    public Task<ServiceResult<StatutoryPolicyDto>> UpdatePolicyAsync(Guid id, StatutoryPolicyRequest r, CancellationToken ct)
        => MutateDraft(id, (x, _) => {
            var error = ValidateHeader(r);
            if (error is null) ApplyHeader(x, r);
            return Task.FromResult(error is null ? null : new ApiFailure("validation", error));
        }, ct);
    public Task<ServiceResult<StatutoryPolicyDto>> SetSocialSecurityAsync(Guid id, SocialSecurityPolicyRequest r, CancellationToken ct)
        => MutateDraft(id, (x, _) => {
            if (x.SchemeType != "SocialSecurity") return Task.FromResult<ApiFailure?>(new("validation", "Social Security configuration requires a SocialSecurity policy."));
            var error = Numbers(r.EmployeeContributionRate, r.EmployerContributionRate, r.MinimumContributionBase, r.MaximumContributionBase);
            if (r.MinimumContributionBase > r.MaximumContributionBase) error = "MaximumContributionBase must be at least MinimumContributionBase.";
            if (error is not null) return Task.FromResult<ApiFailure?>(new("validation", error));
            var c = x.SocialSecurity ??= new() { StatutoryPolicyVersionId = id };
            c.EmployeeContributionRate = r.EmployeeContributionRate; c.EmployerContributionRate = r.EmployerContributionRate;
            c.MinimumContributionBase = r.MinimumContributionBase; c.MaximumContributionBase = r.MaximumContributionBase;
            c.InsuredPersonClassification = Text(r.InsuredPersonClassification);
            return Task.FromResult<ApiFailure?>(null);
        }, ct);
    public Task<ServiceResult<StatutoryPolicyDto>> SetPitAsync(Guid id, PitPolicyRequest r, CancellationToken ct)
        => MutateDraft(id, async (x, token) => {
            if (x.SchemeType != "PersonalIncomeTax") return new("validation", "PIT configuration requires a PersonalIncomeTax policy.");
            var error = Numbers(r.EmploymentExpenseDeductionRate, r.EmploymentExpenseDeductionCap, r.PersonalAllowanceAmount)
                ?? ValidateBrackets(r.Brackets, false);
            if (r.TaxYear is < 1 or > 9999) error = "TaxYear must fit the supported date-year range.";
            if (error is not null) return new("validation", error);
            var c = x.PersonalIncomeTax;
            if (c is null) { c = new() { StatutoryPolicyVersionId = id }; x.PersonalIncomeTax = c; }
            else {
                db.PitTaxBrackets.RemoveRange(c.Brackets);
                await db.SaveChangesAsync(token); // Remove old unique keys before inserting replacement brackets.
                c.Brackets.Clear();
            }
            c.TaxYear = r.TaxYear; c.EmploymentExpenseDeductionRate = r.EmploymentExpenseDeductionRate;
            c.EmploymentExpenseDeductionCap = r.EmploymentExpenseDeductionCap; c.PersonalAllowanceAmount = r.PersonalAllowanceAmount;
            c.WithholdingMethodIdentifier = Text(r.WithholdingMethodIdentifier);
            foreach (var b in r.Brackets) {
                var bracket = new PitTaxBracket { StatutoryPolicyVersionId = id,
                    SortOrder = b.SortOrder!.Value, LowerBoundInclusive = b.LowerBoundInclusive!.Value,
                    UpperBoundExclusive = b.UpperBoundExclusive, Rate = b.Rate!.Value };
                c.Brackets.Add(bracket);
                // Explicitly insert client-generated GUIDs instead of inferring existing rows.
                db.PitTaxBrackets.Add(bracket);
            }
            return null;
        }, ct);
    public Task<ServiceResult<StatutoryPolicyDto>> PublishAsync(Guid id, CancellationToken ct)
        => MutateDraft(id, async (x, token) => {
            if (!x.StatutoryScheme.IsActive) return new("conflict", "Inactive schemes cannot publish policies.");
            var error = PublicationError(x);
            if (error is not null) return new("validation", error);
            if (await db.StatutoryPolicyVersions.AnyAsync(p => p.StatutorySchemeId == x.StatutorySchemeId && p.Status == "Published"
                && p.StatutoryPolicyVersionId != id && (!p.EffectiveTo.HasValue || p.EffectiveTo >= x.EffectiveFrom)
                && (!x.EffectiveTo.HasValue || p.EffectiveFrom <= x.EffectiveTo), token))
                return new("conflict", "Published effective coverage overlaps. An open-ended predecessor is never automatically shortened; a controlled successor workflow requires separate approval.");
            x.Status = "Published"; x.PublishedAt = DateTime.UtcNow;
            return null;
        }, ct);
    public async Task<ServiceResult<bool>> DeletePolicyAsync(Guid id, CancellationToken ct)
    {
        var result = await MutateDraft(id, async (x, token) => {
            if (x.PersonalIncomeTax is not null) {
                db.PitTaxBrackets.RemoveRange(x.PersonalIncomeTax.Brackets);
                await db.SaveChangesAsync(token);
                db.PitPolicyConfigurations.Remove(x.PersonalIncomeTax);
            }
            if (x.SocialSecurity is not null) db.SocialSecurityPolicyConfigurations.Remove(x.SocialSecurity);
            await db.SaveChangesAsync(token);
            db.StatutoryPolicyVersions.Remove(x);
            return null;
        }, ct);
        return result.IsSuccess ? ServiceResult<bool>.Success(true) : Fail<bool>(result.Failure!.Code, result.Failure.Message);
    }
    public async Task<StatutoryPolicyResolution> ResolveAsync(Guid schemeId, DateOnly governingDate, CancellationToken ct)
    {
        // One query includes the scheme and filtered Published versions for a consistent read.
        var scheme = await db.StatutorySchemes.AsNoTracking().Where(x => x.StatutorySchemeId == schemeId)
            .Select(x => new { Scheme = x, Policies = db.StatutoryPolicyVersions
                .Where(p => p.StatutorySchemeId == x.StatutorySchemeId && p.Status == "Published" && p.EffectiveFrom <= governingDate
                    && (!p.EffectiveTo.HasValue || p.EffectiveTo >= governingDate)).Select(p => p.StatutoryPolicyVersionId).ToList() }).SingleOrDefaultAsync(ct);
        if (scheme is null) return new("SchemeNotFound", "Scheme was not found.", null, null);
        var dto = SchemeDto(scheme.Scheme);
        if (scheme.Policies.Count == 0) return new("NoApplicablePolicy", "No Published policy covers the supplied governing date. No fallback is used.", dto, null);
        if (scheme.Policies.Count > 1) return new("Ambiguous", "Multiple Published policies cover the supplied governing date; none was selected.", dto, null);
        var policy = await GetPolicyAsync(scheme.Policies[0], ct);
        // Published rows cannot be edited/deleted through the service. Direct database tampering is unsupported.
        return new("Resolved", "Exactly one Published policy covers the governing date. Inactive schemes remain resolvable for historical inspection.", dto, policy);
    }
    private async Task<ServiceResult<StatutoryPolicyDto>> MutateDraft(Guid id, Func<StatutoryPolicyVersion, CancellationToken, Task<ApiFailure?>> action, CancellationToken ct)
    {
        // Discover the parent before acquiring locks, matching existing period/employee lock conventions.
        var schemeId = await db.StatutoryPolicyVersions.AsNoTracking().Where(x => x.StatutoryPolicyVersionId == id).Select(x => (Guid?)x.StatutorySchemeId).SingleOrDefaultAsync(ct);
        if (!schemeId.HasValue) return Fail<StatutoryPolicyDto>("not_found", "Policy was not found.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var scheme = await LockScheme(schemeId.Value, ct);
        if (scheme is null) return Fail<StatutoryPolicyDto>("not_found", "Scheme was not found.");
        var x = await db.StatutoryPolicyVersions.Include(x => x.SocialSecurity).Include(x => x.PersonalIncomeTax!).ThenInclude(x => x.Brackets)
            .SingleOrDefaultAsync(x => x.StatutoryPolicyVersionId == id, ct);
        if (x is null) return Fail<StatutoryPolicyDto>("not_found", "Policy was not found.");
        if (x.Status != "Draft") return Fail<StatutoryPolicyDto>("conflict", "Published policies, including metadata and typed parameters, are immutable and cannot be deleted or returned to Draft.");
        x.StatutoryScheme = scheme;
        try {
            var error = await action(x, ct);
            if (error is not null) return Fail<StatutoryPolicyDto>(error.Code, error.Message);
            if (db.Entry(x).State != EntityState.Deleted) x.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ServiceResult<StatutoryPolicyDto>.Success(PolicyDto(x));
        }
        catch (DbUpdateException e) when (Unique(e)) { return Fail<StatutoryPolicyDto>("conflict", "A unique scheme/version or bracket key already exists."); }
        catch (DbUpdateException e) when (Referenced(e)) { return Fail<StatutoryPolicyDto>("conflict", "A referenced policy/configuration cannot be deleted or changed."); }
    }
    private Task<StatutoryScheme?> LockScheme(Guid id, CancellationToken ct)
        => db.StatutorySchemes.FromSqlInterpolated($"SELECT * FROM [StatutorySchemes] WITH (UPDLOCK) WHERE [StatutorySchemeId] = {id}").SingleOrDefaultAsync(ct);
    private IQueryable<StatutoryPolicyVersion> PolicyQuery() => db.StatutoryPolicyVersions.AsNoTracking()
        .Include(x => x.SocialSecurity).Include(x => x.PersonalIncomeTax!).ThenInclude(x => x.Brackets);
    private static string? ValidateHeader(StatutoryPolicyRequest r)
        => string.IsNullOrWhiteSpace(r.Version) ? "Version is required."
        : !r.EffectiveFrom.HasValue ? "EffectiveFrom is required."
        : r.EffectiveTo < r.EffectiveFrom ? "EffectiveTo is inclusive and cannot precede EffectiveFrom."
        : r.Currency.Trim().ToUpperInvariant() != "THB" ? "Thailand statutory policies support THB only." : null;
    private static void ApplyHeader(StatutoryPolicyVersion x, StatutoryPolicyRequest r)
    {
        x.Version = r.Version.Trim(); x.EffectiveFrom = r.EffectiveFrom!.Value; x.EffectiveTo = r.EffectiveTo;
        x.Currency = "THB"; x.OfficialReference = Text(r.OfficialReference);
        x.CalculationMethodVersion = Text(r.CalculationMethodVersion)?.ToUpperInvariant();
    }
    private static string? PublicationError(StatutoryPolicyVersion x)
    {
        if (string.IsNullOrWhiteSpace(x.OfficialReference)) return "OfficialReference is required for publication; legal accuracy must be reviewed externally.";
        var expected = x.SchemeType == "SocialSecurity" ? "SSO-TH-V1" : "PIT-TH-V1";
        if (x.CalculationMethodVersion != expected) return $"CalculationMethodVersion must identify the supported D4A configuration contract {expected}. This does not claim runtime calculator compatibility.";
        if (x.SchemeType == "SocialSecurity") {
            var c = x.SocialSecurity;
            if (c is null || !c.EmployeeContributionRate.HasValue || !c.EmployerContributionRate.HasValue
                || !c.MinimumContributionBase.HasValue || !c.MaximumContributionBase.HasValue || string.IsNullOrWhiteSpace(c.InsuredPersonClassification))
                return "Social Security configuration must include employee/employer rates, minimum/maximum base and insured-person classification.";
            return Numbers(c.EmployeeContributionRate, c.EmployerContributionRate, c.MinimumContributionBase, c.MaximumContributionBase)
                ?? (c.MinimumContributionBase > c.MaximumContributionBase ? "Invalid contribution base range." : null);
        }
        var p = x.PersonalIncomeTax;
        if (p is null || !p.TaxYear.HasValue || !p.EmploymentExpenseDeductionRate.HasValue || !p.EmploymentExpenseDeductionCap.HasValue
            || !p.PersonalAllowanceAmount.HasValue || string.IsNullOrWhiteSpace(p.WithholdingMethodIdentifier))
            return "PIT configuration must include TaxYear, expense rate/cap, personal allowance and withholding-method metadata.";
        if (p.TaxYear is < 1 or > 9999) return "TaxYear is outside the supported date-year range.";
        return Numbers(p.EmploymentExpenseDeductionRate, p.EmploymentExpenseDeductionCap, p.PersonalAllowanceAmount)
            ?? ValidateBrackets(p.Brackets.OrderBy(b => b.SortOrder).Select(b => new PitTaxBracketRequest {
                SortOrder = b.SortOrder, LowerBoundInclusive = b.LowerBoundInclusive, UpperBoundExclusive = b.UpperBoundExclusive, Rate = b.Rate }).ToArray(), true);
    }
    private static string? ValidateBrackets(IReadOnlyList<PitTaxBracketRequest>? brackets, bool complete)
    {
        if (brackets is null) return "Brackets cannot be null.";
        for (var i = 0; i < brackets.Count; i++) {
            var b = brackets[i];
            if (b is null) return "Bracket entries cannot be null.";
            if (b.SortOrder != i + 1) return "Brackets must be supplied in consecutive SortOrder beginning at 1.";
            if (!b.LowerBoundInclusive.HasValue || !b.Rate.HasValue) return "Bracket lower bound and rate are required.";
            var error = Numbers(b.LowerBoundInclusive, b.UpperBoundExclusive, b.Rate);
            if (error is not null) return error;
            if (b.UpperBoundExclusive <= b.LowerBoundInclusive) return "UpperBoundExclusive must exceed LowerBoundInclusive.";
            if (!b.UpperBoundExclusive.HasValue && i != brackets.Count - 1) return "Only the final bracket may be unbounded.";
            if (i > 0) {
                var previous = brackets[i - 1];
                if (b.LowerBoundInclusive < previous.UpperBoundExclusive) return "Tax brackets overlap or are not in ascending boundary order.";
                if (complete && b.LowerBoundInclusive != previous.UpperBoundExclusive) return "Published brackets must be contiguous, without gaps.";
            }
        }
        if (complete && (brackets.Count == 0 || brackets[0].LowerBoundInclusive != 0 || brackets[^1].UpperBoundExclusive.HasValue))
            return "Published brackets must cover zero to infinity with exactly one final unbounded bracket.";
        return null;
    }
    private static string? Numbers(params decimal?[] numbers)
        => numbers.Any(x => x.HasValue && (x < 0 || x > Maximum || decimal.Round(x.Value, 4) != x))
            ? "Numeric policy parameters must be nonnegative and fit decimal(19,4); no legal values are inferred." : null;
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? CanonicalType(string? value) => new[] { "SocialSecurity", "PersonalIncomeTax" }.FirstOrDefault(x => x.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
    private static bool Unique(DbUpdateException e) => e.InnerException is SqlException { Number: 2601 or 2627 };
    private static bool Referenced(DbUpdateException e) => e.InnerException is SqlException { Number: 547 };
    private static ServiceResult<T> Fail<T>(string code, string message) => ServiceResult<T>.Fail(code, message);
    private static StatutorySchemeDto SchemeDto(StatutoryScheme x) => new(x.StatutorySchemeId, x.Code, x.Name, x.Jurisdiction, x.SchemeType, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static StatutoryPolicyDto PolicyDto(StatutoryPolicyVersion x) => new(x.StatutoryPolicyVersionId, x.StatutorySchemeId,
        x.SchemeType, x.Version, x.EffectiveFrom, x.EffectiveTo, x.Currency, x.Status, x.OfficialReference,
        x.CalculationMethodVersion, x.CreatedAt, x.UpdatedAt, x.PublishedAt,
        x.SocialSecurity is null ? null : new(x.SocialSecurity.EmployeeContributionRate, x.SocialSecurity.EmployerContributionRate,
            x.SocialSecurity.MinimumContributionBase, x.SocialSecurity.MaximumContributionBase, x.SocialSecurity.InsuredPersonClassification),
        x.PersonalIncomeTax is null ? null : new(x.PersonalIncomeTax.TaxYear, x.PersonalIncomeTax.EmploymentExpenseDeductionRate,
            x.PersonalIncomeTax.EmploymentExpenseDeductionCap, x.PersonalIncomeTax.PersonalAllowanceAmount,
            x.PersonalIncomeTax.WithholdingMethodIdentifier, x.PersonalIncomeTax.Brackets.OrderBy(b => b.SortOrder)
                .Select(b => new PitTaxBracketDto(b.SortOrder, b.LowerBoundInclusive, b.UpperBoundExclusive, b.Rate)).ToArray()));
}
