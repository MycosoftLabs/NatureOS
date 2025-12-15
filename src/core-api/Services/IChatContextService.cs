namespace NatureOS.CoreApi.Services;

public interface IChatContextService
{
    /// <summary>
    /// Build minimal context payload suitable for website UI suggestions.
    /// </summary>
    Task<object> BuildLightweightContextAsync(CancellationToken cancellationToken = default);
}
