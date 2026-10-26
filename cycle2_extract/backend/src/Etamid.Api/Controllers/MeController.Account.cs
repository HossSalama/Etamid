using Etamid.Application.Files;
using Etamid.Application.Identity.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Etamid.Api.Controllers;

public sealed partial class MeController
{
    // ---------- Avatar
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileUploadService.MaxBytes + 1_048_576)]
    public async Task<IActionResult> UploadAvatar([FromForm] FileForm form, CancellationToken ct)
    {
        if (form.File is null || form.File.Length == 0)
            return Respond<AvatarDto>(FileErrors.Required);

        await using var stream = form.File.OpenReadStream();
        return Respond(await Sender.Send(new UploadAvatarCommand(stream, form.File.FileName), ct), "AVATAR_UPDATED");
    }

    // ---------- Bank account
    [HttpGet("bank-account")]
    public async Task<IActionResult> GetBankAccount(CancellationToken ct)
        => Respond(await Sender.Send(new GetMyBankAccountQuery(), ct));

    [HttpPut("bank-account")]
    public async Task<IActionResult> SaveBankAccount(SaveBankAccountCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "BANK_ACCOUNT_SAVED");

    // ---------- Nafath
    [HttpPost("kyc/nafath/start")]
    public async Task<IActionResult> StartNafath(CancellationToken ct)
        => Respond(await Sender.Send(new StartNafathCommand(), ct), "NAFATH_STARTED");

    [HttpGet("kyc/nafath/status")]
    public async Task<IActionResult> NafathStatus([FromQuery] string requestId, CancellationToken ct)
        => Respond(await Sender.Send(new GetNafathStatusQuery(requestId), ct));

    // ---------- Store
    [HttpGet("store")]
    public async Task<IActionResult> GetStore(CancellationToken ct)
        => Respond(await Sender.Send(new GetMyStoreQuery(), ct));

    [HttpPut("store")]
    public async Task<IActionResult> SaveStore(SaveMyStoreCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "STORE_SAVED");

    // ---------- Addresses
    [HttpGet("addresses")]
    public async Task<IActionResult> ListAddresses(CancellationToken ct)
        => Respond(await Sender.Send(new ListMyAddressesQuery(), ct));

    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress(AddAddressCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command, ct), "ADDRESS_SAVED", StatusCodes.Status201Created);

    [HttpPut("addresses/{id:long}")]
    public async Task<IActionResult> UpdateAddress(long id, UpdateAddressCommand command, CancellationToken ct)
        => Respond(await Sender.Send(command with { Id = id }, ct), "ADDRESS_SAVED");

    [HttpPut("addresses/{id:long}/default")]
    public async Task<IActionResult> SetDefaultAddress(long id, CancellationToken ct)
        => Respond(await Sender.Send(new SetDefaultAddressCommand(id), ct), "ADDRESS_SAVED");

    [HttpDelete("addresses/{id:long}")]
    public async Task<IActionResult> DeleteAddress(long id, CancellationToken ct)
        => Respond(await Sender.Send(new DeleteAddressCommand(id), ct), "ADDRESS_DELETED");
}
