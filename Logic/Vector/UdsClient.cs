using System;
using System.Linq;
using System.Text;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// UDS (ISO 14229) service client built on top of IsoTpClient.
    /// </summary>
    public sealed class UdsClient
    {
        private readonly IsoTpClient _isoTp;

        public UdsClient(IsoTpClient isoTp)
        {
            _isoTp = isoTp ?? throw new ArgumentNullException(nameof(isoTp));
        }

        /// <summary>
        /// SID 0x19 02 — ReadDTCInformation by status mask.
        /// Returns the full positive response payload (starting with 0x59).
        /// </summary>
        public byte[] ReadDtcByStatusMask(uint requestId, uint responseId, byte statusMask)
        {
            byte[] request  = { 0x19, 0x02, statusMask };
            byte[] response = _isoTp.SendAndReceive(requestId, responseId, request);

            if (response.Length < 3 || response[0] != 0x59 || response[1] != 0x02)
                throw new InvalidOperationException(
                    $"Invalid ReadDTCInformation response: {BitConverter.ToString(response)}");

            return response;
        }

        /// <summary>
        /// SID 0x22 F1 90 — ReadDataByIdentifier VIN.
        /// Returns the 17-character ASCII VIN string.
        /// </summary>
        public string ReadVin(uint requestId, uint responseId)
        {
            byte[] response = _isoTp.SendAndReceive(requestId, responseId, DtcCanConstants.ReadVin);

            if (response.Length < 3 || response[0] != 0x62 || response[1] != 0xF1 || response[2] != 0x90)
                throw new InvalidOperationException(
                    $"Invalid VIN response: {BitConverter.ToString(response)}");

            return Encoding.ASCII.GetString(response.Skip(3).ToArray()).TrimEnd('\0');
        }
    }
}
