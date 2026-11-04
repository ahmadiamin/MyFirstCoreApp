namespace MyFirstCoreApp.Application.CustomerPortal;

public sealed record SmsBalanceResult(
    bool Success,
    decimal? BalanceAmount,
    decimal? BalanceCount,
    string? Message);