using MyFirstCoreApp.Domain.Enums;

namespace MyFirstCoreApp.Application.Loans;

/// <summary>
/// قرض جدول جدا ندارد؛ یک «سند خودکار» با ReferenceType مخصوص است و با همان دفتر و سال مالی کار می‌کند.
///   LoanGiven / LoanTaken                  : سند خودِ قرض (ReferenceId خالی؛ شناسه‌ی قرض = شناسه‌ی سند)
///   LoanGivenRepayment / LoanTakenRepayment: سند بازپرداخت (ReferenceId = شناسه‌ی سند قرض)
/// </summary>
public static class LoanDocumentTypes
{
    public const string Given = "LoanGiven";
    public const string Taken = "LoanTaken";
    public const string GivenRepayment = "LoanGivenRepayment";
    public const string TakenRepayment = "LoanTakenRepayment";

    public static bool IsLoan(string? referenceType) => referenceType is Given or Taken;
    public static bool IsRepayment(string? referenceType) => referenceType is GivenRepayment or TakenRepayment;

    public static string LoanTypeFor(LoanDirection direction)
        => direction == LoanDirection.Given ? Given : Taken;

    public static string RepaymentTypeFor(LoanDirection direction)
        => direction == LoanDirection.Given ? GivenRepayment : TakenRepayment;

    public static LoanDirection DirectionOf(string referenceType)
        => referenceType is Given or GivenRepayment ? LoanDirection.Given : LoanDirection.Taken;
}

public sealed class LoanRequest
{
    public LoanDirection Direction { get; set; }
    public int ContactId { get; set; }

    /// <summary>حساب «بانک» یا «صندوق (نقد)» که پول از آن خارج یا به آن وارد می‌شود.</summary>
    public int AccountId { get; set; }

    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
}

public sealed class LoanRepaymentRequest
{
    public int AccountId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
}

public class LoanListItem
{
    /// <summary>شناسه‌ی قرض = شناسه‌ی سند.</summary>
    public int Id { get; set; }
    public int Number { get; set; }
    public DateOnly Date { get; set; }
    public LoanDirection Direction { get; set; }
    public int ContactId { get; set; }
    public string? ContactName { get; set; }
    public decimal Amount { get; set; }
    public decimal Repaid { get; set; }
    public decimal Remaining => Amount - Repaid;
    public string? Description { get; set; }
}

public sealed class LoanRepaymentItem
{
    public int Id { get; set; }
    public int Number { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public sealed class LoanDetails : LoanListItem
{
    public List<LoanRepaymentItem> Repayments { get; set; } = new();
}