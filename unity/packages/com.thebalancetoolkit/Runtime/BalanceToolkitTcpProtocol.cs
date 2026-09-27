using System;
using System.Collections.Generic;

/// <summary>
/// Decoder for The Balance Toolkit TCP streams.
///
/// The desktop application broadcasts fixed-size, big-endian binary records and never reads
/// from the client. There is no handshake or stream-selection command.
///
/// Raw stream (default port 11223), 40 bytes per record:
///   0-7   timestamp      i64  microseconds since the Unix epoch
///   8-15  mac_address    u64  board MAC address (low 48 bits)
///   16-19 top_right      f32
///   20-23 bottom_right   f32
///   24-27 top_left       f32
///   28-31 bottom_left    f32
///   32-35 cop_x          f32
///   36-39 cop_y          f32
///
/// Processed stream (default port 11224), 44 bytes per record:
///   0-7   timestamp      i64
///   8-15  mac_address    u64
///   16-19 v_cop_x        f32
///   20-23 v_cop_y        f32
///   24-27 stability      f32
///   28-31 dpsi_mlsi      f32
///   32-35 dpsi_apsi      f32
///   36-39 dpsi_vsi       f32
///   40-43 dpsi_overall   f32
/// </summary>
public static class BalanceToolkitTcpProtocol
{
    public const int DefaultRawPort = 11223;
    public const int DefaultProcessedPort = 11224;
    public const int RawRecordSize = 40;
    public const int ProcessedRecordSize = 44;

    public struct RawSample
    {
        public long TimestampMicros;
        public ulong MacAddress;
        public float TopRight;
        public float BottomRight;
        public float TopLeft;
        public float BottomLeft;
        public float CopX;
        public float CopY;
    }

    public struct ProcessedSample
    {
        public long TimestampMicros;
        public ulong MacAddress;
        public float VelocityCopX;
        public float VelocityCopY;
        public float StabilityIndex;
        public float DpsiMlsi;
        public float DpsiApsi;
        public float DpsiVsi;
        public float DpsiOverall;
    }

    /// <summary>
    /// Accumulates bytes from the socket and yields complete fixed-size records.
    /// A record may arrive split across several reads, so leftover bytes are kept.
    /// </summary>
    public sealed class RecordFramer
    {
        private readonly int recordSize;
        private readonly byte[] pending;
        private int pendingLength;

        public RecordFramer(int recordSize)
        {
            this.recordSize = recordSize;
            pending = new byte[recordSize];
        }

        public void Reset()
        {
            pendingLength = 0;
        }

        /// <summary>
        /// Feeds <paramref name="count"/> bytes from <paramref name="data"/> and invokes
        /// <paramref name="onRecord"/> with a slice (buffer, offset) for every complete record.
        /// </summary>
        public void Feed(byte[] data, int count, Action<byte[], int> onRecord)
        {
            int offset = 0;

            if (pendingLength > 0)
            {
                int needed = recordSize - pendingLength;
                int take = Math.Min(needed, count);
                Buffer.BlockCopy(data, 0, pending, pendingLength, take);
                pendingLength += take;
                offset = take;
                if (pendingLength < recordSize) return;
                onRecord(pending, 0);
                pendingLength = 0;
            }

            while (count - offset >= recordSize)
            {
                onRecord(data, offset);
                offset += recordSize;
            }

            int remainder = count - offset;
            if (remainder > 0)
            {
                Buffer.BlockCopy(data, offset, pending, 0, remainder);
                pendingLength = remainder;
            }
        }
    }

    public static RawSample DecodeRaw(byte[] buffer, int offset)
    {
        return new RawSample
        {
            TimestampMicros = ReadInt64BE(buffer, offset),
            MacAddress = ReadUInt64BE(buffer, offset + 8),
            TopRight = ReadSingleBE(buffer, offset + 16),
            BottomRight = ReadSingleBE(buffer, offset + 20),
            TopLeft = ReadSingleBE(buffer, offset + 24),
            BottomLeft = ReadSingleBE(buffer, offset + 28),
            CopX = ReadSingleBE(buffer, offset + 32),
            CopY = ReadSingleBE(buffer, offset + 36),
        };
    }

    public static ProcessedSample DecodeProcessed(byte[] buffer, int offset)
    {
        return new ProcessedSample
        {
            TimestampMicros = ReadInt64BE(buffer, offset),
            MacAddress = ReadUInt64BE(buffer, offset + 8),
            VelocityCopX = ReadSingleBE(buffer, offset + 16),
            VelocityCopY = ReadSingleBE(buffer, offset + 20),
            StabilityIndex = ReadSingleBE(buffer, offset + 24),
            DpsiMlsi = ReadSingleBE(buffer, offset + 28),
            DpsiApsi = ReadSingleBE(buffer, offset + 32),
            DpsiVsi = ReadSingleBE(buffer, offset + 36),
            DpsiOverall = ReadSingleBE(buffer, offset + 40),
        };
    }

    /// <summary>Formats a MAC address as AA:BB:CC:DD:EE:FF, or "N/A" when zero.</summary>
    public static string FormatMac(ulong mac)
    {
        if (mac == 0) return "N/A";
        var parts = new List<string>(6);
        for (int shift = 40; shift >= 0; shift -= 8)
        {
            parts.Add(((mac >> shift) & 0xFF).ToString("X2"));
        }
        return string.Join(":", parts);
    }

    private static ulong ReadUInt64BE(byte[] b, int o)
    {
        return ((ulong)b[o] << 56) | ((ulong)b[o + 1] << 48) | ((ulong)b[o + 2] << 40) | ((ulong)b[o + 3] << 32)
             | ((ulong)b[o + 4] << 24) | ((ulong)b[o + 5] << 16) | ((ulong)b[o + 6] << 8) | b[o + 7];
    }

    private static long ReadInt64BE(byte[] b, int o)
    {
        return unchecked((long)ReadUInt64BE(b, o));
    }

    private static float ReadSingleBE(byte[] b, int o)
    {
        uint bits = ((uint)b[o] << 24) | ((uint)b[o + 1] << 16) | ((uint)b[o + 2] << 8) | b[o + 3];
        return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
    }
}
