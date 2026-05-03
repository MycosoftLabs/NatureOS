using System.Text;
using NatureOS.MINDEX.Models;

namespace NatureOS.Mycorrhizae;

/// <summary>
/// MDP v1 (Mycosoft Device Protocol): COBS framing + CRC16.
/// </summary>
public static class MDPv1Protocol
{
    public enum MessageType : byte
    {
        Telemetry = 0x01,
        Command = 0x02,
        Event = 0x03,
        Ack = 0x04,
        AcousticRaw = 0x20,
        AcousticFingerprint = 0x21,
        MagneticAnomaly = 0x22,
        OceanEnvironment = 0x23,
        TacticalAssessment = 0x24,
        MaritimeRelay = 0x25
    }

    public static byte[] EncodeMessage(MessageType messageType, byte[] payload)
    {
        var frame = new List<byte>(1 + payload.Length + 2) { (byte)messageType };
        frame.AddRange(payload);

        var crc = CalculateCRC16(frame.ToArray());
        frame.AddRange(BitConverter.GetBytes(crc));

        return COBSEncode(frame.ToArray());
    }

    public static (MessageType Type, byte[] Payload, bool Valid) DecodeMessage(byte[] encodedData)
    {
        try
        {
            var decoded = COBSDecode(encodedData);
            if (decoded == null || decoded.Length < 1 + 2)
                return (MessageType.Telemetry, Array.Empty<byte>(), false);

            var receivedCrc = BitConverter.ToUInt16(decoded, decoded.Length - 2);
            var frame = decoded.AsSpan(0, decoded.Length - 2).ToArray();
            var calculated = CalculateCRC16(frame);
            if (receivedCrc != calculated)
                return (MessageType.Telemetry, Array.Empty<byte>(), false);

            var type = (MessageType)frame[0];
            var payload = frame.AsSpan(1).ToArray();
            return (type, payload, true);
        }
        catch
        {
            return (MessageType.Telemetry, Array.Empty<byte>(), false);
        }
    }

    public static byte[] COBSEncode(byte[] data)
    {
        if (data.Length == 0)
            return new byte[] { 0x01, 0x00 };

        var encoded = new List<byte>(data.Length + 2);
        var distance = 1;
        var codeIndex = 0;
        encoded.Add(0);

        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == 0)
            {
                encoded[codeIndex] = (byte)distance;
                codeIndex = encoded.Count;
                encoded.Add(0);
                distance = 1;
                continue;
            }

            encoded.Add(data[i]);
            distance++;

            if (distance == 255)
            {
                encoded[codeIndex] = (byte)distance;
                codeIndex = encoded.Count;
                encoded.Add(0);
                distance = 1;
            }
        }

        encoded[codeIndex] = (byte)distance;
        encoded.Add(0);
        return encoded.ToArray();
    }

    public static byte[]? COBSDecode(byte[] encoded)
    {
        if (encoded.Length < 2)
            return null;

        var decoded = new List<byte>(encoded.Length);
        var i = 0;

        while (i < encoded.Length)
        {
            var code = encoded[i++];
            if (code == 0)
                break;

            for (var j = 1; j < code && i < encoded.Length; j++)
                decoded.Add(encoded[i++]);

            if (code < 255 && i < encoded.Length)
                decoded.Add(0);
        }

        // remove optional trailing 0 we may have appended during decode
        if (decoded.Count > 0 && decoded[^1] == 0)
            decoded.RemoveAt(decoded.Count - 1);

        return decoded.ToArray();
    }

    public static ushort CalculateCRC16(byte[] data)
    {
        const ushort poly = 0x1021;
        ushort crc = 0xFFFF;

        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ poly)
                    : (ushort)(crc << 1);
            }
        }

        return crc;
    }

    public static MycoBrainTelemetry? ParseNDJSON(string jsonLine)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<MycoBrainTelemetry>(
                jsonLine,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    public static byte[] BuildCommandFrame(MycoBrainCommand command)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(command);
        return EncodeMessage(MessageType.Command, Encoding.UTF8.GetBytes(json));
    }

    public static byte[] BuildTelemetryFrame(MycoBrainTelemetry telemetry)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(telemetry);
        return EncodeMessage(MessageType.Telemetry, Encoding.UTF8.GetBytes(json));
    }
}
