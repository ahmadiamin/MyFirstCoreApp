using Microsoft.EntityFrameworkCore;
using MyFirstCoreApp.Application.Accounting;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
using MyFirstCoreApp.Domain.Entities.Accounting;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Infrastructure.Services;

public class FiscalYearService : IFiscalYearService
{
    private const int MaxYearLengthInDays = 400;

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public FiscalYearService(ApplicationDbContext context, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _context = context;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    // ============================================
    // ثبت
    // ============================================

    public Task<FiscalYear> CreateAsync(FiscalYearRequest request, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            await ValidateAsync(request, tenantId, excludeId: null);

            var year = new FiscalYear
            {
                TenantId = tenantId,
                Name = ResolveName(request),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Status = FiscalYearStatus.Open
            };

            _context.FiscalYears.Add(year);
            await _context.SaveChangesAsync();

            return year;
        });
    }

    // ============================================
    // ویرایش
    // ============================================

    public Task UpdateAsync(int fiscalYearId, FiscalYearRequest request, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var year = await GetAsync(fiscalYearId, tenantId);

            if (year.Status == FiscalYearStatus.Closed)
                throw new InvalidOperationException("سال مالی بسته قابل ویرایش نیست. ابتدا آن را باز کنید.");

            await ValidateAsync(request, tenantId, excludeId: year.Id);

            // هیچ سند موجودی نباید از بازه‌ی جدید بیرون بیفتد
            var outside = await _context.Transactions.CountAsync(t =>
                t.FiscalYearId == year.Id &&
                (t.Date < request.StartDate || t.Date > request.EndDate));

            if (outside > 0)
            {
                throw new InvalidOperationException(
                    $"{outside} سند این سال خارج از بازه‌ی جدید قرار می‌گیرد. بازه را گسترده‌تر کنید.");
            }

            year.Name = ResolveName(request);
            year.StartDate = request.StartDate;
            year.EndDate = request.EndDate;

            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // بستن / باز کردن
    // ============================================

    public Task CloseAsync(int fiscalYearId, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var year = await GetAsync(fiscalYearId, tenantId);

            if (year.Status == FiscalYearStatus.Closed)
                throw new InvalidOperationException("این سال مالی قبلاً بسته شده است.");

            // سند پیش‌نویس هنوز در گزارش‌ها نیست؛ با بستن سال دیگر هم نمی‌شود قطعی‌اش کرد
            var drafts = await _context.Transactions.CountAsync(t =>
                t.FiscalYearId == year.Id && !t.IsPosted);

            if (drafts > 0)
            {
                throw new InvalidOperationException(
                    $"این سال {drafts} سند پیش‌نویس دارد. ابتدا آن‌ها را قطعی یا حذف کنید.");
            }

            year.Status = FiscalYearStatus.Closed;
            year.ClosedAt = _timeProvider.GetUtcNow().UtcDateTime;
            year.ClosedBy = _currentUser.UserId;

            await _context.SaveChangesAsync();
        });
    }

    public Task ReopenAsync(int fiscalYearId, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var year = await GetAsync(fiscalYearId, tenantId);

            if (year.Status != FiscalYearStatus.Closed)
                throw new InvalidOperationException("این سال مالی باز است.");

            var laterClosed = await _context.FiscalYears.AnyAsync(f =>
                f.TenantId == tenantId &&
                f.StartDate > year.StartDate &&
                f.Status == FiscalYearStatus.Closed);

            if (laterClosed)
            {
                throw new InvalidOperationException(
                    "برای باز کردن این سال، ابتدا سال‌های مالیِ بعدی را که بسته‌اند باز کنید.");
            }

            year.Status = FiscalYearStatus.Open;
            year.ClosedAt = null;
            year.ClosedBy = null;

            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // حذف
    // ============================================

    public Task DeleteAsync(int fiscalYearId, int tenantId)
    {
        return _context.ExecuteInTransactionAsync(async () =>
        {
            var year = await GetAsync(fiscalYearId, tenantId);

            if (year.Status == FiscalYearStatus.Closed)
                throw new InvalidOperationException("سال مالی بسته قابل حذف نیست.");

            if (await _context.Transactions.AnyAsync(t => t.FiscalYearId == year.Id))
                throw new InvalidOperationException("این سال مالی سند دارد و قابل حذف نیست.");

            // حذف واقعی را Interceptor به حذف نرم تبدیل می‌کند
            _context.FiscalYears.Remove(year);
            await _context.SaveChangesAsync();
        });
    }

    // ============================================
    // پرس‌وجو
    // ============================================

    public async Task<FiscalYear?> GetCurrentAsync(int tenantId)
    {
        var today = _timeProvider.Today();

        var current = await _context.FiscalYears
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.StartDate <= today && f.EndDate >= today);

        if (current != null) return current;

        return await _context.FiscalYears
            .Where(f => f.TenantId == tenantId)
            .OrderByDescending(f => f.StartDate)
            .FirstOrDefaultAsync();
    }

    // ============================================
    // Helpers
    // ============================================

    private async Task<FiscalYear> GetAsync(int fiscalYearId, int tenantId)
    {
        var year = await _context.FiscalYears
            .FirstOrDefaultAsync(f => f.Id == fiscalYearId && f.TenantId == tenantId);

        return year ?? throw new InvalidOperationException("سال مالی پیدا نشد.");
    }

    private static string ResolveName(FiscalYearRequest request)
        => string.IsNullOrWhiteSpace(request.Name)
            ? FiscalYearRules.DefaultName(request.StartDate)
            : request.Name.Trim();

    private async Task ValidateAsync(FiscalYearRequest request, int tenantId, int? excludeId)
    {
        if (request.StartDate == default)
            throw new InvalidOperationException("تاریخ شروع سال مالی الزامی است.");

        if (request.EndDate == default)
            throw new InvalidOperationException("تاریخ پایان سال مالی الزامی است.");

        if (request.EndDate < request.StartDate)
            throw new InvalidOperationException("تاریخ پایان نباید قبل از تاریخ شروع باشد.");

        var lengthInDays = request.EndDate.DayNumber - request.StartDate.DayNumber + 1;
        if (lengthInDays > MaxYearLengthInDays)
            throw new InvalidOperationException($"طول سال مالی نباید بیش از {MaxYearLengthInDays} روز باشد.");

        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length > 100)
            throw new InvalidOperationException("نام سال مالی حداکثر ۱۰۰ کاراکتر است.");

        var overlapping = await _context.FiscalYears
            .Where(f => f.TenantId == tenantId
                     && f.Id != (excludeId ?? 0)
                     && f.StartDate <= request.EndDate
                     && f.EndDate >= request.StartDate)
            .Select(f => f.Name)
            .FirstOrDefaultAsync();

        if (overlapping != null)
            throw new InvalidOperationException($"این بازه با «{overlapping}» همپوشانی دارد.");
    }
}
