using System.Text.Json.Serialization;

namespace NatureOS.MINDEX.Models;

/// <summary>
/// MycoBrain device telemetry model (Side-A + Side-B)
/// </summary>
public class MycoBrainTelemetry
{
    [JsonPropertyName("seq")]
    public uint SequenceNumber { get; set; }

    [JsonPropertyName("serial")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("fw_version")]
    public string FirmwareVersion { get; set; } = string.Empty;

    /// <summary>
    /// Device timestamp (Unix epoch ms)
    /// </summary>
    [JsonPropertyName("ts")]
    public long DeviceTimestamp { get; set; }

    [JsonPropertyName("side_a")]
    public MycoBrainSideA? SideA { get; set; }

    [JsonPropertyName("side_b")]
    public MycoBrainSideB? SideB { get; set; }

    [JsonPropertyName("power")]
    public MycoBrainPowerStatus? Power { get; set; }
}

public class MycoBrainSideA
{
    [JsonPropertyName("ai_counts")]
    public Dictionary<string, int>? AnalogInputCounts { get; set; }

    [JsonPropertyName("ai_volts")]
    public Dictionary<string, double>? AnalogInputVolts { get; set; }

    [JsonPropertyName("bme688")]
    public BME688Reading? BME688 { get; set; }

    [JsonPropertyName("i2c_devices")]
    public List<string>? I2CDevices { get; set; }

    [JsonPropertyName("mosfet_states")]
    public Dictionary<string, bool>? MosfetStates { get; set; }

    [JsonPropertyName("uptime_ms")]
    public ulong UptimeMs { get; set; }
}

public class MycoBrainSideB
{
    [JsonPropertyName("rssi")]
    public int? RSSI { get; set; }

    [JsonPropertyName("snr")]
    public double? SNR { get; set; }

    [JsonPropertyName("tx_count")]
    public uint TxCount { get; set; }

    [JsonPropertyName("rx_count")]
    public uint RxCount { get; set; }

    [JsonPropertyName("ack_count")]
    public uint AckCount { get; set; }

    [JsonPropertyName("retry_count")]
    public uint RetryCount { get; set; }

    [JsonPropertyName("uart_buffer")]
    public UartBufferStatus? UartBuffer { get; set; }
}

public class BME688Reading
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("humidity")]
    public double Humidity { get; set; }

    [JsonPropertyName("pressure")]
    public double Pressure { get; set; }

    [JsonPropertyName("gas_resistance")]
    public double GasResistance { get; set; }

    [JsonPropertyName("chip_id")]
    public byte? ChipId { get; set; }
}

public class MycoBrainPowerStatus
{
    [JsonPropertyName("battery_voltage")]
    public double? BatteryVoltage { get; set; }

    [JsonPropertyName("source")]
    public string? PowerSource { get; set; }

    [JsonPropertyName("power_good")]
    public bool PowerGood { get; set; }
}

public class UartBufferStatus
{
    [JsonPropertyName("rx_available")]
    public int RxAvailable { get; set; }

    [JsonPropertyName("tx_available")]
    public int TxAvailable { get; set; }

    [JsonPropertyName("overflow_count")]
    public uint OverflowCount { get; set; }
}

public class MycoBrainDevice
{
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("device_type")]
    public string DeviceType { get; set; } = "mycobrain";

    [JsonPropertyName("firmware_version")]
    public string FirmwareVersion { get; set; } = string.Empty;

    [JsonPropertyName("i2c_addresses")]
    public List<string> I2CAddresses { get; set; } = new();

    [JsonPropertyName("analog_labels")]
    public Dictionary<string, string> AnalogLabels { get; set; } = new();

    [JsonPropertyName("mosfet_labels")]
    public Dictionary<string, string> MosfetLabels { get; set; } = new();

    [JsonPropertyName("power_status")]
    public MycoBrainPowerStatus? PowerStatus { get; set; }

    [JsonPropertyName("location")]
    public GeoLocation? Location { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    [JsonPropertyName("tenant_id")]
    public string? TenantId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("last_seen")]
    public DateTime? LastSeen { get; set; }

    [JsonPropertyName("status")]
    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;
}

public enum DeviceStatus
{
    Unknown,
    Online,
    Offline,
    Maintenance,
    Error
}

public class MycoBrainCommand
{
    [JsonPropertyName("cmd_id")]
    public MycoBrainCommandId CommandId { get; set; }

    [JsonPropertyName("params")]
    public Dictionary<string, object>? Parameters { get; set; }

    [JsonPropertyName("seq")]
    public uint SequenceNumber { get; set; }

    [JsonPropertyName("target_serial")]
    public string TargetSerial { get; set; } = string.Empty;
}

public enum MycoBrainCommandId
{
    SetMosfet = 0x01,
    SetTelemetryInterval = 0x02,
    ScanI2C = 0x03,
    GetStatus = 0x04,
    SetAnalogLabel = 0x05,
    SetMosfetLabel = 0x06,
    FirmwareUpdate = 0x07,
    Reset = 0x08
}
