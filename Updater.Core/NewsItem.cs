using System.Text.Json.Serialization;

namespace L2ModernUpdater.Core;

public sealed class NewsItem
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("publishedAt")]
    public DateTimeOffset PublishedAt { get; init; } = DateTimeOffset.UtcNow;
}
