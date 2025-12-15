using Microsoft.Azure.Cosmos;
using Ulid;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Minimal feedback sink for website/MAS. Stores entries in Cosmos DB (mindex/myca_feedback).
/// </summary>
public sealed class FeedbackStore : IFeedbackStore
{
    private readonly Container _container;
    private readonly ILogger<FeedbackStore> _logger;

    public FeedbackStore(CosmosClient cosmosClient, ILogger<FeedbackStore> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase("mindex");
        _container = database.GetContainer("myca_feedback");
    }

    public async Task AppendAsync(FeedbackEntry entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
            entry.Id = Ulid.NewUlid().ToString();

        try
        {
            await _container.CreateItemAsync(entry, new PartitionKey(entry.ConversationId), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Container missing is an infrastructure/config issue; don't throw 500s for feedback capture.
            _logger.LogWarning(ex, "Feedback container missing (mindex/myca_feedback). Feedback not persisted.");
        }
    }
}
