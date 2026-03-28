namespace NatureOS.CoreApi.Services;

public sealed class ChatContextService : IChatContextService
{
    private readonly IDeviceService _deviceService;
    private readonly IEventService _eventService;

    public ChatContextService(IDeviceService deviceService, IEventService eventService)
    {
        _deviceService = deviceService;
        _eventService = eventService;
    }

    public async Task<object> BuildLightweightContextAsync(CancellationToken cancellationToken = default)
    {
        var deviceStats = await _deviceService.GetDeviceStatisticsAsync(cancellationToken);
        var eventStats = await _eventService.GetEventStatisticsAsync(new EventQuery(), cancellationToken);

        deviceStats.DevicesByStatus.TryGetValue(NatureOS.MINDEX.Models.DeviceStatus.Online, out var onlineCount);

        return new
        {
            activeDevices = onlineCount,
            totalDevices = deviceStats.TotalCount,
            eventsToday = eventStats.TodayCount,
            eventsPerHour = eventStats.AveragePerHour,
            topDomains = eventStats.EventsByDomain
                .OrderByDescending(kv => kv.Value)
                .Take(5)
                .Select(kv => new { domain = kv.Key, count = kv.Value })
                .ToArray()
        };
    }
}
