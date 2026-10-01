using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.Payroll;

namespace SIAMIS.Api.Controllers;

/// <summary>Minimum tax inputs, revisioned declarations, manual claims and aggregate opening balances. No PIT/YTD calculation.</summary>
/// <remarks>Verification is internal review, not certification of legal eligibility or completeness for D6. Sensitive IDs require future production RBAC.</remarks>
[Route("api/employees/{employeeId:guid}")]
public sealed class EmployeeTaxController(IEmployeeStatutoryService service) : StatutoryConfigurationController
{
    /// <summary>Reads the focused taxpayer profile. Returns null when not configured; does not fabricate a profile.</summary>
    [HttpGet("tax-profile")]
    [ProducesResponseType(typeof(EmployeeTaxProfileDto), 200)]
    public async Task<ActionResult<EmployeeTaxProfileDto?>> Profile(Guid employeeId, CancellationToken ct)
    {
        var result = await service.GetProfileAsync(employeeId, ct);
        return result.IsSuccess ? new JsonResult(result.Value) : Failure<EmployeeTaxProfileDto?>(result.Failure!);
    }

    /// <summary>Creates/replaces only the employee's taxpayer identifier. Verified declarations retain their verification-time snapshot.</summary>
    [HttpPut("tax-profile")]
    [ProducesResponseType(typeof(EmployeeTaxProfileDto), 200)]
    public async Task<ActionResult<EmployeeTaxProfileDto>> SetProfile(Guid employeeId, EmployeeTaxProfileRequest request, CancellationToken ct)
        => Respond(await service.SetProfileAsync(employeeId, request, ct));

    /// <summary>Lists declaration revisions, optionally filtered by taxYear, without taxpayer identifier snapshots.</summary>
    [HttpGet("tax-declarations")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeTaxDeclarationSummaryDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<EmployeeTaxDeclarationSummaryDto>>> List(Guid employeeId, [FromQuery] int? taxYear, CancellationToken ct)
        => Respond(await service.ListDeclarationsAsync(employeeId, taxYear, ct));

    /// <summary>Reads a complete owned declaration revision, including claims, opening state and sensitive verification-time identifier.</summary>
    [HttpGet("tax-declarations/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeTaxDeclarationDto), 200)]
    public async Task<ActionResult<EmployeeTaxDeclarationDto>> Get(Guid employeeId, Guid id, CancellationToken ct)
        => Respond(await service.GetDeclarationAsync(employeeId, id, ct));

    /// <summary>Creates the next Draft revision, automatically referencing the current Verified revision. No claims/balances are copied or inferred.</summary>
    [HttpPost("tax-declarations")]
    [ProducesResponseType(typeof(EmployeeTaxDeclarationDto), 201)]
    public async Task<ActionResult<EmployeeTaxDeclarationDto>> Create(Guid employeeId, EmployeeTaxDeclarationCreateRequest request, CancellationToken ct)
    {
        var result = await service.CreateDeclarationAsync(employeeId, request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { employeeId, id = result.Value!.Declaration.EmployeeTaxDeclarationId }, result.Value)
            : Failure<EmployeeTaxDeclarationDto>(result.Failure!);
    }

    /// <summary>Updates Draft remarks only; year, revision, replacement identity, status and audit timestamps are server-controlled.</summary>
    [HttpPut("tax-declarations/{id:guid}")]
    [ProducesResponseType(typeof(EmployeeTaxDeclarationDto), 200)]
    public async Task<ActionResult<EmployeeTaxDeclarationDto>> Update(Guid employeeId, Guid id, EmployeeTaxDeclarationUpdateRequest request, CancellationToken ct)
        => Respond(await service.UpdateDeclarationAsync(employeeId, id, request, ct));

    /// <summary>Deletes only a safe Draft and its owned inputs. Verified revisions cannot be deleted.</summary>
    [HttpDelete("tax-declarations/{id:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(Guid employeeId, Guid id, CancellationToken ct)
        => DeleteResponse(await service.DeleteDeclarationAsync(employeeId, id, ct));

    /// <summary>Reads declared claims without calculating allowances.</summary>
    [HttpGet("tax-declarations/{id:guid}/claims")]
    [ProducesResponseType(typeof(IReadOnlyList<EmployeeTaxClaimDto>), 200)]
    public async Task<ActionResult<IReadOnlyList<EmployeeTaxClaimDto>>> Claims(Guid employeeId, Guid id, CancellationToken ct)
    {
        var result = await service.GetDeclarationAsync(employeeId, id, ct);
        return result.IsSuccess ? Ok(result.Value!.Claims) : Failure<IReadOnlyList<EmployeeTaxClaimDto>>(result.Failure!);
    }

    /// <summary>Adds a Spouse/Child/Parent claim to Draft; Amount/Quantity are declarations, not computed entitlements. Returns the updated declaration.</summary>
    [HttpPost("tax-declarations/{id:guid}/claims")]
    [ProducesResponseType(typeof(EmployeeTaxDeclarationDto), 201)]
    public async Task<ActionResult<EmployeeTaxDeclarationDto>> AddClaim(Guid employeeId, Guid id, EmployeeTaxClaimRequest request, CancellationToken ct)
    {
        var result = await service.SetClaimAsync(employeeId, id, null, request, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(Claims), new { employeeId, id }, result.Value) : Failure<EmployeeTaxDeclarationDto>(result.Failure!);
    }

    /// <summary>Replaces an owned claim in Draft only; Verified claims are immutable.</summary>
    [HttpPut("tax-declarations/{id:guid}/claims/{claimId:guid}")]
    [ProducesResponseType(typeof(EmployeeTaxDeclarationDto), 200)]
    public async Task<ActionResult<EmployeeTaxDeclarationDto>> UpdateClaim(Guid employeeId, Guid id, Guid claimId, EmployeeTaxClaimRequest request, CancellationToken ct)
        => Respond(await service.SetClaimAsync(employeeId, id, claimId, request, ct));

    /// <summary>Deletes an owned Draft claim; never removes Verified declaration history.</summary>
    [HttpDelete("tax-declarations/{id:guid}/claims/{claimId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteClaim(Guid employeeId, Guid id, Guid claimId, CancellationToken ct)
        => DeleteResponse(await service.DeleteClaimAsync(employeeId, id, claimId, ct));

    /// <summary>Reads the aggregate pre-SIAMIS opening balance. Null means no explicit state has been authored.</summary>
    [HttpGet("tax-declarations/{id:guid}/opening-balance")]
    [ProducesResponseType(typeof(EmployeeTaxOpeningBalanceDto), 200)]
    public async Task<ActionResult<EmployeeTaxOpeningBalanceDto?>> Opening(Guid employeeId, Guid id, CancellationToken ct)
    {
        var result = await service.GetDeclarationAsync(employeeId, id, ct);
        return result.IsSuccess ? new JsonResult(result.Value!.OpeningBalance) : Failure<EmployeeTaxOpeningBalanceDto?>(result.Failure!);
    }

    /// <summary>Replaces Draft THB opening inputs through inclusive AsOfDate. Unknown keeps null amounts; ConfirmedZero and VerifiedAmount require Remarks and server verification timestamp.</summary>
    /// <remarks>PriorTaxableEmploymentIncome is the approved pre-SIAMIS aggregate, not GrossPay or automatically generic TaxableEarnings. D6 interprets it and prevents cutoff double counting.</remarks>
    [HttpPut("tax-declarations/{id:guid}/opening-balance")]
    [ProducesResponseType(typeof(EmployeeTaxDeclarationDto), 200)]
    public async Task<ActionResult<EmployeeTaxDeclarationDto>> SetOpening(Guid employeeId, Guid id, EmployeeTaxOpeningBalanceRequest request, CancellationToken ct)
        => Respond(await service.SetOpeningAsync(employeeId, id, request, ct));

    /// <summary>Deletes an opening balance from Draft only; subsequent verification requires another explicit opening state.</summary>
    [HttpDelete("tax-declarations/{id:guid}/opening-balance")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteOpening(Guid employeeId, Guid id, CancellationToken ct)
        => DeleteResponse(await service.DeleteOpeningAsync(employeeId, id, ct));

    /// <summary>Internally verifies supplied inputs and atomically selects this immutable revision for new calculations. Explicit Unknown opening state is allowed; D6 may still reject insufficient inputs.</summary>
    [HttpPost("tax-declarations/{id:guid}/verify")]
    [ProducesResponseType(typeof(EmployeeTaxDeclarationDto), 200)]
    public async Task<ActionResult<EmployeeTaxDeclarationDto>> Verify(Guid employeeId, Guid id, CancellationToken ct)
        => Respond(await service.VerifyDeclarationAsync(employeeId, id, ct));

    private IActionResult DeleteResponse(SIAMIS.Application.Employees.ServiceResult<bool> result)
        => result.IsSuccess ? NoContent() : Failure<bool>(result.Failure!).Result!;
}
