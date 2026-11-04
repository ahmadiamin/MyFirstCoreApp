namespace MyFirstCoreApp.Application.CustomerPortal
{
    using System.Text.Json.Serialization;

    public sealed class FarazSmsDeliveryResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("data")]
        public FarazSmsDeliveryData? Data { get; init; }
    }

    public sealed class FarazSmsDeliveryData
    {
        [JsonPropertyName("data")]
        public List<FarazSmsDeliveryItem> Items { get; init; } = [];
    }

    public sealed class FarazSmsDeliveryItem
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("request_id")]
        public long RequestId { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("destination")]
        public string? Destination { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("pages")]
        public int Pages { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; init; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}