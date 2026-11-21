using System.ComponentModel.DataAnnotations;
using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Areas.Projects.ViewModels;

public sealed class ProjectExpenseFormViewModel
{
    public int? ProjectId { get; set; }
    public int? ContactId { get; set; }
    [Required(ErrorMessage = "حساب هزینه را انتخاب کنید.")]
    public int ExpenseAccountId { get; set; }
    [Required(ErrorMessage = "مبلغ الزامی است.")]
    public string AmountText { get; set; } = string.Empty;
    [Required(ErrorMessage = "تاریخ الزامی است.")]
    public string DateText { get; set; } = string.Empty;
    [Required(ErrorMessage = "شرح هزینه الزامی است.")]
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
}

public sealed class ProjectExpensePaymentFormViewModel
{
    public int ProjectExpenseId { get; set; }
    [Required(ErrorMessage = "تاریخ الزامی است.")]
    public string DateText { get; set; } = string.Empty;
    [Required(ErrorMessage = "مبلغ الزامی است.")]
    public string AmountText { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.BankTransfer;
    public string? Reference { get; set; }
    public string? Description { get; set; }
}