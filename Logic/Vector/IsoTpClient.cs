using System;
using System.Collections.Generic;
using System.Linq;

namespace VectorCanTest.Logic.Vector
{
    /// <summary>
    /// ISO 15765-2 (ISO-TP) transport layer client.
    /// Supports Single Frame, First Frame + Consecutive Frame multi-frame reassembly,
    /// and NRC 0x78 (ResponsePending) with P2* timeout extension.
    /// </summary>
    public sealed class IsoTpClient
    {
        private readonly ICanBus _bus;
        private readonly int _p2TimeoutMs;
        private readonly int _p2StarTimeoutMs;

        public IsoTpClient(ICanBus bus, int p2TimeoutMs = 1000, int p2StarTimeoutMs = 5000)
        {
            _bus             = bus ?? throw new ArgumentNullException(nameof(bus));
            _p2TimeoutMs     = p2TimeoutMs;
            _p2StarTimeoutMs = p2StarTimeoutMs;
        }

        /// <summary>
        /// Sends a UDS request and reassembles the ISO-TP response.
        /// Returns the raw UDS response payload (without ISO-TP PCI bytes).
        /// </summary>
        public byte[] SendAndReceive(uint requestId, uint responseId, byte[] udsPayload)
        {
            if (udsPayload == null) throw new ArgumentNullException(nameof(udsPayload));
            if (udsPayload.Length > 7)
                throw new NotSupportedException("Single-frame UDS requests only (max 7 bytes).");

            // Build and send Single Frame request
            byte[] frame = new byte[8];
            frame[0] = (byte)udsPayload.Length;
            Array.Copy(udsPayload, 0, frame, 1, udsPayload.Length);
            _bus.Send(new CanMessage(requestId, frame), 1000);

            // Receive state
            var payload      = new List<byte>();
            int expectedLen  = -1;
            int nextSeq      = 1;
            DateTime p2Dead  = DateTime.UtcNow.AddMilliseconds(_p2TimeoutMs);
            DateTime? p2Star = null;

            while (true)
            {
                DateTime deadline = p2Star ?? p2Dead;
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException("Timeout waiting for ISO-TP response.");

                int remaining = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
                CanMessage rx = _bus.Recv(remaining);

                if (rx == null || rx.ArbitrationId != responseId || rx.Data.Length == 0)
                    continue;

                byte pci       = rx.Data[0];
                byte frameType = (byte)(pci & 0xF0);

                // ── NRC 0x78 ResponsePending ──────────────────────────────
                if (IsNrcPending(rx.Data))
                {
                    if (!p2Star.HasValue)
                        p2Star = DateTime.UtcNow.AddMilliseconds(_p2StarTimeoutMs);
                    continue;
                }

                // ── General Negative Response ─────────────────────────────
                if (IsNegativeResponse(rx.Data))
                    throw new UdsNegativeResponseException(rx.Data[3]); // NRC at index 3

                // ── Single Frame (0x0X) ────────────────────────────────────
                if (frameType == 0x00)
                {
                    int len = pci & 0x0F;
                    if (len == 0 || len > 7 || rx.Data.Length < len + 1)
                        throw new InvalidOperationException($"Invalid ISO-TP SF: PCI=0x{pci:X2}");
                    return rx.Data.Skip(1).Take(len).ToArray();
                }

                // ── First Frame (0x1X) ────────────────────────────────────
                if (frameType == 0x10)
                {
                    if (rx.Data.Length < 8)
                        throw new InvalidOperationException("ISO-TP FF frame too short.");

                    expectedLen = ((pci & 0x0F) << 8) | rx.Data[1];
                    if (expectedLen <= 6)
                        throw new InvalidOperationException($"Invalid ISO-TP FF length: {expectedLen}");

                    payload.Clear();
                    payload.AddRange(rx.Data.Skip(2).Take(6));

                    // Send Flow Control: ContinueToSend
                    _bus.Send(new CanMessage(requestId, DtcCanConstants.FlowControlContinueToSend), 1000);

                    if (!p2Star.HasValue)
                        p2Star = DateTime.UtcNow.AddMilliseconds(_p2StarTimeoutMs);

                    continue;
                }

                // ── Consecutive Frame (0x2X) ──────────────────────────────
                if (frameType == 0x20)
                {
                    if (expectedLen <= 0)
                        throw new InvalidOperationException("CF received before FF.");

                    int seq = pci & 0x0F;
                    if (seq != nextSeq)
                        throw new InvalidOperationException(
                            $"ISO-TP sequence error. Expected {nextSeq}, got {seq}.");

                    nextSeq = (nextSeq + 1) & 0x0F;
                    payload.AddRange(rx.Data.Skip(1).Take(7));

                    if (payload.Count >= expectedLen)
                        return payload.Take(expectedLen).ToArray();

                    continue;
                }

                // Ignore flow control frames sent by remote (0x30)
                if (frameType == 0x30) continue;

                throw new InvalidOperationException($"Unsupported ISO-TP frame type: 0x{frameType:X2}");
            }
        }

        // ── Negative response: SF with PCI len>=3, byte[1]=0x7F ─────────────
        private static bool IsNegativeResponse(byte[] data) =>
            data.Length >= 4 && data[1] == DtcCanConstants.NegativeResponse;

        private static bool IsNrcPending(byte[] data) =>
            data.Length >= 4 &&
            data[1] == DtcCanConstants.NegativeResponse &&
            data[3] == DtcCanConstants.NrcResponsePending;
    }
}
