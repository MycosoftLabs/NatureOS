using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace NatureOS.CoreApi.Hubs;

/// <summary>
/// SignalR hub for real-time communication across the NatureOS ecosystem
/// </summary>
public class NatureOSHub : Hub
{
    private readonly ILogger<NatureOSHub> _logger;

    public NatureOSHub(ILogger<NatureOSHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString() ?? "Unknown";
        
        _logger.LogInformation("Client connected: {ConnectionId} from {UserAgent}", connectionId, userAgent);
        
        // Add to general updates group
        await Groups.AddToGroupAsync(connectionId, "AllUsers");
        
        // Send welcome message
        await Clients.Caller.SendAsync("Connected", new
        {
            ConnectionId = connectionId,
            Timestamp = DateTime.UtcNow,
            Message = "Connected to NatureOS real-time hub"
        });
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client disconnected: {ConnectionId}, Exception: {Exception}", 
            connectionId, exception?.Message);
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a device-specific group for updates
    /// </summary>
    public async Task JoinDeviceGroup(string deviceId)
    {
        var groupName = $"device-{deviceId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Client {ConnectionId} joined device group {GroupName}", 
            Context.ConnectionId, groupName);
        
        await Clients.Caller.SendAsync("JoinedGroup", new
        {
            GroupName = groupName,
            Message = $"Joined updates for device {deviceId}"
        });
    }

    /// <summary>
    /// Join a location-based group for regional updates
    /// </summary>
    public async Task JoinLocationGroup(double latitude, double longitude, double radiusKm)
    {
        // Create a simplified location group based on coordinates
        var latGrid = Math.Floor(latitude * 10) / 10; // Round to 0.1 degree precision
        var lngGrid = Math.Floor(longitude * 10) / 10;
        var groupName = $"location-{latGrid}-{lngGrid}";
        
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Client {ConnectionId} joined location group {GroupName} for area {Lat},{Lng} radius {Radius}km", 
            Context.ConnectionId, groupName, latitude, longitude, radiusKm);
        
        await Clients.Caller.SendAsync("JoinedGroup", new
        {
            GroupName = groupName,
            Message = $"Joined updates for location {latitude:F2}, {longitude:F2}"
        });
    }

    /// <summary>
    /// Join the dashboard users group
    /// </summary>
    public async Task JoinDashboardGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "DashboardUsers");
        
        _logger.LogInformation("Client {ConnectionId} joined dashboard group", Context.ConnectionId);
        
        await Clients.Caller.SendAsync("JoinedGroup", new
        {
            GroupName = "DashboardUsers",
            Message = "Joined dashboard updates"
        });
    }

    /// <summary>
    /// Join the MYCA users group for AI assistant updates
    /// </summary>
    public async Task JoinMycaGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "MycaUsers");
        
        _logger.LogInformation("Client {ConnectionId} joined MYCA group", Context.ConnectionId);
        
        await Clients.Caller.SendAsync("JoinedGroup", new
        {
            GroupName = "MycaUsers",
            Message = "Joined MYCA AI assistant updates"
        });
    }

    /// <summary>
    /// Leave a specific group
    /// </summary>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        
        _logger.LogInformation("Client {ConnectionId} left group {GroupName}", 
            Context.ConnectionId, groupName);
        
        await Clients.Caller.SendAsync("LeftGroup", new
        {
            GroupName = groupName,
            Message = $"Left group {groupName}"
        });
    }

    /// <summary>
    /// Send a message to other users (for testing/debugging)
    /// </summary>
    public async Task SendMessage(string message)
    {
        var timestamp = DateTime.UtcNow;
        var connectionId = Context.ConnectionId;
        
        _logger.LogInformation("Message from {ConnectionId}: {Message}", connectionId, message);
        
        await Clients.Others.SendAsync("MessageReceived", new
        {
            From = connectionId,
            Message = message,
            Timestamp = timestamp
        });
    }
} 