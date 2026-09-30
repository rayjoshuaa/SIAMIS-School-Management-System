using Microsoft.AspNetCore.Mvc;
using SIAMIS.Application.MasterData;

namespace SIAMIS.Api.Controllers;

/// <summary>Read-only lookups for master data used by Employee Management.</summary>
[ApiController]
[Route("api/master-data")]
[Produces("application/json")]
public sealed class MasterDataController(IMasterDataService masterData) : ControllerBase
{
    /// <summary>Returns departments ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive departments; otherwise only active values are returned.</param>
    [HttpGet("departments")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetDepartments([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetDepartmentsAsync(includeInactive, cancellationToken));

    /// <summary>Returns designations ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive designations; otherwise only active values are returned.</param>
    [HttpGet("designations")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetDesignations([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetDesignationsAsync(includeInactive, cancellationToken));

    /// <summary>Returns employment types ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive employment types; otherwise only active values are returned.</param>
    [HttpGet("employment-types")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetEmploymentTypes([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetEmploymentTypesAsync(includeInactive, cancellationToken));

    /// <summary>Returns employment statuses ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive employment statuses; otherwise only active values are returned.</param>
    [HttpGet("employment-statuses")]
    [ProducesResponseType(typeof(IReadOnlyList<EmploymentStatusDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmploymentStatusDto>>> GetEmploymentStatuses([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetEmploymentStatusesAsync(includeInactive, cancellationToken));

    /// <summary>Returns locations ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive locations; otherwise only active values are returned.</param>
    [HttpGet("locations")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetLocations([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetLocationsAsync(includeInactive, cancellationToken));

    /// <summary>Returns hiring sources ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive hiring sources; otherwise only active values are returned.</param>
    [HttpGet("hiring-sources")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetHiringSources([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetHiringSourcesAsync(includeInactive, cancellationToken));

    /// <summary>Returns genders ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive genders; otherwise only active values are returned.</param>
    [HttpGet("genders")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetGenders([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetGendersAsync(includeInactive, cancellationToken));

    /// <summary>Returns marital statuses ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive marital statuses; otherwise only active values are returned.</param>
    [HttpGet("marital-statuses")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetMaritalStatuses([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetMaritalStatusesAsync(includeInactive, cancellationToken));

    /// <summary>Returns nationalities ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive nationalities; otherwise only active values are returned.</param>
    [HttpGet("nationalities")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetNationalities([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetNationalitiesAsync(includeInactive, cancellationToken));

    /// <summary>Returns address types ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive address types; otherwise only active values are returned.</param>
    [HttpGet("address-types")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetAddressTypes([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetAddressTypesAsync(includeInactive, cancellationToken));

    /// <summary>Returns countries ordered by name.</summary>
    /// <param name="includeInactive">When true, includes inactive countries; otherwise only active values are returned.</param>
    [HttpGet("countries")]
    [ProducesResponseType(typeof(IReadOnlyList<MasterDataItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MasterDataItemDto>>> GetCountries([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
        => Ok(await masterData.GetCountriesAsync(includeInactive, cancellationToken));
}
