using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RequestTracker.Api.Errors;
using RequestTracker.Api.Http;
using RequestTracker.Application;
using RequestTracker.Application.Abstractions;
using RequestTracker.Application.Requests;
using RequestTracker.Domain;

namespace RequestTracker.Api.Controllers;

[ApiController]
[Route("api/requests")]
public sealed class RequestsController(
    RequestQueryService queryService,
    RequestStatusService statusService,
    BulkStatusService bulkService,
    IRequestReadCache cache,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Lists requests with server-side filtering, prefix search, sorting and paging. All filters combine with AND;
    /// <c>status</c> and <c>priority</c> may be repeated (OR within the parameter).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<RequestListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<ActionResult<PagedResult<RequestListItemDto>>> List(
        [FromQuery] RequestListQuery query, CancellationToken cancellationToken) =>
        Ok(await queryService.ListAsync(query, cancellationToken));

    /// <summary>Global counts by status and priority (cached; <c>generatedAtUtc</c> shows how fresh they are).</summary>
    [HttpGet("summary")]
    [ProducesResponseType<RequestSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestSummaryDto>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await cache.GetSummaryAsync(cancellationToken));

    /// <summary>Gets one request. The <c>ETag</c> header carries its version for later updates.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiProblems.ContentType)]
    public async Task<IActionResult> GetById([FromRoute] GetRequestQuery query, CancellationToken cancellationToken)
    {
        var result = await queryService.GetByIdAsync(query.Id, cancellationToken);
        switch (result)
        {
            case GetRequestResult.Found found:
                Response.Headers.ETag = ETagHeader.Format(found.Request.Version);
                return Ok(found.Request);
            default:
                return new ProblemResult(ApiProblems.NotFound(HttpContext, $"Request {query.Id} does not exist."));
        }
    }

    /// <summary>
    /// Changes the status. Requires <c>If-Match</c> (the version from the last read) and <c>X-User-Name</c>. Check order:
    /// input 400, missing If-Match 428, malformed If-Match 400, unknown id 404, stale version 409, transition 400, then 200.
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired, ApiProblems.ContentType)]
    public async Task<IActionResult> ChangeStatus(
        [FromRoute] GetRequestQuery query,
        [FromBody] ChangeStatusBody body,
        CancellationToken cancellationToken)
    {
        // 1. Input validation: id and body were checked by the validation filter; the user header is checked here.
        if (currentUser.UserName is not { } userName)
        {
            var errors = new Dictionary<string, string[]>
            {
                [HeaderCurrentUser.HeaderName] =
                    [$"The {HeaderCurrentUser.HeaderName} header is required (1-{UserNameRules.MaxLength} characters)."],
            };
            return new ProblemResult(ApiProblems.Validation(HttpContext, errors));
        }

        // 2-3. Optimistic concurrency is mandatory: a missing header is 428, a malformed one 400.
        if (!Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatch) || ifMatch.Count == 0)
        {
            return new ProblemResult(ApiProblems.PreconditionRequired(HttpContext));
        }

        if (!ETagHeader.TryParseVersion(ifMatch, out var expectedVersion))
        {
            return new ProblemResult(ApiProblems.InvalidVersion(HttpContext));
        }

        QueryValues.TryParseEnum<RequestStatus>(body.Status, out var target); // validated by the filter

        var result = await statusService.ChangeStatusAsync(query.Id, target, expectedVersion, userName, cancellationToken);
        switch (result)
        {
            case ChangeStatusResult.Updated updated:
                Response.Headers.ETag = ETagHeader.Format(updated.Request.Version);
                return Ok(updated.Request);
            case ChangeStatusResult.Conflict conflict:
                return new ProblemResult(ApiProblems.VersionConflict(
                    HttpContext, query.Id, conflict.CurrentStatus, conflict.CurrentVersion));
            case ChangeStatusResult.InvalidTransition invalid:
                return new ProblemResult(ApiProblems.InvalidStatusTransition(
                    HttpContext, invalid.CurrentStatus, invalid.RequestedStatus));
            default:
                return new ProblemResult(ApiProblems.NotFound(HttpContext, $"Request {query.Id} does not exist."));
        }
    }

    /// <summary>
    /// Moves up to 100 requests to one target status (Partial Success): the call answers 200 with one outcome per item
    /// (updated, notFound, conflict, invalidTransition) and a summary; failed items do not affect the others. Requires
    /// <c>X-User-Name</c>. Malformed input (0 or more than 100 items, duplicate ids, bad versions or status) is 400 and
    /// nothing is processed. If the client disconnects mid-call, items already saved stay saved.
    /// </summary>
    [HttpPost("bulk-status")]
    [ProducesResponseType<BulkStatusResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    public async Task<IActionResult> BulkChangeStatus([FromBody] BulkStatusBody body, CancellationToken cancellationToken)
    {
        if (currentUser.UserName is not { } userName)
        {
            var errors = new Dictionary<string, string[]>
            {
                [HeaderCurrentUser.HeaderName] =
                    [$"The {HeaderCurrentUser.HeaderName} header is required (1-{UserNameRules.MaxLength} characters)."],
            };
            return new ProblemResult(ApiProblems.Validation(HttpContext, errors));
        }

        // Validated by the filter, so these parses cannot fail.
        QueryValues.TryParseEnum<RequestStatus>(body.TargetStatus, out var target);
        var items = body.Items!
            .Select(i => (i.Id, VersionToken.TryDecode(i.Version, out var version) ? version : []))
            .ToList();

        return Ok(await bulkService.ChangeStatusAsync(target, items, userName, cancellationToken));
    }

    /// <summary>The status changes of one request, newest first.</summary>
    [HttpGet("{id}/history")]
    [ProducesResponseType<IReadOnlyList<StatusHistoryItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ApiProblems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ApiProblems.ContentType)]
    public async Task<IActionResult> GetHistory([FromRoute] GetRequestQuery query, CancellationToken cancellationToken)
    {
        var result = await statusService.GetHistoryAsync(query.Id, cancellationToken);
        return result switch
        {
            HistoryResult.Found found => Ok(found.Items),
            _ => new ProblemResult(ApiProblems.NotFound(HttpContext, $"Request {query.Id} does not exist.")),
        };
    }
}
