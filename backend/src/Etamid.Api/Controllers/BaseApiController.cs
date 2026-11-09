using Etamid.Application.Common.Interfaces;
using Etamid.Application.Common.Models;
using Etamid.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    private ISender? _sender;
    private IMessageLocalizer? _localizer;

    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();
    private IMessageLocalizer Localizer => _localizer ??= HttpContext.RequestServices.GetRequiredService<IMessageLocalizer>();

    protected IActionResult Respond<T>(Result<T> result, string successKey = "SUCCESS", int status = StatusCodes.Status200OK)
        => result.IsSuccess ? Success(result.Value, successKey, status) : Failure(result.Error);

    protected IActionResult Respond(Result result, string successKey = "SUCCESS", int status = StatusCodes.Status200OK)
        => result.IsSuccess ? Success(null, successKey, status) : Failure(result.Error);

    protected IActionResult RespondPaged<T>(Result<PagedResult<T>> result, string successKey = "SUCCESS")
    {
        if (result.IsFailure) return Failure(result.Error);
        var page = result.Value;
        return StatusCode(StatusCodes.Status200OK, ApiResponse.Ok(page.Items, Localizer.Get(successKey), page.ToMeta()));
    }

    private IActionResult Success(object? data, string key, int status)
        => StatusCode(status, ApiResponse.Ok(data, Localizer.Get(key)));

    /// <summary>
    /// A 200 error envelope for the rare endpoints that fail before they
    /// reach a handler (e.g. the dev-only fake-payment endpoint): same shape
    /// as <see cref="Failure"/>, HTTP 200 with the code in the body.
    /// </summary>
    protected IActionResult ErrorEnvelope(string code, string? message = null)
    {
        var text = message ?? Localizer.Get(code);
        return StatusCode(StatusCodes.Status200OK,
            ApiResponse.Fail(text, [new ApiError { Code = code, Message = text }]));
    }

    /// <summary>
    /// Every API error returns HTTP 200 with the error in the envelope
    /// ({ success: false, errors: [{ code, message, field }] }); the error
    /// code identifies the failure, not the HTTP status (owner decision
    /// 2026-11-09). Success paths still return 200/201 as before.
    /// </summary>
    private IActionResult Failure(Error error)
    {
        var message = Localizer.Get(error.Code);

        if (error.Extras?.TryGetValue("retryAfterSeconds", out var retry) == true && retry is int seconds)
            Response.Headers.RetryAfter = seconds.ToString();

        var apiError = new ApiError
        {
            Code = error.Code,
            Message = message,
            Field = error.Field,
            Extras = error.Extras is null ? null : new Dictionary<string, object?>(error.Extras)
        };
        return StatusCode(StatusCodes.Status200OK, ApiResponse.Fail(message, [apiError]));
    }
}
