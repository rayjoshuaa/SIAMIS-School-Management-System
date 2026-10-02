using SIAMIS.Application.Payroll;

namespace SIAMIS.Infrastructure.Services;

/// <summary>Pure treatment gate; deliberately has no nationality, taxpayer ID, SSO or monetary input.</summary>
public static class EmployeeTaxTreatmentResolver
{
    public static EmployeeTaxTreatmentResolution Resolve(EmployeeTaxTreatmentDto? treatment)
    {
        if (treatment is null)
            return new("Unresolved", "No selected Verified declaration exists for this employee/Gregorian tax year.", null);
        if (treatment.Status != "Verified" || !treatment.VerifiedAt.HasValue)
            return new("Unresolved", "Unverified treatment cannot authorize the future standard PIT method.", treatment);
        if (treatment.ResidencyStatus is not ("Unknown" or "Resident" or "NonResident")
            || treatment.EmploymentTaxTreatment is not ("Unknown" or "StandardSection40_1" or "RequiresReview"))
            return new("InvalidInput", "Stored treatment is outside the approved vocabulary.", treatment);
        if (treatment.EmploymentTaxTreatment == "RequiresReview")
            return new("Blocked", "Verified RequiresReview still blocks automatic PIT.", treatment);
        if (treatment.ResidencyStatus == "Unknown" || treatment.EmploymentTaxTreatment == "Unknown")
            return new("Unresolved", "Unknown residence or employment treatment is not an approval or exemption.", treatment);
        return new("Approved", "Verified StandardSection40_1 treatment is approved for a future supported calculator; no tax was calculated.", treatment);
    }
}
