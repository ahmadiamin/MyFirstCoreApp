using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;

namespace MyFirstCoreApp.Tests.Common
{
    public sealed class FakeFarazSmsBalanceService : IFarazSmsBalanceService
    {
        public Task<SmsBalanceResult> GetBalanceAsync(
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new SmsBalanceResult(false, null, null, "در تست استفاده نمی‌شود."));
        }
    }
}