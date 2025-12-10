using Microsoft.Azure.Cosmos;
using NatureOS.MINDEX.Models;
using System.Net;

namespace NatureOS.CoreApi.Services;

/// <summary>
/// Service for managing IoT devices in NatureOS
/// </summary>
public class DeviceService : IDeviceService
{
    private readonly Container _devicesContainer;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(CosmosClient cosmosClient, ILogger<DeviceService> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase("mindex");
        _devicesContainer = database.GetContainer("devices");
    }

    public async Task<Device> RegisterDeviceAsync(Device device, CancellationToken cancellationToken = default)
    {
        try
        {
            // Set registration metadata
            device.CreatedAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;
            device.Status = DeviceStatus.Online;
            device.LastSeen = DateTime.UtcNow;

            // Create device with partition key
            var response = await _devicesContainer.CreateItemAsync(
                device,
                new PartitionKey(device.DeviceId),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Registered device {DeviceId} of type {DeviceType}", 
                device.DeviceId, device.DeviceType);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Device {DeviceId} already exists", device.DeviceId);
            throw new InvalidOperationException($"Device {device.DeviceId} already exists", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register device {DeviceId}", device.DeviceId);
            throw;
        }
    }

    public async Task<Device?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _devicesContainer.ReadItemAsync<Device>(
                deviceId,
                new PartitionKey(deviceId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<IEnumerable<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c ORDER BY c.created_at DESC");
            var iterator = _devicesContainer.GetItemQueryIterator<Device>(query);

            var results = new List<Device>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(response);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get devices");
            throw;
        }
    }

    public async Task<Device> UpdateDeviceAsync(Device device, CancellationToken cancellationToken = default)
    {
        try
        {
            // Update timestamp
            device.UpdatedAt = DateTime.UtcNow;

            var response = await _devicesContainer.ReplaceItemAsync(
                device,
                device.DeviceId,
                new PartitionKey(device.DeviceId),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Updated device {DeviceId}", device.DeviceId);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Device {DeviceId} not found for update", device.DeviceId);
            throw new InvalidOperationException($"Device {device.DeviceId} not found", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update device {DeviceId}", device.DeviceId);
            throw;
        }
    }

    public async Task<bool> DeleteDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _devicesContainer.DeleteItemAsync<Device>(
                deviceId,
                new PartitionKey(deviceId),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Deleted device {DeviceId}", deviceId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Device {DeviceId} not found for deletion", deviceId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete device {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<DeviceStatistics> GetDeviceStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var statistics = new DeviceStatistics();

            // Get total count
            var countQuery = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
            var countIterator = _devicesContainer.GetItemQueryIterator<long>(countQuery);
            if (countIterator.HasMoreResults)
            {
                var countResponse = await countIterator.ReadNextAsync(cancellationToken);
                statistics.TotalDevices = countResponse.FirstOrDefault();
            }

            // Get devices by status
            var statusQuery = new QueryDefinition("SELECT c.status, COUNT(1) as count FROM c GROUP BY c.status");
            var statusIterator = _devicesContainer.GetItemQueryIterator<dynamic>(statusQuery);
            while (statusIterator.HasMoreResults)
            {
                var statusResponse = await statusIterator.ReadNextAsync(cancellationToken);
                foreach (var item in statusResponse)
                {
                    if (Enum.TryParse<DeviceStatus>(item.status?.ToString(), out DeviceStatus status))
                    {
                        statistics.DevicesByStatus[status] = (long)item.count;
                    }
                }
            }

            // Get devices by type
            var typeQuery = new QueryDefinition("SELECT c.device_type, COUNT(1) as count FROM c GROUP BY c.device_type");
            var typeIterator = _devicesContainer.GetItemQueryIterator<dynamic>(typeQuery);
            while (typeIterator.HasMoreResults)
            {
                var typeResponse = await typeIterator.ReadNextAsync(cancellationToken);
                foreach (var item in typeResponse)
                {
                    var deviceType = item.device_type?.ToString() ?? "unknown";
                    statistics.DevicesByType[deviceType] = (long)item.count;
                }
            }

            // Get active devices (last 24 hours)
            var activeQuery = new QueryDefinition(
                "SELECT VALUE COUNT(1) FROM c WHERE c.last_seen >= @cutoff")
                .WithParameter("@cutoff", DateTime.UtcNow.AddDays(-1));
            var activeIterator = _devicesContainer.GetItemQueryIterator<long>(activeQuery);
            if (activeIterator.HasMoreResults)
            {
                var activeResponse = await activeIterator.ReadNextAsync(cancellationToken);
                statistics.ActiveDevices = activeResponse.FirstOrDefault();
            }

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device statistics");
            throw;
        }
    }
} 