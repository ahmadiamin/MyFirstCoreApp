namespace MyFirstCoreApp.Application.CustomerPortal;

public sealed record FarazSmsSendResult(bool IsAccepted, string? RequestId, string Message);

public sealed record FarazSmsDeliveryReport(
    string RequestId,
    bool HttpSuccess,
    string ResponseJson,
    string Message);
