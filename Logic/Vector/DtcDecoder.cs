using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorCanTest.Logic.Vector
{
    public sealed class DtcRecord
    {
        public string Code { get; set; }
        public byte Status { get; set; }
        public byte[] RawBytes { get; set; }
    }

    /// <summary>
    /// Decodes the byte payload from SID 0x59 02 (ReadDTCInformation positive response).
    /// Each DTC is 4 bytes: B1 B2 B3 Status.
    /// </summary>
    public static class DtcDecoder
    {
        private static readonly string[] DtcTypePrefix = { "P", "C", "B", "U" };

        public static IReadOnlyList<DtcRecord> ParseReadDtcResponse(byte[] response)
        {
            if (response == null || response.Length < 3)
                throw new InvalidOperationException("ReadDTCInformation response is too short.");

            if (response[0] != 0x59 || response[1] != 0x02)
                throw new InvalidOperationException(
                    $"Response is not 59 02. Got: {BitConverter.ToString(response.Take(3).ToArray())}");

            // Skip the 3-byte header (59 02 <availabilityStatusMask>)
            byte[] records = response.Skip(3).ToArray();
            if (records.Length == 0) return Array.Empty<DtcRecord>();

            if (records.Length % 4 != 0)
                throw new InvalidOperationException(
                    $"DTC record payload length {records.Length} is not divisible by 4.");

            var result = new List<DtcRecord>();
            for (int i = 0; i < records.Length; i += 4)
            {
                byte b1 = records[i], b2 = records[i + 1], b3 = records[i + 2], status = records[i + 3];
                result.Add(new DtcRecord
                {
                    Code     = DecodeDtcCode(b1, b2, b3),
                    Status   = status,
                    RawBytes = new[] { b1, b2, b3, status }
                });
            }

            return result;
        }

        public static string DecodeDtcCode(byte b1, byte b2, byte b3)
        {
            int dtcType = (b1 & 0xC0) >> 6;
            int digit2  = (b1 & 0x30) >> 4;
            int digit3  =  b1 & 0x0F;
            return DtcTypePrefix[dtcType]
                   + digit2.ToString("X1")
                   + digit3.ToString("X1")
                   + b2.ToString("X2")
                   + b3.ToString("X2");
        }
    }
}
