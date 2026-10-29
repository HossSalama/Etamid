using Etamid.Application.Identity.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Etamid.Api.Controllers;

[EnableRateLimiting("auth")]
public sealed class AuthController : BaseApiController
{
    [AllowAnonymous]
    [HttpPost("register/individual")]
    public async Task<IActionResult> RegisterIndividual(RegisterIndividualCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "REGISTERED", StatusCodes.Status201Created);

    [AllowAnonymous]
    [HttpPost("register/company")]
    public async Task<IActionResult> RegisterCompany(RegisterCompanyCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "REGISTERED", StatusCodes.Status201Created);

    /// <summary>Verifies the six-digit code sent to the registered email and activates the account.</summary>
    [AllowAnonymous]
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "EMAIL_VERIFIED");

    [AllowAnonymous]
    [HttpPost("otp/resend")]
    public async Task<IActionResult> ResendEmailCode(ResendEmailCodeCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "OTP_SENT");

    /// <summary>Legacy phone-verification endpoint. Phone verification no longer substitutes for email activation.</summary>
    [AllowAnonymous]
    [HttpPost("phone/verify")]
    public async Task<IActionResult> VerifyPhone(VerifyPhoneCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "PHONE_VERIFIED");

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "LOGGED_IN");

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "TOKEN_REFRESHED");

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutCommand? command, CancellationToken ct)
        => Respond(await Sender.Send(command ?? new LogoutCommand(null, null), ct), "LOGGED_OUT");

    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
        => Respond(await Sender.Send(new LogoutAllCommand(), ct), "LOGGED_OUT");

    [AllowAnonymous]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "PASSWORD_RESET_CODE_SENT");

    [AllowAnonymous]
    [HttpPost("password/verify-otp")]
    public async Task<IActionResult> VerifyResetCode(VerifyResetCodeCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "RESET_CODE_VERIFIED");

    [AllowAnonymous]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "PASSWORD_RESET_OK");
}
