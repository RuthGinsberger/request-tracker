using Microsoft.AspNetCore.Mvc;
using RequestTracker.Application.Abstractions;
using RequestTracker.Application.Requests;

namespace RequestTracker.Api.Controllers;

[ApiController]
[Route("api/lookups")]
public sealed class LookupsController(IRequestReadCache cache) : ControllerBase
{
    /// <summary>Distinct organization names and assignees for the filter and user pickers (cached, TTL only).</summary>
    [HttpGet]
    [ProducesResponseType<LookupsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LookupsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await cache.GetLookupsAsync(cancellationToken));
}
