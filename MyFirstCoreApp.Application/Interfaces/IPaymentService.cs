using MyFirstCoreApp.Domain.Entities.Banking;

namespace MyFirstCoreApp.Application.Interfaces;

public interface IPaymentService
{
    Task<int> GetNextNumberAsync(int tenantId);

    Task<Payment> CreateAsync(Payment payment);
    Task UpdateAsync(Payment payment);
    Task DeleteAsync(int id, int tenantId);

    Task PostAsync(int paymentId, int tenantId);
    Task UnpostAsync(int paymentId, int tenantId);
}