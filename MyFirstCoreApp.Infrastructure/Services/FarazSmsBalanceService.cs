using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using MyFirstCoreApp.Application.CustomerPortal;
using MyFirstCoreApp.Application.Interfaces;

namespace MyFirstCoreApp.Infrastructure.Services;

public sealed class FarazSmsBalanceService : IFarazSmsBalanceService
{
    private const string Endpoint =
        "https://api.iranpayamak.com/ws/v1/account/balance";

    private readonly HttpClient _httpClient;

    public FarazSmsBalanceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SmsBalanceResult> GetBalanceAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(false, null, null, "کلید API پیامک ثبت نشده است.");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            request.Headers.TryAddWithoutValidation("Api-Key", apiKey);

            using var response = await _httpClient.SendAsync(
                request, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new(
                    false, null, null,
                    $"دریافت اعتبار پیامک ناموفق بود (HTTP {(int)response.StatusCode}).");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (!root.TryGetProperty("status", out var status) ||
                !string.Equals(status.GetString(), "success",
                    StringComparison.OrdinalIgnoreCase))
            {
                var message = root.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : null;

                return new(false, null, null,
                    string.IsNullOrWhiteSpace(message)
                        ? "دریافت اعتبار از سرویس پیامک ناموفق بود."
                        : message);
            }

            if (!root.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("balance_amount", out var amountElement) ||
                !data.TryGetProperty("balance_count", out var countElement))
            {
                return new(false, null, null,
                    "اطلاعات اعتبار در پاسخ سرویس وجود ندارد.");
            }

            if (!TryReadDecimal(amountElement, out var amount) ||
                !TryReadDecimal(countElement, out var count))
            {
                return new(false, null, null,
                    "مقادیر اعتبار دریافتی معتبر نیستند.");
            }

            return new(true, amount, count, null);
        }
        catch (HttpRequestException)
        {
            return new(false, null, null, "ارتباط با سرویس پیامک برقرار نشد.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, null, "مهلت دریافت اعتبار پیامک به پایان رسید.");
        }
        catch (JsonException)
        {
            return new(false, null, null, "پاسخ سرویس اعتبار پیامک معتبر نیست.");
        }
    }

    private static bool TryReadDecimal(
        JsonElement element,
        out decimal value)
    {
        value = 0;

        var text = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString();

        return decimal.TryParse(
            text,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out value);
    }
}