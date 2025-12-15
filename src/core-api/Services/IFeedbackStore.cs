namespace NatureOS.CoreApi.Services;

public interface IFeedbackStore
{
    Task AppendAsync(FeedbackEntry entry, CancellationToken cancellationToken = default);
}

public sealed class FeedbackEntry
{
    public string Id { get; set; } = string.Empty;
    public required string ConversationId { get; set; }
    public required string Feedback { get; set; } // "positive" | "negative" | free text
    public string? Note { get; set; }
    public DateTime TimestampUtc { get; set; }
}
