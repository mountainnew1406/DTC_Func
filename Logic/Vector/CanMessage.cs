using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// Classic CAN 2.0 message (max 8 bytes). Mirrors python-can's can.Message.
    /// </summary>
    public sealed class CanMessage
    {
        public uint ArbitrationId { get; }
        public byte[] Data { get; }
        public bool IsExtendedId { get; }
        public bool IsRx { get; internal set; }
        public DateTimeOffset Timestamp { get; internal set; }

        public int Dlc => Data.Length;

        public CanMessage(uint arbitrationId, IReadOnlyList<byte> data, bool isExtendedId = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Count > 8)
                throw new ArgumentOutOfRangeException(nameof(data), "Classic CAN payload must be <= 8 bytes.");

            ArbitrationId = arbitrationId;
            Data = data.ToArray();
            IsExtendedId = isExtendedId;
            Timestamp = DateTimeOffset.UtcNow;
        }
    }
}
