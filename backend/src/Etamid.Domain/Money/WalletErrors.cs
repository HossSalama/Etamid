using Etamid.SharedKernel;

namespace Etamid.Domain.Money;

public static class WalletErrors
{
    /// <summary>The wallet is locked until activation: no balance, history, or withdrawal is visible.</summary>
    public static readonly Error NotActivated = Error.Forbidden("WALLET_NOT_ACTIVATED");

    public static readonly Error InsufficientBalance = Error.Validation("WALLET_INSUFFICIENT_BALANCE", "amount");

    public static readonly Error BankAccountRequired = Error.Validation("WALLET_BANK_ACCOUNT_REQUIRED");

    public static readonly Error WithdrawalNotFound = Error.NotFound("WALLET_WITHDRAWAL_NOT_FOUND");

    public static readonly Error InvalidState = Error.Conflict("WALLET_INVALID_STATE");

    /// <summary>Admin endpoints are operator-only; operators are the user ids in the wallet.operators setting.</summary>
    public static readonly Error NotOperator = Error.Forbidden("WALLET_NOT_OPERATOR");
}
