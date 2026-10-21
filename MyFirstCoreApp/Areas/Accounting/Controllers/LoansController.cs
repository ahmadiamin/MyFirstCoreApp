using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFirstCoreApp.Application.Attachments;
using MyFirstCoreApp.Application.Common.Interfaces;
using MyFirstCoreApp.Application.Common.Utilities;
using MyFirstCoreApp.Application.Interfaces;
//using MyFirstCoreApp.Application.Loans;
using MyFirstCoreApp.Areas.Accounting.ViewModels.Loan;
using MyFirstCoreApp.Domain.Enums;
using MyFirstCoreApp.Infrastructure.Persistence;

namespace MyFirstCoreApp.Areas.Accounting.Controllers;

[Area("Accounting")]
[Authorize]
public class LoansController : Controller
{
    //private const string InvalidDateMessage = "تاریخ نامعتبر است. نمونه‌ی درست: 1405/01/10";
    //private const string InvalidAmountMessage = "مبلغ نامعتبر است. فقط عدد صحیح (ریال) وارد کنید.";
    //private const string NoTenantMessage = "شرکت فعال مشخص نیست.";
    //private const string UnexpectedErrorMessage = "خطای غیرمنتظره رخ داد. لطفاً دوباره تلاش کنید.";

    //private const long MaxRequestBytes = 10 * 1024 * 1024;

    //private readonly ApplicationDbContext _context;
    //private readonly ICurrentTenant _currentTenant;
    //private readonly ILoanService _loanService;
    //private readonly TimeProvider _timeProvider;
    //private readonly ILogger<LoansController> _logger;

    //public LoansController(
    //    ApplicationDbContext context,
    //    ICurrentTenant currentTenant,
    //    ILoanService loanService,
    //    TimeProvider timeProvider,
    //    ILogger<LoansController> logger)
    //{
    //    _context = context;
    //    _currentTenant = currentTenant;
    //    _loanService = loanService;
    //    _timeProvider = timeProvider;
    //    _logger = logger;
    //}

    //// ============================================
    //// لیست
    //// ============================================

    //[Authorize(Policy = "Loan.View")]
    //public async Task<IActionResult> Index(int? direction = null, bool onlyOpen = true)
    //{
    //    var tenantId = _currentTenant.TenantId;
    //    if (!tenantId.HasValue) return Forbid();

    //    LoanDirection? filter = direction.HasValue && Enum.IsDefined(typeof(LoanDirection), direction.Value)
    //        ? (LoanDirection)direction.Value
    //        : null;

    //    var loans = await _loanService.GetLoansAsync(filter, onlyOpen);

    //    var model = new LoanIndexViewModel
    //    {
    //        Direction = direction,
    //        OnlyOpen = onlyOpen,
    //        Items = loans.Select(ToItem).ToList(),
    //        TotalGivenRemaining = loans.Where(l => l.Direction == LoanDirection.Given).Sum(l => l.Remaining),
    //        TotalTakenRemaining = loans.Where(l => l.Direction == LoanDirection.Taken).Sum(l => l.Remaining)
    //    };

    //    return View(model);
    //}

    //// ============================================
    //// ثبت قرض
    //// ============================================

    //[Authorize(Policy = "Loan.Create")]
    //public async Task<IActionResult> Create(LoanDirection direction = LoanDirection.Given)
    //{
    //    var model = new LoanFormViewModel
    //    {
    //        Direction = direction,
    //        Date = PersianDate.Format(_timeProvider.Today())
    //    };

    //    await FillOptionsAsync(model);
    //    return View(model);
    //}

    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //[Authorize(Policy = "Loan.Create")]
    //[RequestSizeLimit(MaxRequestBytes)]
    //[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    //public async Task<IActionResult> Create(LoanFormViewModel model)
    //{
    //    var tenantId = _currentTenant.TenantId;
    //    if (!tenantId.HasValue) return Forbid();

    //    if (!PersianDate.TryParse(model.Date, out var date))
    //        ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

    //    if (!TryParseAmount(model.AmountText, out var amount))
    //        ModelState.AddModelError(nameof(model.AmountText), InvalidAmountMessage);

    //    if (!ModelState.IsValid)
    //    {
    //        await FillOptionsAsync(model);
    //        return View(model);
    //    }

    //    try
    //    {
    //        await using var receiptStream = model.Receipt?.OpenReadStream();

    //        var loanId = await _loanService.CreateAsync(
    //            new LoanRequest
    //            {
    //                Direction = model.Direction,
    //                ContactId = model.ContactId!.Value,
    //                AccountId = model.AccountId!.Value,
    //                Amount = amount,
    //                Date = date,
    //                Description = model.Description
    //            },
    //            tenantId.Value,
    //            ToUpload(model.Receipt, receiptStream));

    //        TempData["Success"] = "قرض با موفقیت ثبت شد.";
    //        return RedirectToAction(nameof(Details), new { id = loanId });
    //    }
    //    catch (InvalidOperationException ex)
    //    {
    //        ModelState.AddModelError("", ex.Message);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Loan creation failed");
    //        ModelState.AddModelError("", UnexpectedErrorMessage);
    //    }

    //    await FillOptionsAsync(model);
    //    return View(model);
    //}

    //// ============================================
    //// جزئیات و بازپرداخت
    //// ============================================

    //[Authorize(Policy = "Loan.View")]
    //public async Task<IActionResult> Details(int id)
    //{
    //    var loan = await _loanService.GetAsync(id);
    //    if (loan == null) return NotFound();

    //    var model = new LoanDetailsViewModel
    //    {
    //        Loan = ToItem(loan),
    //        Repayments = loan.Repayments.Select(r => new LoanRepaymentViewModel
    //        {
    //            Id = r.Id,
    //            Number = r.Number,
    //            Date = PersianDate.Format(r.Date),
    //            Amount = r.Amount,
    //            Description = r.Description
    //        }).ToList(),
    //        RepayForm = new LoanRepayFormViewModel
    //        {
    //            LoanId = id,
    //            Date = PersianDate.Format(_timeProvider.Today()),
    //            AmountText = loan.Remaining > 0 ? loan.Remaining.ToString("0", CultureInfo.InvariantCulture) : string.Empty
    //        },
    //        AccountOptions = await GetAccountOptionsAsync()
    //    };

    //    return View(model);
    //}

    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //[Authorize(Policy = "Loan.Edit")]
    //[RequestSizeLimit(MaxRequestBytes)]
    //[RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    //public async Task<IActionResult> Repay(LoanRepayFormViewModel model)
    //{
    //    var tenantId = _currentTenant.TenantId;
    //    if (!tenantId.HasValue) return Forbid();

    //    if (!PersianDate.TryParse(model.Date, out var date))
    //        ModelState.AddModelError(nameof(model.Date), InvalidDateMessage);

    //    if (!TryParseAmount(model.AmountText, out var amount))
    //        ModelState.AddModelError(nameof(model.AmountText), InvalidAmountMessage);

    //    if (!ModelState.IsValid)
    //    {
    //        TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
    //        return RedirectToAction(nameof(Details), new { id = model.LoanId });
    //    }

    //    try
    //    {
    //        await using var receiptStream = model.Receipt?.OpenReadStream();

    //        await _loanService.RepayAsync(
    //            model.LoanId,
    //            new LoanRepaymentRequest
    //            {
    //                AccountId = model.AccountId!.Value,
    //                Amount = amount,
    //                Date = date,
    //                Description = model.Description
    //            },
    //            tenantId.Value,
    //            ToUpload(model.Receipt, receiptStream));

    //        TempData["Success"] = "بازپرداخت ثبت شد.";
    //    }
    //    catch (InvalidOperationException ex)
    //    {
    //        TempData["Error"] = ex.Message;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Loan repayment failed");
    //        TempData["Error"] = UnexpectedErrorMessage;
    //    }

    //    return RedirectToAction(nameof(Details), new { id = model.LoanId });
    //}

    //// ============================================
    //// حذف
    //// ============================================

    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //[Authorize(Policy = "Loan.Delete")]
    //public async Task<IActionResult> Delete(int id)
    //{
    //    var tenantId = _currentTenant.TenantId;
    //    if (!tenantId.HasValue) return Forbid();

    //    try
    //    {
    //        await _loanService.DeleteAsync(id, tenantId.Value);
    //        TempData["Success"] = "قرض حذف شد.";
    //        return RedirectToAction(nameof(Index));
    //    }
    //    catch (InvalidOperationException ex)
    //    {
    //        TempData["Error"] = ex.Message;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Loan deletion failed");
    //        TempData["Error"] = UnexpectedErrorMessage;
    //    }

    //    return RedirectToAction(nameof(Details), new { id });
    //}

    //[HttpPost]
    //[ValidateAntiForgeryToken]
    //[Authorize(Policy = "Loan.Delete")]
    //public async Task<IActionResult> DeleteRepayment(int id, int loanId)
    //{
    //    var tenantId = _currentTenant.TenantId;
    //    if (!tenantId.HasValue) return Forbid();

    //    try
    //    {
    //        await _loanService.DeleteRepaymentAsync(id, tenantId.Value);
    //        TempData["Success"] = "بازپرداخت حذف شد.";
    //    }
    //    catch (InvalidOperationException ex)
    //    {
    //        TempData["Error"] = ex.Message;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Loan repayment deletion failed");
    //        TempData["Error"] = UnexpectedErrorMessage;
    //    }

    //    return RedirectToAction(nameof(Details), new { id = loanId });
    //}

    //// ============================================
    //// Helpers
    //// ============================================

    //private static LoanListItemViewModel ToItem(LoanListItem loan) => new()
    //{
    //    Id = loan.Id,
    //    Number = loan.Number,
    //    Date = PersianDate.Format(loan.Date),
    //    IsGiven = loan.Direction == LoanDirection.Given,
    //    ContactName = loan.ContactName,
    //    Amount = loan.Amount,
    //    Repaid = loan.Repaid,
    //    Remaining = loan.Remaining,
    //    Description = loan.Description
    //};

    //private static AttachmentUpload? ToUpload(IFormFile? file, Stream? stream)
    //    => file == null || stream == null || file.Length == 0
    //        ? null
    //        : new AttachmentUpload { FileName = file.FileName, Length = file.Length, Content = stream };

    ///// <summary>
    ///// مبلغ را از متن می‌خواند: ارقام فارسی/عربی، جداکننده‌ی هزارگان («,» «٬» «،») و فاصله مجاز است؛
    ///// اعشار، علامت و هر نویسه‌ی دیگر مجاز نیست.
    ///// </summary>
    //private static bool TryParseAmount(string? text, out decimal amount)
    //{
    //    amount = 0;
    //    if (string.IsNullOrWhiteSpace(text)) return false;

    //    var cleaned = PersianDate.NormalizeDigits(text)
    //        .Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "");

    //    return decimal.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount > 0;
    //}

    //private async Task FillOptionsAsync(LoanFormViewModel model)
    //{
    //    model.ContactOptions = await GetContactOptionsAsync();
    //    model.AccountOptions = await GetAccountOptionsAsync();
    //}

    //private async Task<List<SelectListItem>> GetContactOptionsAsync()
    //{
    //    var tenantId = _currentTenant.TenantId;
    //    if (!tenantId.HasValue) return new();

    //    return await _context.Contacts
    //        .Where(c => c.TenantId == tenantId.Value && c.IsActive)
    //        .OrderBy(c => c.Name)
    //        .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
    //        .ToListAsync();
    //}

    //private async Task<List<SelectListItem>> GetAccountOptionsAsync()
    //{
    //    var tenantId = _currentTenant.TenantId;
    //    if (!tenantId.HasValue) return new();

    //    return await _context.Accounts
    //        .Where(a => a.TenantId == tenantId.Value && a.IsActive
    //                 && (a.AccountType == AccountType.Bank || a.AccountType == AccountType.Cash))
    //        .OrderBy(a => a.Name)
    //        .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Name })
    //        .ToListAsync();
    //}
}
