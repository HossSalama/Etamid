using Etamid.Application.Identity.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Etamid.Api.Controllers;

[EnableRateLimiting("auth")]
public sealed class AuthController : BaseApiController
{
    /// <summary>Step 1 of registration. After this the app verifies the phone with Firebase.</summary>
    [AllowAnonymous]
    [HttpPost("register/individual")]
    public async Task<IActionResult> RegisterIndividual(RegisterIndividualCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "REGISTERED", StatusCodes.Status201Created);

    [AllowAnonymous]
    [HttpPost("register/company")]
    public async Task<IActionResult> RegisterCompany(RegisterCompanyCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "REGISTERED", StatusCodes.Status201Created);

    /// <summary>Step 2: send the Firebase ID token obtained after the SMS check. Activates the account and signs in.</summary>
    [AllowAnonymous]
    [HttpPost("phone/verify")]
    public async Task<IActionResult> VerifyPhone(VerifyPhoneCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "PHONE_VERIFIED");

    /// <summary>Step 2 (Email OTP): verify the 6-digit code sent to the email address.</summary>
    [AllowAnonymous]
    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "PHONE_VERIFIED");

    /// <summary>Resend 6-digit OTP code to email.</summary>
    [AllowAnonymous]
    [HttpPost("otp/resend")]
    public async Task<IActionResult> ResendOtp(ResendOtpCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "OTP_SENT");

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

    /// <summary>Forgot password: send the Firebase ID token for the phone together with the new password.</summary>
    [AllowAnonymous]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "PASSWORD_RESET_OK");
}
