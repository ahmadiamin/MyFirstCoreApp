using MyFirstCoreApp.Application.CustomerPortal;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IFarazSmsBalanceService
{
    Task<SmsBalanceResult> GetBalanceAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}