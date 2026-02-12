using NatureOS.MINDEX.Models;
using System.Text.Json;

namespace NatureOS.CoreApi.Services;

public interface IMycoBrainService
{
    Task<ProcessingResult> ProcessTelemetryAsync(MycoBrainTelemetry telemetry, CancellationToken cancellationToken = default);
    Task<ProcessingResult> ProcessNDJSONLineAsync(string jsonLine, CancellationToken cancellationToken = default);
    Task<ProcessingResult> ProcessMDPFrameAsync(byte[] frame, CancellationToken cancellationToken = default);
    Task<ProcessingResult> ProcessEnvelopeAsync(JsonElement envelope, CancellationToken cancellationToken = default);

    Task<bool> SendCommandAsync(MycoBrainCommand command, CancellationToken cancellationToken = default);

    Task<MycoBrainDevice> RegisterDeviceAsync(MycoBrainDevice device, CancellationToken cancellationToken = default);
    Task<MycoBrainDevice?> GetDeviceAsync(string serialNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<MycoBrainDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<MycoBrainDevice> UpdateDeviceAsync(MycoBrainDevice device, CancellationToken cancellationToken = default);

    Task<IEnumerable<MycoBrainTelemetry>> GetTelemetryHistoryAsync(string serialNumber, DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default);
}
